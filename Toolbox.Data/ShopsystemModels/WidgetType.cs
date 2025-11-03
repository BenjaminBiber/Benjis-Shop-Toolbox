using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Toolbox.Data.ShopsystemModels;

[PrimaryKey("WidgetTypeId", "ShopId")]
public partial class WidgetType
{
    [Key]
    public int WidgetTypeId { get; set; }

    [Key]
    public short ShopId { get; set; }

    [StringLength(50)]
    public string Label { get; set; } = null!;

    [StringLength(50)]
    public string? ControllerName { get; set; }

    [StringLength(50)]
    public string? ActionName { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public byte[]? Rowversion { get; set; }

    [StringLength(50)]
    public string? ViewComponentName { get; set; }
}
