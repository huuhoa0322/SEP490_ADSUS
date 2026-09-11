using System.ComponentModel.DataAnnotations.Schema;

namespace ADSUS_BE.DAL.Entities;

public partial class InvoiceItem
{
    [Column("item_type")]
    public InvoiceItemType ItemType { get; set; } = InvoiceItemType.Medicine;
}
