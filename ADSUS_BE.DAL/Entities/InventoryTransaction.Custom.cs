using System;

namespace ADSUS_BE.DAL.Entities;

public partial class InventoryTransaction
{
    [System.ComponentModel.DataAnnotations.Schema.Column("txn_type")]
    public InventoryTxnType TxnType { get; set; }
}
