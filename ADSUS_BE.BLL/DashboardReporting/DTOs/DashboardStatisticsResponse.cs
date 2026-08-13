namespace ADSUS_BE.BLL.DashboardReporting.DTOs;

/// <summary>
/// UC-05 FT-10 — toàn bộ số liệu của màn thống kê SCR-08.
///
/// BR-01: chỉ có số đếm và tỉ lệ đã tổng hợp. Không trường nào ở đây chứa tên, số điện
/// thoại, hay bất kỳ thông tin nhận dạng nào của bệnh nhân.
/// BR-02: chỉ để đọc, không thao tác gì được từ màn này.
/// </summary>
public class DashboardStatisticsResponse
{
    /// <summary>Khoảng thời gian thực sự được dùng để tính, sau khi đã áp mặc định.</summary>
    public string FromDate { get; set; } = string.Empty;

    public string ToDate { get; set; } = string.Empty;

    public AccountStatistics Accounts { get; set; } = new();

    public ClinicalStatistics Clinical { get; set; } = new();

    public AppointmentStatistics Appointments { get; set; } = new();

    public AdherenceStatistics Adherence { get; set; } = new();

    public AiModelMetrics ActiveAiModel { get; set; } = new();

    /// <summary>
    /// UC-05 bước 3 — diễn biến từng ngày để vẽ biểu đồ xu hướng.
    ///
    /// LUÔN đủ mọi ngày trong khoảng, kể cả ngày không có gì phát sinh (giá trị 0). Bỏ trống
    /// ngày rỗng thì đường biểu đồ nối thẳng qua khoảng trống, nhìn như hoạt động vẫn đều
    /// trong khi thực tế là không có gì.
    /// </summary>
    public IReadOnlyList<DailyPoint> Trend { get; set; } = Array.Empty<DailyPoint>();
}

public class AiModelMetrics
{
    public string VersionCode { get; set; } = string.Empty;
    public decimal? Precision { get; set; }
    public decimal? Recall { get; set; }
    public decimal? Map50 { get; set; }
    public DateTime? LastEvaluatedAt { get; set; }
}

/// <summary>Một điểm trên biểu đồ xu hướng.</summary>
public class DailyPoint
{
    /// <summary>Định dạng yyyy-MM-dd.</summary>
    public string Date { get; set; } = string.Empty;

    public int NewAccounts { get; set; }
    public int Cases { get; set; }
    public int Appointments { get; set; }
}

/// <summary>Tài khoản — tính trên toàn hệ thống, không lọc theo thời gian.</summary>
public class AccountStatistics
{
    public int Total { get; set; }
    public int AdminCount { get; set; }
    public int DoctorCount { get; set; }
    public int NurseCount { get; set; }
    public int PatientCount { get; set; }
    public int ActiveCount { get; set; }
    public int DeactivatedCount { get; set; }

    /// <summary>Số tài khoản mới tạo trong khoảng đang xem.</summary>
    public int NewInRange { get; set; }

    /// <summary>Phần trăm tài khoản còn hoạt động, làm tròn 1 chữ số thập phân.</summary>
    public double ActiveRate { get; set; }
}

/// <summary>Ca khám và kết quả AI.</summary>
public class ClinicalStatistics
{
    public int CaseCount { get; set; }
    public int AiRunCount { get; set; }
    public int AiConfirmedCount { get; set; }
    public int AiRejectedCount { get; set; }

    /// <summary>Số kết quả AI bác sĩ chưa duyệt — bệnh nhân chưa thấy được (GB-05).</summary>
    public int AiPendingCount { get; set; }

    /// <summary>
    /// Tỉ lệ bác sĩ xác nhận, tính trên số kết quả ĐÃ DUYỆT (confirmed + rejected).
    /// Cố ý không tính cả phần đang chờ vào mẫu số: gộp vào thì chỉ số này tụt xuống mỗi khi
    /// có nhiều ca mới, trong khi bác sĩ chẳng làm gì sai cả.
    /// </summary>
    public double AiConfirmRate { get; set; }
}

/// <summary>Lịch hẹn.</summary>
public class AppointmentStatistics
{
    public int BookedCount { get; set; }
    public int CancelledCount { get; set; }
    public int SlotCount { get; set; }

    /// <summary>Tỉ lệ huỷ trên tổng số lượt đặt.</summary>
    public double CancellationRate { get; set; }

    // ĐÃ BỎ: "Schedule Slot Utilization Rate %" và số lượt đặt trung bình mỗi khung giờ.
    //
    // UCS gọi tên chỉ số "Schedule Slot Utilization Rate %" nhưng tự gắn cờ là TỰ SUY RA,
    // PRD không định nghĩa ở đâu. Mà tính tỉ lệ lấp đầy thì cần biết một khung giờ nhận
    // được tối đa bao nhiêu lượt — ScheduleSlot KHÔNG có cột Capacity, và chú thích ngay
    // trong entity ghi rõ "không giới hạn số Appointment/slot" (quyết định UCS 3.1 ngày
    // 23/07/2026). Không có mẫu số thì không có tỉ lệ.
    //
    // Trước đây chỗ này trả về số lượt đặt TRUNG BÌNH mỗi khung giờ để lấp chỗ trống, nhưng
    // đó là một đại lượng khác hẳn thứ tài liệu gọi tên. Để lại thì người đọc báo cáo tưởng
    // đang xem tỉ lệ lấp đầy, mà con số ấy không nói lên điều đó — bỏ hẳn còn hơn hiển thị
    // một số dễ hiểu nhầm. Số khung giờ đã mở vẫn giữ ở SlotCount.
    //
    // Nếu sau này nhóm thêm Capacity cho ScheduleSlot thì mới tính được tỉ lệ thật.
}

/// <summary>Tuân thủ uống thuốc, tính trên toàn phòng khám.</summary>
public class AdherenceStatistics
{
    public int ScheduledDoseCount { get; set; }
    public int TakenDoseCount { get; set; }

    /// <summary>Phần trăm liều đã được bệnh nhân xác nhận uống.</summary>
    public double AdherenceRate { get; set; }
}
