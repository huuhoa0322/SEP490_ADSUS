# ADSUS AI Backend (AIBE)

Đây là module AI của hệ thống ADSUS, chịu trách nhiệm nhận ảnh siêu âm và trả về dự đoán toạ độ (BBox/Mask) cũng như gợi ý thước đo (caliper).

> **Lưu ý:** Phần giao diện (Frontend) và lưu trữ dữ liệu xác nhận (C# Backend + Supabase) đã được tách riêng. AIBE hiện tại chỉ đóng vai trò phân tích AI thuần tuý.

## Kiến trúc

```
ADSUS_AIBE/
├── backend/
│   ├── main.py         # FastAPI: /api/detect, /api/reload-model
│   ├── geometry.py     # Toán học caliper (2 chiều: bbox/mask -> caliper, caliper -> vùng)
│   └── requirements.txt
├── start.bat           # Script khởi động cho Windows
└── start.sh            # Script khởi động cho Linux/Mac
```

## Chạy thử (Local)

Rất đơn giản, bạn chỉ cần chạy script `start.bat` (hoặc `start.sh` trên Linux/Mac):

```bash
# Trên Windows
start.bat

# Trên Linux/Mac
./start.sh
```

Script sẽ tự động:
1. Cài đặt các thư viện cần thiết (`requirements.txt`).
2. Khởi chạy FastAPI server trên cổng `8000`.

Lúc này AI Backend sẽ sẵn sàng nhận request từ `ADSUS_BE` (C# Backend) tại `http://localhost:8000`.

**Hoặc nếu bạn muốn chạy thủ công bằng Terminal như cũ:**
```bash
cd backend
uvicorn main:app --reload --port 8000
```

## Luồng hoạt động

1. **Tải ảnh lên** (từ FE -> BE C# -> AIBE qua API `/api/detect`)
   - Backend chạy `model.predict()`, trả về bbox (và mask nếu model là segmentation).
   - Dựa trên output đó, tính toán ra toạ độ 4 điểm caliper gợi ý (tính theo quy ước lâm sàng).
2. **Quản lý Model** (qua API `/api/reload-model`)
   - C# Backend có thể gọi API này để thay đổi model đang sử dụng một cách linh hoạt, tải trực tiếp từ Hugging Face.

## Tích hợp hệ thống
- Toàn bộ dữ liệu sau khi bác sĩ chỉnh sửa và xác nhận đều được **ADSUS_BE (C#)** lưu trực tiếp vào **Supabase**.
- Mọi logic gán nhãn, lưu trữ Dataset giờ đây do hệ thống Backend chính (C#) đảm nhiệm, AIBE hoạt động nhẹ nhàng như một Microservice AI thuần tuý.
