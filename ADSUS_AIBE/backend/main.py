"""
Backend cho công cụ hỗ trợ AI đánh dấu tổn thương nội mạc tử cung.

Luồng hoạt động:
    1. POST /api/detect        — upload ảnh, chạy model AI (best.pt), trả về
                                  bbox/mask AI phát hiện + gợi ý toạ độ 4 điểm caliper.

Chạy thử:
    pip install -r requirements.txt
    Chạy file start.bat (Windows) hoặc start.sh (Linux/Mac)
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
from fastapi import FastAPI, File, Header, HTTPException, UploadFile, Form
from fastapi.middleware.cors import CORSMiddleware
from PIL import Image
from pydantic import BaseModel

from geometry import (
    CaliperPair,
    Point,
    lesion_region_from_calipers,
    suggest_calipers_from_bbox,
    suggest_calipers_from_mask
)

load_dotenv()
HF_TOKEN = os.environ.get("HUGGINGFACE_TOKEN")

CONF_THRESHOLD = float(os.environ.get("CONF_THRESHOLD", "0.15"))

app = FastAPI(title="Lesion Annotation Assist API")
app.add_middleware(
    CORSMiddleware,
    allow_origins=[],  # Chỉ gọi server-to-server (C# Backend) — không bao giờ có trình duyệt gọi thẳng
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


def verify_webhook_token(authorization: str | None):
    """
    Kiểm tra Bearer token dùng chung giữa AI Backend và C# Backend.

    Dùng cho mọi endpoint gọi từ C# Backend (server-to-server) — kể cả /api/detect,
    vì trên Render endpoint này có URL public, không còn nằm sau mạng nội bộ Docker
    như lúc chạy VPS nữa.
    """
    expected_token = os.environ.get("WEBHOOK_TOKEN")
    if not expected_token:
        raise HTTPException(status_code=500, detail="WEBHOOK_TOKEN is not configured on the server")
    if not authorization or not authorization.startswith("Bearer "):
        raise HTTPException(status_code=401, detail="Missing or invalid Authorization header")

    token = authorization.split(" ")[1]
    if token != expected_token:
        raise HTTPException(status_code=403, detail="Invalid token")


@app.post("/api/reload-model")
async def reload_model(req: ReloadModelRequest, authorization: str | None = Header(default=None)):
    """
    Cập nhật model mới theo repo_id và filename (từ Admin qua C# Backend).
    """
    try:
        verify_webhook_token(authorization)

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


# ---------------------------------------------------------------------------
# Endpoint 1: Detect — AI gợi ý
# ---------------------------------------------------------------------------

@app.post("/api/detect")
async def detect(
    file: UploadFile = File(...),
    repo_id: str | None = Form(default=None),
    filename: str | None = Form(default=None),
    authorization: str | None = Header(default=None)
):
    verify_webhook_token(authorization)

    contents = await file.read()
    try:
        image = Image.open(io.BytesIO(contents)).convert("RGB")
    except Exception:
        raise HTTPException(400, "File ảnh không hợp lệ.")

    img_w, img_h = image.size
    session_id = str(uuid.uuid4())

    if repo_id and filename:
        if _model is None or _current_repo_id != repo_id or _current_filename != filename:
            load_ai_model(repo_id, filename)

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

        # Normalize to 0-1 for YOLO format integration
        nx1, ny1, nx2, ny2 = x1 / img_w, y1 / img_h, x2 / img_w, y2 / img_h
        detections.append({
            "confidence": conf,
            "class_id": cls_id,
            "bbox": {"xmin": nx1, "ymin": ny1, "xmax": nx2, "ymax": ny2},
            "suggested_calipers": {
                "pair_a": [pair_a.p1.as_tuple(), pair_a.p2.as_tuple()],
                "pair_b": [pair_b.p1.as_tuple(), pair_b.p2.as_tuple()],
            },
        })

    return {
        "session_id": session_id,
        "image_width": img_w,
        "image_height": img_h,
        "detections": detections,
    }


@app.get("/api/health")
async def health():
    return {"status": "ok", "model_loaded": _model is not None}
