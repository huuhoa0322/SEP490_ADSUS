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
    public int LockedCount { get; set; }
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

    /// <summary>
    /// Số lượt đặt trung bình trên mỗi khung giờ mở.
    ///
    /// UCS ghi rõ "Schedule Slot Utilization Rate" là chỉ số TỰ SUY RA, PRD không định nghĩa
    /// ở đâu. Một khung giờ không giới hạn số lượt đặt (không có thuộc tính Capacity), nên
    /// không tính được "phần trăm lấp đầy" — ở đây trả về số trung bình, là thứ duy nhất có
    /// nghĩa với mô hình dữ liệu hiện tại.
    /// </summary>
    public double AverageBookingsPerSlot { get; set; }
}

/// <summary>Tuân thủ uống thuốc, tính trên toàn phòng khám.</summary>
public class AdherenceStatistics
{
    public int ScheduledDoseCount { get; set; }
    public int TakenDoseCount { get; set; }

    /// <summary>Phần trăm liều đã được bệnh nhân xác nhận uống.</summary>
    public double AdherenceRate { get; set; }
}
