namespace ADSUS_BE.BLL.PatientRelationship.DTOs;

// === REQUESTS ===

/// <summary>
/// Thêm người thân vào danh bạ (Mobile API).
/// </summary>
public record AddRelativeRequest(
    string FullName,
    string? Phone,
    DateOnly? DateOfBirth,
    string? RelationshipName
);

/// <summary>
/// Cập nhật thông tin người thân (Họ tên, SĐT, Ngày sinh, Quan hệ).
/// FullName/Phone/DateOfBirth chỉ cập nhật khi PatientProfile.UserId IS NULL (guest).
/// </summary>
public record UpdateRelativeRequest(
    string? FullName,
    string? Phone,
    DateOnly? DateOfBirth,
    string? RelationshipName
);

// === RESPONSES ===

/// <summary>
/// Trả về 1 người thân trong danh bạ
/// </summary>
public record RelativeResponse(
    Guid RelationshipId,
    Guid PatientProfileId,
    string PatientName,
    string? PatientPhone,
    DateOnly? DateOfBirth,
    string? Gender,
    string? RelationshipName,
    bool IsRegisteredAccount,
    DateTime CreatedAt
);

/// <summary>
/// Trả về danh sách người thân
/// </summary>
public record RelativesListResponse(
    IReadOnlyList<RelativeResponse> Relatives
);

/// <summary>
/// Mối quan hệ dùng khi đặt lịch hộ: ai là bệnh nhân thật được khám (PatientProfileId) và ai
/// là chủ danh bạ đã thêm người thân này (OwnerUserId).
/// </summary>
public record RelationshipBookingTarget(
    Guid RelationshipId,
    Guid PatientProfileId,
    Guid OwnerUserId
);
