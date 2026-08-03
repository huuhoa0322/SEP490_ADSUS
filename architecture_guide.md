# Tài liệu Kiến trúc Hệ thống (Microservices)

Tài liệu này mô tả chi tiết kiến trúc phân tán cho hệ thống Hỗ trợ Đánh dấu Tổn thương (Lesion Tool), bao gồm 3 thành phần chính: Frontend (React), Main Backend (C#), và AI Backend (Python).

---

## 1. Tổng quan Kiến trúc (Architecture Overview)

Hệ thống sử dụng mô hình **Microservices** để phân tách rõ ràng trách nhiệm:

1. **Frontend (React/Vue)**: Đóng vai trò là trung tâm điều phối trạng thái (State) và giao tiếp trực tiếp với cả 2 Backend.
2. **Main Backend (C#)**: Xử lý nghiệp vụ, quản lý người dùng, phân quyền, và tương tác trực tiếp với Database (Supabase).
3. **AI Backend (Python/FastAPI)**: Là một dịch vụ Stateless (không lưu trạng thái). Chỉ có một nhiệm vụ duy nhất: Nhận ảnh vào -> Tính toán bằng YOLO -> Trả về JSON tọa độ.

---

## 2. Luồng hoạt động chi tiết (Data Flow)

### A. Luồng Phát hiện AI & Xác nhận của Bác sĩ (Khám bệnh)
> [!NOTE]
> File ảnh gốc luôn được giữ trên RAM của Frontend cho đến khi Bác sĩ bấm "Lưu".

1. **Upload & Detect**: 
   - Người dùng (Bác sĩ) chọn ảnh trên giao diện Frontend.
   - Frontend giữ đối tượng `File` trong RAM và gọi API `POST [PYTHON_URL]/api/detect` gửi bản sao của ảnh đi.
   - Python Backend nhận ảnh, chạy YOLO, phân tích và trả về file JSON (tọa độ bbox, calipers). Python lập tức "quên" bức ảnh này đi.
2. **Review & Edit**:
   - Frontend nhận JSON, vẽ lên màn hình bên trái (Kết quả AI).
   - Bác sĩ thao tác chỉnh sửa tọa độ trên màn hình bên phải (Kết quả Bác sĩ). Frontend cập nhật tọa độ vào `state`.
3. **Save to Database**:
   - Bác sĩ bấm "Lưu".
   - Frontend đóng gói **File ảnh gốc** (vẫn lưu trong RAM) và **Tọa độ chốt cuối cùng** thành dạng `multipart/form-data`.
   - Frontend gọi API `POST [C#_URL]/api/save-annotation` gửi lên Backend C#.
   - C# nhận dữ liệu: Upload file ảnh lên Storage (Cloudflare R2/S3/Supabase Storage) lấy link URL, sau đó lưu tọa độ và link URL vào bảng dữ liệu trong Supabase DB.

### B. Luồng Quản lý Phiên bản AI (Admin)
1. Kỹ sư AI train xong model mới (`best.pt`) và upload lên một repository **Private** trên Hugging Face.
2. Admin vào trang quản trị (trên Frontend), điền thông tin model mới (tên Repo, tên file, độ chính xác) và gọi API C# để lưu thành một dòng mới vào bảng `ai_model_versions` trong Supabase, gán `is_active = TRUE`.
3. C# lưu DB xong, gọi API webhook `POST [PYTHON_URL]/api/reload-model` sang Python.
4. Python nhận lệnh, tự động đọc token bảo mật, tải file từ Hugging Face về (caching) và load lại Model vào RAM. Hệ thống cập nhật AI không cần khởi động lại Server!

---

## 3. Thiết kế Cơ sở dữ liệu (Supabase SQL)

Bảng quản lý phiên bản AI (Chạy trên SQL Editor của Supabase):

```sql
CREATE TABLE ai_model_versions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    version_name VARCHAR(50) NOT NULL,
    hf_repo_id VARCHAR(255) NOT NULL, -- Ví dụ: 'buiduong/lesion-model'
    hf_filename VARCHAR(255) NOT NULL, -- Ví dụ: 'best_v2.pt'
    metrics_map FLOAT,
    metrics_recall FLOAT,
    description TEXT,
    is_active BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Chỉ cho phép 1 model được active tại một thời điểm (Tùy chọn)
CREATE UNIQUE INDEX one_active_model_idx ON ai_model_versions (is_active) WHERE is_active = TRUE;
```

---

## 4. Code cấu trúc lại cho AI Backend (Python)

Khi bạn thiết lập Workspace mới, file `backend/main.py` của Python sẽ được viết lại cho **Stateless hoàn toàn**, cực kỳ mỏng nhẹ. Bạn có thể sử dụng cấu trúc code sau:

> [!TIP]
> Nhớ cài thêm thư viện: `pip install huggingface_hub python-dotenv fastapi uvicorn ultralytics pillow`

```python
import os
import io
from fastapi import FastAPI, File, UploadFile, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from huggingface_hub import hf_hub_download
from ultralytics import YOLO
from PIL import Image
import numpy as np

# Load môi trường (cần có HUGGINGFACE_TOKEN trong file .env)
HF_TOKEN = os.getenv("HUGGINGFACE_TOKEN")

app = FastAPI(title="AI Lesion Detection Service")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], # Cho phép React gọi
    allow_methods=["*"],
    allow_headers=["*"],
)

# Biến toàn cục giữ model đang chạy
current_model = None
current_repo_id = None
current_filename = None

def load_ai_model(repo_id: str, filename: str):
    global current_model, current_repo_id, current_filename
    print(f"Đang tải model từ HF: {repo_id}/{filename} ...")
    
    # hf_hub_download tự động kiểm tra cache, không tải lại nếu đã có
    model_path = hf_hub_download(
        repo_id=repo_id,
        filename=filename,
        token=HF_TOKEN
    )
    
    current_model = YOLO(model_path)
    current_repo_id = repo_id
    current_filename = filename
    print("Model đã sẵn sàng!")

@app.on_event("startup")
async def startup_event():
    # Khi server vừa chạy, bạn có thể thiết lập mặc định (hoặc gọi API sang C# để hỏi model nào đang active)
    # Tạm thời hardcode mặc định khi khởi động:
    try:
        load_ai_model(
            repo_id=os.getenv("DEFAULT_HF_REPO", "buiduong/lesion-model"),
            filename=os.getenv("DEFAULT_HF_FILE", "best.pt")
        )
    except Exception as e:
        print("Lỗi khi load model ban đầu:", e)

@app.post("/api/detect")
async def detect_image(file: UploadFile = File(...)):
    if current_model is None:
        raise HTTPException(status_code=503, detail="Model AI chưa sẵn sàng")
        
    contents = await file.read()
    image = Image.open(io.BytesIO(contents)).convert("RGB")
    
    # Chạy YOLO
    results = current_model.predict(np.array(image), conf=0.25, verbose=False)
    
    # ... (Logic xử lý bounding box, NMS, mảng array giống y hệt file cũ của bạn) ...
    # ... (Giả sử trả về biến 'detections') ...
    detections = [] 
    
    # Trả về JSON (Không lưu ảnh ra ổ cứng!)
    return {
        "status": "success",
        "detections": detections
    }

@app.post("/api/reload-model")
async def reload_model(repo_id: str, filename: str):
    """
    API này sẽ được Backend C# gọi khi Admin bấm nút đổi phiên bản trên web
    """
    try:
        load_ai_model(repo_id, filename)
        return {"status": "success", "message": f"Đã chuyển sang model {filename}"}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
```
