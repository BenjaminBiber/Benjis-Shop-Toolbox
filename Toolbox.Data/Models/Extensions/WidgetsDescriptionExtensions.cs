using Toolbox.Data.Services;
using Toolbox.Data.ShopsystemModels;

namespace Toolbox.Data.Models.Extensions;

public static class WidgetsDescriptionExtensions
{
    public static String GetInsertStatement(this WidgetsDescription widgetsDescription, SqlBuilder sqlBuilder)
    {
        var widgets = new List<WidgetsDescription>(){widgetsDescription};
        return GetInsertStatement(widgets, sqlBuilder);
    }

    public static String GetInsertStatement(this List<WidgetsDescription> widgetsDescriptions, SqlBuilder sqlBuilder)
    {
        return sqlBuilder.BuildInsertStatement("Widgets", widgetsDescriptions, [
            ("ShopId", x => x.ShopId),
            ("LanguageId", x => x.LanguageId),
            ("Body", x => x.Body),
            ("CreatedBy", x => x.CreatedBy),
            ("CreatedAt", x => x.CreatedAt)
        ]);
    }
}