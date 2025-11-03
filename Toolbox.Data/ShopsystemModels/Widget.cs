using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Toolbox.Data.ShopsystemModels;

[PrimaryKey("WidgetId", "ShopId")]
public partial class Widget
{
    [Key]
    public int WidgetId { get; set; }

    [Key]
    public short ShopId { get; set; }

    public int WidgetTypeId { get; set; }

    [StringLength(255)]
    public string Label { get; set; } = null!;

    [StringLength(50)]
    [Unicode(false)]
    public string LocationKey { get; set; } = null!;

    [StringLength(500)]
    public string? Config { get; set; }

    public DateTime PublishAt { get; set; }

    public DateTime? PublishUntil { get; set; }

    public int Sorting { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public byte[]? Rowversion { get; set; }

    public Guid? PrivacyCategoryUId { get; set; }

    [StringLength(50)]
    public string? PrivacyIdentifier { get; set; }
}
