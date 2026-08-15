using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations.Schema;

namespace ADSUS_BE.DAL.Entities;

public partial class HealthLog
{
    [Column("log_type")]
    public HealthLogType LogType { get; set; }
}
