/// Đọc mốc thời gian (timestamp) backend trả về, khai ở ĐÚNG MỘT CHỖ phía mobile.
///
/// Backend lưu và trả mọi mốc thời gian theo UTC, dạng ISO-8601 có hậu tố `Z`
/// (vd `2026-09-24T01:30:00Z`). `DateTime.parse` giữ nguyên giá trị UTC, nên đem thẳng lên UI
/// thì giờ/ngày hiển thị lệch 7 tiếng so với giờ Việt Nam. Đổi về giờ máy (`toLocal()`) ngay
/// lúc parse để mọi màn hình dùng chung một quy tắc.
///
/// Chỉ dùng cho mốc thời gian (createdAt, sentAt, uploadedAt...). Trường chỉ có ngày
/// (`DateOnly` bên backend như `visitDate`, `logDate`, `slotDate`) không có `Z` nên
/// `DateTime.parse` thường đã đúng.
class ApiDateTime {
  const ApiDateTime._();

  static DateTime parse(String raw) => DateTime.parse(raw).toLocal();

  static DateTime? tryParse(String? raw) {
    if (raw == null || raw.isEmpty) return null;
    return DateTime.tryParse(raw)?.toLocal();
  }
}
