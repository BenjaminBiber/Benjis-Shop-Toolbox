using Toolbox.Data.Services;
using Toolbox.Data.ShopsystemModels;

namespace Toolbox.Data.Models.Extensions;

public static class ObjectExtensionExtensions
{
    public static String GetInsertStatement(this ObjectExtension objectExtension, SqlBuilder sqlBuilder)
    {
        var widgets = new List<ObjectExtension>(){objectExtension};
        return GetInsertStatement(widgets, sqlBuilder);
    }

    public static String GetInsertStatement(this List<ObjectExtension> objectExtensions, SqlBuilder sqlBuilder)
    {
        return sqlBuilder.BuildInsertStatement("ObjectExtensions", objectExtensions, [
            ("ObjectName", x => x.ObjectName),
            ("ExtensionName", x => x.ExtensionName),
            ("ExtensionTypeId", x => x.ExtensionTypeId),
            ("DbType", x => x.DbType),
            ("DefaultValue", x => x.DefaultValue),
            ("SortOrder", x => x.SortOrder),
            ("Title", x => x.Title),
            ("Tooltip", x => x.Tooltip),
            ("ColumnSize", x => x.ColumnSize),
            ("ColumnSize", x => x.ColumnSize),
            ("ColumnTypeId", x => x.ColumnTypeId),
            ("ColumnWidth", x => x.ColumnWidth),
            ("ColumnWeight", x => x.ColumnWeight),
            ("FieldTypeId", x => x.FieldTypeId),
            ("FieldWidth", x => x.FieldWidth),
            ("IsFilter", x => x.IsFilter),
            ("FilterTypeId", x => x.FilterTypeId),
            ("FilterMinValue", x => x.FilterMinValue),
            ("FilterMaxValue", x => x.FilterMaxValue),
            ("IsSearchable", x => x.IsSearchable),
            ("FieldHeight", x => x.FieldHeight),
            ("ComboBoxFieldType", x => x.ComboBoxFieldType),
            ("Source", x => x.Source),
            ("SourceProxy", x => x.SourceProxy),
            ("NumberFormat", x => x.NumberFormat),
            ("CreatedBy", x => x.CreatedBy),
            ("CreatedAt", x => x.CreatedAt)
        ]);
    }
}