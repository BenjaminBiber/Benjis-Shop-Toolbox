using Toolbox.Data.Services;
using Toolbox.Data.ShopsystemModels;

namespace Toolbox.Data.Models.Extensions;

public static class CustomWidgetLocationExtensions
{
    public static String GetInsertStatement(this CustomWidgetLocation customWidgetLocation, SqlBuilder sqlBuilder)
    {
        var widgets = new List<CustomWidgetLocation>(){customWidgetLocation};
        return GetInsertStatement(widgets, sqlBuilder);
    }

    public static String GetInsertStatement(this List<CustomWidgetLocation> customWidgetLocations, SqlBuilder sqlBuilder)
    {
        return sqlBuilder.BuildInsertStatement("CustomWidgetLocations", customWidgetLocations, [
            ("Label", x => x.Label),
            ("Description", x => x.Description),
            ("CreatedBy", x => x.CreatedBy),
            ("CreatedAt", x => x.CreatedAt)
        ]);
    }
}