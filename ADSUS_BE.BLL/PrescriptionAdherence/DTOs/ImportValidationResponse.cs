namespace ADSUS_BE.BLL.PrescriptionAdherence.DTOs
{
    public class ImportValidationResponse
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
