namespace ADSUS_BE.BLL.ClinicServiceManagement.DTOs;

public class CreateClinicServiceRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
}
