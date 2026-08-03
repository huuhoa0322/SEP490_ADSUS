"""
Backend cho công cụ hỗ trợ đánh dấu tổn thương nội mạc tử cung.

Luồng hoạt động:
    1. POST /api/detect        — upload ảnh, chạy model AI (best.pt), trả về
                                  bbox/mask AI phát hiện + gợi ý toạ độ 4 điểm caliper.
    2. POST /api/save-annotation — bác sĩ xác nhận/chỉnh caliper trên ảnh gốc,
                                  gửi lại 4 điểm cuối cùng; backend tính lại vùng
                                  tổn thương và LƯU vào dataset cho lần retrain sau
                                  (ảnh gốc + nhãn do người xác nhận, không qua AI inpaint).

Chạy thử:
    pip install -r requirements.txt
    export MODEL_PATH=/path/to/best.pt
    uvicorn main:app --reload --port 8000
"""

import io
import json
import os
import time
import uuid
from pathlib import Path

from dotenv import load_dotenv
from huggingface_hub import hf_hub_download
import numpy as np
from fastapi import FastAPI, File, Header, HTTPException, UploadFile
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from PIL import Image
from pydantic import BaseModel

from geometry import (
    CaliperPair,
    Point,
    lesion_region_from_calipers,
    suggest_calipers_from_bbox,
    suggest_calipers_from_mask,
    to_yolo_bbox_label,
    to_yolo_obb_label,
)

load_dotenv()
HF_TOKEN = os.environ.get("HUGGINGFACE_TOKEN")

CONF_THRESHOLD = float(os.environ.get("CONF_THRESHOLD", "0.15"))

# Nơi lưu ảnh gốc + nhãn bác sĩ xác nhận — chính là dữ liệu training "vàng"
DATA_DIR = Path(os.environ.get("DATA_DIR", "./confirmed_dataset"))
DATA_DIR.mkdir(parents=True, exist_ok=True)
(DATA_DIR / "images").mkdir(exist_ok=True)
(DATA_DIR / "labels").mkdir(exist_ok=True)
(DATA_DIR / "audit").mkdir(exist_ok=True)  # lưu cả bản AI gợi ý ban đầu để đối chiếu/audit

# Ảnh đang chờ bác sĩ xử lý, giữ tạm trong bộ nhớ theo session_id để /save-annotation
# biết ảnh gốc là ảnh nào mà không cần bác sĩ upload lại
_PENDING_SESSIONS: dict[str, dict] = {}

app = FastAPI(title="Lesion Annotation Assist API")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Thu hẹp lại đúng domain frontend khi lên production
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.middleware("http")
async def add_no_cache_headers(request, call_next):
    response = await call_next(request)
    if request.url.path == "/" or request.url.path.endswith(".html"):
        response.headers["Cache-Control"] = "no-cache, no-store, must-revalidate"
        response.headers["Pragma"] = "no-cache"
        response.headers["Expires"] = "0"
    return response


_model = None
_current_repo_id = "tranqui247/adsus"
_current_filename = "yolo-efficientNetv2-m1-nbl.pt"

def load_ai_model(repo_id: str, filename: str):
    global _model, _current_repo_id, _current_filename
    print(f"Đang tải model từ HF: {repo_id}/{filename} ...")
    try:
        from ultralytics import YOLO
    except ImportError as e:
        raise RuntimeError("Chưa cài ultralytics. Chạy: pip install ultralytics") from e
    
    model_path = hf_hub_download(
        repo_id=repo_id,
        filename=filename,
        token=HF_TOKEN
    )
    _model = YOLO(model_path)
    _current_repo_id = repo_id
    _current_filename = filename
    print("Model đã sẵn sàng!")

def get_model():
    """Lazy-load model — chỉ nạp 1 lần, dùng lại cho mọi request."""
    global _model
    if _model is None:
        load_ai_model(_current_repo_id, _current_filename)
    return _model

class ReloadModelRequest(BaseModel):
    repo_id: str
    filename: str

