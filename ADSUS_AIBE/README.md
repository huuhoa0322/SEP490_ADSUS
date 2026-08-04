# Công cụ xác nhận tổn thương (AI-assist + bác sĩ xác nhận)

## Kiến trúc

```
lesion-tool/
├── backend/
│   ├── main.py         # FastAPI: /api/detect, /api/save-annotation
│   ├── geometry.py      # Toán học caliper (2 chiều: bbox/mask -> caliper, caliper -> vùng)
│   └── requirements.txt
└── frontend/
    └── index.html        # Split-screen UI, thuần HTML/CSS/JS (không cần build tool)
```

Backend phục vụ luôn frontend tĩnh ở `/` — chỉ cần chạy 1 server, không cần
CORS phức tạp khi deploy chung domain.

## Chạy thử

```bash
cd backend
pip install -r requirements.txt

# Trỏ tới file best.pt của bạn
export MODEL_PATH=/duong/dan/toi/best.pt
# Ngưỡng confidence hiển thị gợi ý AI (tuỳ chỉnh theo khuyến nghị recall-ưu-tiên
# đã bàn — có thể để thấp hơn mặc định 0.25 vì bác sĩ sẽ tự lọc lại)
export CONF_THRESHOLD=0.15

uvicorn main:app --reload --port 8000
```

Mở trình duyệt tại `http://localhost:8000`.

## Luồng hoạt động

1. **Tải ảnh lên** → backend chạy `model.predict()`, trả về bbox (và mask nếu
   model là segmentation) + toạ độ 4 điểm caliper gợi ý (tính theo đúng quy
   ước lâm sàng: trục dài nhất trước — dùng `cv2.minAreaRect` trên mask nếu
   có, fallback về bbox thẳng trục nếu model chỉ detect).
2. **Panel trái** hiển thị kết quả AI thô (bbox cam, %confidence) — **chỉ để
   tham khảo, không tương tác được** — đúng yêu cầu "không dùng ảnh do AI
   detect ra trong hồ sơ y tế".
3. **Panel phải** hiển thị ảnh gốc + caliper (màu xanh, giống caliper thật
   trên máy siêu âm) mà bác sĩ có thể **kéo-thả từng điểm** để chỉnh cho khớp
   ranh giới thật, hoặc bấm "+ Thêm caliper mới" để tự đánh dấu vùng AI bỏ
   sót (click đủ 4 điểm: 2 điểm trục dài, 2 điểm trục ngắn).
4. **Lưu xác nhận** → backend nhận đúng 4 điểm cuối cùng, tính lại vùng tổn
   thương (`lesion_region_from_calipers`) theo đúng chiều ngược, ghi ra:
   - Ảnh gốc → `confirmed_dataset/images/<id>.png`
   - Nhãn YOLO detect chuẩn (axis-aligned bbox) → `confirmed_dataset/labels/<id>.txt`
   - Nhãn YOLO OBB (oriented, khớp đúng góc xoay thật) → `confirmed_dataset/labels/<id>_obb.txt`
   - Audit trail đầy đủ (gợi ý AI ban đầu + caliper bác sĩ chỉnh cuối + mã
     bác sĩ + thời gian) → `confirmed_dataset/audit/<session_id>/`

## Vì sao lưu cả bbox chuẩn lẫn OBB

Nếu bạn tiếp tục dùng YOLO detect thường (không xoay), dùng file `.txt`
không có hậu tố `_obb`. Nếu sau này muốn thử YOLO-OBB (Ultralytics hỗ trợ
sẵn `yolov8n-obb.pt` v.v.) để tận dụng đúng hướng trục caliper thật (không bị
mất thông tin khi ép về bbox thẳng trục), dùng file `_obb.txt` — không cần
tính toán lại.

## Giá trị dữ liệu thu được

Mỗi lần bác sĩ lưu xác nhận là 1 mẫu dữ liệu **gán nhãn bởi người thật, trên
ảnh gốc 100% (không qua bất kỳ AI inpainting/generative nào)** — đúng loại dữ
liệu "vàng" để dùng cho các lần retrain sau, và giải quyết đúng vấn đề dataset
nhỏ đã gặp phải xuyên suốt quá trình nghiên cứu. Audit trail cũng cho phép đo
lường "bác sĩ sửa AI nhiều hay ít" theo thời gian — một chỉ số hữu ích để
theo dõi model có đang cải thiện qua các lần retrain hay không.

## Giới hạn / việc cần làm thêm trước khi dùng thật

- **Không có xác thực người dùng** (login bác sĩ) — hiện chỉ có ô nhập
  "Mã bác sĩ" dạng text tự do, cần thay bằng auth thật trước khi triển khai.
- **`_PENDING_SESSIONS` lưu trong RAM** — mất khi restart server; nếu cần
  độ bền cao hơn, chuyển sang Redis/DB.
- **Không xử lý quy đổi pixel → cm/mm** (cần đọc `PixelSpacing` từ DICOM nếu
  chuyển sang làm việc trực tiếp trên DICOM thay vì ảnh export JPG/PNG).
- **CORS đang mở `*`** — thu hẹp lại đúng domain frontend khi deploy thật.
