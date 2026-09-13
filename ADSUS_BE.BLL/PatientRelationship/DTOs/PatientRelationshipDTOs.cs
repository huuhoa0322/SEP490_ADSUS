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
/// Cập nhật thông tin người thân
/// </summary>
public record UpdateRelativeRequest(
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