@app.post("/api/reload-model")
async def reload_model(req: ReloadModelRequest, authorization: str | None = Header(default=None)):
    """
    Cập nhật model mới theo repo_id và filename (từ Admin qua C# Backend).
    """
    try:
        expected_token = os.environ.get("WEBHOOK_TOKEN")
        if not expected_token:
            raise HTTPException(status_code=500, detail="WEBHOOK_TOKEN is not configured on the server")
        if not authorization or not authorization.startswith("Bearer "):
            raise HTTPException(status_code=401, detail="Missing or invalid Authorization header")
        
        token = authorization.split(" ")[1]
        if token != expected_token:
            raise HTTPException(status_code=403, detail="Invalid token")

        load_ai_model(req.repo_id, req.filename)
        return {"status": "success", "message": f"Đã chuyển sang model {req.filename}"}
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


# ---------------------------------------------------------------------------
# Schemas
# ---------------------------------------------------------------------------

class PointIn(BaseModel):
    x: float
    y: float


class SaveAnnotationRequest(BaseModel):
    session_id: str
    # Mỗi tổn thương bác sĩ GIỮ LẠI = 1 entry gồm 4 điểm (2 cặp caliper) + nguồn gốc
    lesions: list[dict]  # [{"pair_a":[...], "pair_b":[...], "source": "ai"|"doctor_added", "ai_detection_index": int|None}]
    rejected_ai_indices: list[int] = []  # index trong ai_suggestion.json mà bác sĩ đã bấm "từ chối" (False Positive)
    doctor_id: str | None = None
    notes: str | None = None


# ---------------------------------------------------------------------------
# Endpoint 1: Detect — AI gợi ý
# ---------------------------------------------------------------------------

@app.post("/api/detect")
async def detect(file: UploadFile = File(...)):
    contents = await file.read()
    try:
        image = Image.open(io.BytesIO(contents)).convert("RGB")
    except Exception:
        raise HTTPException(400, "File ảnh không hợp lệ.")

    img_w, img_h = image.size
    session_id = str(uuid.uuid4())

    # Lưu ảnh gốc tạm thời cho session này (dùng lại khi bác sĩ save-annotation)
    session_dir = DATA_DIR / "audit" / session_id
    session_dir.mkdir(parents=True, exist_ok=True)
    orig_path = session_dir / "original.png"
    image.save(orig_path)

    IOU_THRESHOLD = float(os.environ.get("IOU_THRESHOLD", "0.1"))
    model = get_model()
    results = model.predict(np.array(image), conf=CONF_THRESHOLD, verbose=False)
    r = results[0]

    # Hàm tính IoU thủ công
    def calculate_iou(box1, box2):
        x_left = max(box1[0], box2[0])
        y_top = max(box1[1], box2[1])
        x_right = min(box1[2], box2[2])
        y_bottom = min(box1[3], box2[3])

        if x_right < x_left or y_bottom < y_top:
            return 0.0

        intersection_area = (x_right - x_left) * (y_bottom - y_top)
        box1_area = (box1[2] - box1[0]) * (box1[3] - box1[1])
        box2_area = (box2[2] - box2[0]) * (box2[3] - box2[1])
        union_area = box1_area + box2_area - intersection_area

        return intersection_area / union_area if union_area > 0 else 0.0

    detections = []
    has_mask = getattr(r, "masks", None) is not None

    # Lấy toàn bộ box thô từ YOLO
    raw_detections = []
    for i, box in enumerate(r.boxes):
        raw_detections.append({
            "orig_idx": i,
            "bbox": [float(box.xyxy[0][0]), float(box.xyxy[0][1]), float(box.xyxy[0][2]), float(box.xyxy[0][3])],
            "conf": float(box.conf[0]),
            "cls_id": int(box.cls[0])
        })

    # Sắp xếp theo độ tin cậy giảm dần
    raw_detections.sort(key=lambda x: x["conf"], reverse=True)

    # NMS thủ công
    kept_detections = []
    for d in raw_detections:
        keep = True
        for kd in kept_detections:
            if calculate_iou(d["bbox"], kd["bbox"]) > IOU_THRESHOLD:
                keep = False
                break
        if keep:
            kept_detections.append(d)

    detections = []
    for d in kept_detections:
        i = d["orig_idx"]
        x1, y1, x2, y2 = d["bbox"]
        conf = d["conf"]
        cls_id = d["cls_id"]

        if has_mask:
            mask_arr = r.masks.data[i].cpu().numpy()
            # Resize mask về đúng kích thước ảnh gốc nếu model output khác size
            if mask_arr.shape != (img_h, img_w):
                import cv2
                mask_arr = cv2.resize(mask_arr, (img_w, img_h))
            pair_a, pair_b = suggest_calipers_from_mask(mask_arr)
        else:
            pair_a, pair_b = suggest_calipers_from_bbox(x1, y1, x2, y2)

        detections.append({
            "confidence": conf,
            "class_id": cls_id,
            "bbox": {"x1": x1, "y1": y1, "x2": x2, "y2": y2},
            "suggested_calipers": {
                "pair_a": [pair_a.p1.as_tuple(), pair_a.p2.as_tuple()],
                "pair_b": [pair_b.p1.as_tuple(), pair_b.p2.as_tuple()],
            },
        })

    # Lưu lại đúng gợi ý AI ban đầu để sau này audit "bác sĩ có sửa nhiều không"
    with open(session_dir / "ai_suggestion.json", "w") as f:
        json.dump(detections, f, indent=2)

    _PENDING_SESSIONS[session_id] = {
        "image_path": str(orig_path),
        "width": img_w,
        "height": img_h,
        "created_at": time.time(),
    }

    return {
        "session_id": session_id,
        "image_width": img_w,
        "image_height": img_h,
        "detections": detections,
    }


