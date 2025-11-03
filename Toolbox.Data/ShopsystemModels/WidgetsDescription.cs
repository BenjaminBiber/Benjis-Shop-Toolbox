using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DBCloner.Models;

[PrimaryKey("ShopId", "WidgetId", "LanguageId")]
public partial class WidgetsDescription
{
    [Key]
    public int WidgetId { get; set; }

    [Key]
    public short LanguageId { get; set; }

    [Key]
    public short ShopId { get; set; }

    public string? Body { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public byte[]? Rowversion { get; set; }
}
