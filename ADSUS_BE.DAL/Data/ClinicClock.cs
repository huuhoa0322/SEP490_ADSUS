namespace ADSUS_BE.DAL.Data;

/// <summary>
/// Quy đổi giữa giờ UTC lưu trong database và NGÀY LÀM VIỆC của phòng khám.
///
/// Mọi mốc thời gian trong database đều là UTC — đúng cách làm, không đổi. Nhưng khi Admin
/// hỏi "hôm nay có bao nhiêu tài khoản mới", "hôm nay" của họ là hôm nay ở Việt Nam chứ
/// không phải ở London.
///
/// Không xử lý thì lệch thấy rõ: từ 00:00 đến 07:00 giờ Việt Nam, UTC vẫn đang ở ngày hôm
/// trước, nên dashboard mở lúc 6 giờ sáng sẽ nói "hôm nay" trong khi hiển thị số liệu của
/// hôm qua. Các bản ghi tạo trong khung 7 tiếng đó cũng bị dồn nhầm sang ngày trước trên
/// biểu đồ.
///
/// Đặt ở tầng DAL vì cả tầng nghiệp vụ (chọn khoảng thời gian) lẫn repository (gom nhóm
/// theo ngày) đều cần đúng một con số này — để hai nơi tự khai riêng là sớm muộn cũng lệch.
///
/// Việt Nam không có giờ mùa hè nên độ lệch cố định +07:00, không cần tra bảng múi giờ của
/// hệ điều hành (mà tên múi giờ trên Windows và Linux lại khác nhau).
/// </summary>
public static class ClinicClock
{
    /// <summary>Độ lệch múi giờ của phòng khám so với UTC.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    /// <summary>Số giờ lệch, dạng số — dùng cho biểu thức LINQ dịch xuống SQL.</summary>
    public const double OffsetHours = 7;

    /// <summary>Hôm nay theo lịch phòng khám.</summary>
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.Add(Offset));

    /// <summary>
    /// Đổi một ngày ở phòng khám thành mốc UTC lúc 00:00 của ngày đó.
    /// Ví dụ 01/08 tại Việt Nam bắt đầu từ 17:00 ngày 31/07 UTC.
    /// </summary>
    public static DateTime StartOfDayUtc(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue) - Offset, DateTimeKind.Utc);

    /// <summary>
    /// Mốc UTC ngay sau khi ngày đó kết thúc ở phòng khám.
    ///
    /// Dùng kiểu "nhỏ hơn mốc này" thay vì "nhỏ hơn hoặc bằng cuối ngày": so sánh với cuối
    /// ngày là mất sạch dữ liệu phát sinh trong ngày cuối, vì mọi mốc giờ đều lớn hơn 00:00.
    /// </summary>
    public static DateTime EndOfDayExclusiveUtc(DateOnly date) => StartOfDayUtc(date.AddDays(1));
}