# ---------------------------------------------------------------------------
# Endpoint 2: Save annotation — bác sĩ xác nhận, ghi vào dataset training
# ---------------------------------------------------------------------------

@app.post("/api/save-annotation")
async def save_annotation(req: SaveAnnotationRequest):
    session = _PENDING_SESSIONS.get(req.session_id)
    if session is None:
        raise HTTPException(404, "Session không tồn tại hoặc đã hết hạn, hãy detect lại ảnh.")

    img_w, img_h = session["width"], session["height"]
    record_id = str(uuid.uuid4())

    yolo_bbox_lines = []
    yolo_obb_lines = []
    regions_out = []

    for lesion in req.lesions:
        pair_a = CaliperPair(
            p1=Point(**lesion["pair_a"][0]), p2=Point(**lesion["pair_a"][1])
        )
        pair_b = CaliperPair(
            p1=Point(**lesion["pair_b"][0]), p2=Point(**lesion["pair_b"][1])
        )
        region = lesion_region_from_calipers(pair_a, pair_b)
        regions_out.append(region.to_dict())
        yolo_bbox_lines.append(to_yolo_bbox_label(region, img_w, img_h))
        yolo_obb_lines.append(to_yolo_obb_label(region, img_w, img_h))

    # Copy ảnh gốc sang thư mục dataset chính thức + ghi nhãn
    import shutil
    image_dest = DATA_DIR / "images" / f"{record_id}.png"
    shutil.copy(session["image_path"], image_dest)

    label_bbox_path = DATA_DIR / "labels" / f"{record_id}.txt"
    label_obb_path = DATA_DIR / "labels" / f"{record_id}_obb.txt"
    label_bbox_path.write_text("\n".join(yolo_bbox_lines))
    label_obb_path.write_text("\n".join(yolo_obb_lines))

    # Audit trail — bắt buộc cho hồ sơ y tế: lưu lại ai xác nhận, khi nào,
    # caliper cuối cùng khác gì so với gợi ý AI ban đầu, VÀ box nào bị từ chối
    # (False Positive) — đây là dữ liệu để tính precision/recall thật sau này,
    # KHÔNG dùng để tự động phán "đúng/sai" thay chuyên môn bác sĩ — chỉ ghi
    # lại đúng những gì bác sĩ đã quyết định.
    audit_record = {
        "record_id": record_id,
        "session_id": req.session_id,
        "doctor_id": req.doctor_id,
        "notes": req.notes,
        "timestamp": time.time(),
        "final_calipers": [
            {**lesion, "region": regions_out[i]} for i, lesion in enumerate(req.lesions)
        ],
        "rejected_ai_indices": req.rejected_ai_indices,
    }
    with open(DATA_DIR / "audit" / req.session_id / "doctor_confirmed.json", "w") as f:
        json.dump(audit_record, f, indent=2)

    del _PENDING_SESSIONS[req.session_id]

    return {
        "record_id": record_id,
        "saved_image": str(image_dest),
        "saved_label_bbox": str(label_bbox_path),
        "saved_label_obb": str(label_obb_path),
        "regions": regions_out,
    }


