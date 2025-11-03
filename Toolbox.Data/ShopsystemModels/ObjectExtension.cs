using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Toolbox.Data.ShopsystemModels;

[PrimaryKey("ObjectName", "ExtensionName", "ExtensionTypeId")]
public partial class ObjectExtension
{
    [Key]
    [StringLength(50)]
    [Unicode(false)]
    public string ObjectName { get; set; } = null!;

    [Key]
    [StringLength(50)]
    [Unicode(false)]
    public string ExtensionName { get; set; } = null!;

    [Key]
    public int ExtensionTypeId { get; set; }

    public int DbType { get; set; }

    [StringLength(4000)]
    public string? DefaultValue { get; set; }

    public short SortOrder { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string Title { get; set; } = null!;

    [StringLength(200)]
    public string? Tooltip { get; set; }

    public short? ColumnSize { get; set; }

    public int ColumnTypeId { get; set; }

    public short? ColumnWidth { get; set; }

    public short? ColumnWeight { get; set; }

    public bool? ColumnHidden { get; set; }

    public int FieldTypeId { get; set; }

    public short? FieldWidth { get; set; }

    public bool? IsFilter { get; set; }

    public int FilterTypeId { get; set; }

    [StringLength(50)]
    public string? FilterMinValue { get; set; }

    [StringLength(50)]
    public string? FilterMaxValue { get; set; }

    public bool IsSearchable { get; set; }

    public short FieldHeight { get; set; }

    public int ComboBoxFieldType { get; set; }

    [StringLength(500)]
    public string? Source { get; set; }

    [StringLength(3000)]
    public string? SourceProxy { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public int? ModifiedBy { get; set; }

    public byte[]? Rowversion { get; set; }

    [StringLength(50)]
    public string? NumberFormat { get; set; }
}
