using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Tài khoản đăng nhập cho cả 3 vai trò. Không bao giờ hard-delete — vô hiệu hóa bằng status = DEACTIVATED (data rule: accounts never permanently deleted).
/// </summary>
public partial class User
{
    public Guid UserId { get; set; }

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    /// <summary>
    /// Chuyển lên từ patient_profiles — dùng chung cho cả 3 vai trò (trước chỉ Patient có). NULL cho phép vì Admin/Doctor không bắt buộc khai báo; với Patient đây là đầu vào lâm sàng phụ trợ cho AI (tuổi) — đúng tên đề tài &quot;…and Clinical Information&quot;. DB không tách được quyền theo cột: tầng ứng dụng phải ẩn trường này khỏi mọi giao diện/API quản lý tài khoản mà Admin dùng khi role của tài khoản đó = PATIENT — giữ đúng tinh thần &quot;Admin không truy cập dữ liệu y tế&quot; (§2.3). Không ẩn với tài khoản ADMIN/DOCTOR.
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Định danh đăng nhập duy nhất (thay cho username cũ) — Đăng nhập = số điện thoại + mật khẩu.
    /// </summary>
    public string Phone { get; set; } = null!;

    /// <summary>
    /// Chỉ dùng để tự cấp lại mật khẩu khi quên (không dùng để đăng nhập). Hệ thống gửi mật khẩu mới qua email này khi người dùng yêu cầu.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// TRUE sau khi Admin cấp lại mật khẩu (FT-06) — hệ thống ép đổi mật khẩu ở lần đăng nhập kế tiếp (UC-25).
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// Cờ bật đăng nhập sinh trắc học (FT-03). Mẫu vân tay/khuôn mặt nằm trong secure enclave của OS — KHÔNG BAO GIỜ lưu trong DB.
    /// </summary>
    public bool BiometricEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AiChatMessage> AiChatMessages { get; set; } = new List<AiChatMessage>();

    public virtual ICollection<AiModelVersion> AiModelVersions { get; set; } = new List<AiModelVersion>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();

    public virtual ICollection<Case> Cases { get; set; } = new List<Case>();

    public virtual ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();

    public virtual ICollection<PatientProfile> PatientProfileCreatedByNavigations { get; set; } = new List<PatientProfile>();

    public virtual PatientProfile? PatientProfileUser { get; set; }

    public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

    public virtual ICollection<ScheduleSlot> ScheduleSlots { get; set; } = new List<ScheduleSlot>();

    public virtual ICollection<UserFcmToken> UserFcmTokens { get; set; } = new List<UserFcmToken>();
}