@app.get("/api/metrics")
async def metrics():
    """
    Tính precision/recall thực tế từ toàn bộ audit trail đã có, dựa HOÀN TOÀN
    vào quyết định bác sĩ đã ghi lại — không tự suy diễn đúng/sai:
        - AI box được bác sĩ giữ (source='ai', không rejected)  -> True Positive
        - AI box bị bác sĩ bấm "từ chối"                         -> False Positive
        - Lesion bác sĩ tự thêm (source='doctor_added')          -> False Negative (AI bỏ sót)

    LƯU Ý QUAN TRỌNG (nêu rõ trong luận văn nếu dùng số liệu này):
    đây KHÔNG phải benchmark độc lập — bác sĩ đã thấy gợi ý AI trước khi
    quyết định, nên có khả năng thiên lệch theo hướng chấp nhận gợi ý
    (anchoring bias) khiến số liệu có xu hướng lạc quan hơn thực tế.
    Chỉ nên dùng để giám sát xu hướng theo thời gian (model có tệ đi không),
    không dùng thay thế cho tập test offline độc lập.
    """
    audit_root = DATA_DIR / "audit"
    tp, fp, fn = 0, 0, 0
    per_session = []

    for session_dir in audit_root.iterdir():
        if not session_dir.is_dir():
            continue
        confirmed_path = session_dir / "doctor_confirmed.json"
        if not confirmed_path.exists():
            continue  # session chưa được bác sĩ lưu (bỏ dở giữa chừng)

        with open(confirmed_path) as f:
            confirmed = json.load(f)

        session_tp = sum(
            1 for l in confirmed["final_calipers"]
            if l.get("source") == "ai"
        )
        session_fp = len(confirmed.get("rejected_ai_indices", []))
        session_fn = sum(
            1 for l in confirmed["final_calipers"]
            if l.get("source") == "doctor_added"
        )

        tp += session_tp
        fp += session_fp
        fn += session_fn
        per_session.append({
            "session_id": confirmed["session_id"],
            "doctor_id": confirmed.get("doctor_id"),
            "timestamp": confirmed["timestamp"],
            "tp": session_tp, "fp": session_fp, "fn": session_fn,
        })

    precision = tp / (tp + fp) if (tp + fp) > 0 else None
    recall = tp / (tp + fn) if (tp + fn) > 0 else None

    return {
        "note": (
            "Số liệu tính từ audit trail thực tế (bác sĩ đã xác nhận/từ chối/tự thêm). "
            "Có thể lạc quan hơn năng lực thật do anchoring bias — không thay thế test set độc lập."
        ),
        "total_sessions": len(per_session),
        "true_positive": tp, "false_positive": fp, "false_negative": fn,
        "precision": precision, "recall": recall,
        "per_session": sorted(per_session, key=lambda s: s["timestamp"]),
    }


@app.get("/api/health")
async def health():
    return {"status": "ok", "model_loaded": _model is not None}


# Phục vụ frontend tĩnh (nếu build cùng thư mục ../frontend)
frontend_dir = Path(__file__).parent.parent / "frontend"
if frontend_dir.exists():
    app.mount("/", StaticFiles(directory=str(frontend_dir), html=True), name="frontend")
