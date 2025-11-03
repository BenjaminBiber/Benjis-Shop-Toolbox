using Toolbox.Data.Services;
using Toolbox.Data.ShopsystemModels;

namespace Toolbox.Data.Models.Extensions;

public static class WidgetExtensions
{
    public static String GetInsertStatement(this Widget widget, SqlBuilder sqlBuilder)
    {
        var widgets = new List<Widget>(){widget};
        return GetInsertStatement(widgets, sqlBuilder);
    }

    public static String GetInsertStatement(this List<Widget> widgets, SqlBuilder sqlBuilder)
    {
        return sqlBuilder.BuildInsertStatement("Widgets", widgets, [
            ("ShopId", x => x.ShopId),
            ("WidgetTypeId", x => x.WidgetTypeId),
            ("Label", x => x.Label),
            ("LocationKey", x => x.LocationKey),
            ("Config", x => x.Config),
            ("PublishAt", x => x.PublishAt),
            ("PublishUntil", x => x.PublishUntil),
            ("Sorting", x => x.Sorting),
            ("PrivacyCategoryUId", x => x.PrivacyCategoryUId),
            ("PrivacyIdentifier", x => x.PrivacyIdentifier),
            ("CreatedBy", x => x.CreatedBy),
            ("CreatedAt", x => x.CreatedAt)
        ]);
    }
}