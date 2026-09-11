namespace ADSUS_BE.BLL.ClinicServiceManagement.DTOs;

public class UpdateClinicServiceRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public bool? IsActive { get; set; }
}
