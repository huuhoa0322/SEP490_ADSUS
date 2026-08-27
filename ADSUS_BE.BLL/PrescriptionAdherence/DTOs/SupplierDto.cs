using System;
using System.ComponentModel.DataAnnotations;

namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs;

public record SupplierResponse(
    Guid SupplierId,
    string Name,
    string PhoneNumber,
    string Email,
    string Address,
    string TaxCode,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record CreateSupplierRequest(
    [Required] string Name,
    [Required] string PhoneNumber,
    [Required] string Email,
    [Required] string Address,
    [Required] string TaxCode
);

public record UpdateSupplierRequest(
    [Required] string Name,
    [Required] string PhoneNumber,
    [Required] string Email,
    [Required] string Address
);
