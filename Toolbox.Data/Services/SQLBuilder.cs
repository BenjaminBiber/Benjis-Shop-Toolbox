using System.Globalization;
using System.Text;
using Toolbox.Data.Models.Interfaces;

namespace Toolbox.Data.Services;

public class SqlBuilder
{
    private readonly INotificationService  _notificationService;

    public SqlBuilder(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }
    
    public string BuildInsertStatement<T>(string tableName, IReadOnlyList<T> data,
        (string Column, Func<T, object?> Selector)[] columns)
    {
        if (String.IsNullOrEmpty(tableName))
        {
            _notificationService.Error("Tabellen Name darf nicht leer sein");
        }

        if (data == null || data.Count == 0)
        {
            _notificationService.Error("Tabellen Data darf nicht leer sein");
        }

        if (columns == null || columns.Length == 0)
        {
            _notificationService.Error("Tabellen Columns darf nicht leer sein");
        }
        
        var sb = new StringBuilder();
        sb.Append("INSERT INTO ").Append('[').Append(tableName).Append(']')
            .Append(" (")
            .Append(string.Join(", ", columns.Select(c => $"[{c.Column}]")))
            .Append(") VALUES ");
        
        for (int i = 0; i < data.Count; i++)
        {
            var row = data[i];
            var values = columns.Select(c => SqlLiteral(c.Selector(row)));
            sb.Append('(').Append(string.Join(", ", values)).Append(')');
            if (i < data.Count - 1) sb.Append(',');
        }

        sb.Append(';');
        return sb.ToString();
    }

    private static string SqlLiteral(object? value)
    {
        if (value is null) return "NULL";

        switch (value)
        {
            case string s:
                return $"N'{s.Replace("'", "''")}'";
            case char ch:
                return $"N'{ch.ToString().Replace("'", "''")}'";
            case bool b:
                return b ? "1" : "0";
            case byte[] bytes:
                return "0x" + BitConverter.ToString(bytes).Replace("-", "");
            case DateTime dt:
                return $"'{dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}'";
            case DateTimeOffset dto:
                return $"'{dto.ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture)}'";
            case Guid g:
                return $"'{g}'";
            case decimal dec:
                return dec.ToString(CultureInfo.InvariantCulture);
            case float f:
                return f.ToString(CultureInfo.InvariantCulture);
            case double d:
                return d.ToString(CultureInfo.InvariantCulture);
            case sbyte or byte or short or ushort or int or uint or long or ulong:
                return Convert.ToString(value, CultureInfo.InvariantCulture)!;
            default:
                var str = value.ToString() ?? "";
                return $"N'{str.Replace("'", "''")}'";
        }
    }
}