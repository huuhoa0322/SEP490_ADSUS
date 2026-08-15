using System.ComponentModel.DataAnnotations.Schema;
namespace ADSUS_BE.DAL.Entities;

public partial class Medicine
{
    [Column("status")]
    public MedicineStatus Status { get; set; } = MedicineStatus.Active;
}
