namespace ADSUS_BE.BLL.CaseClinicServices.DTOs;

public class CaseClinicServiceResponse
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid ClinicServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceCode { get; set; } = string.Empty;
    public decimal PriceAtTime { get; set; }
    public DateTime CreatedAt { get; set; }
}
