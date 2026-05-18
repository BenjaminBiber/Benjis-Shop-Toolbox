using System.IO;
using Microsoft.EntityFrameworkCore;
using Toolbox.Data.Models.Interfaces;
using Toolbox.Data.Models.ShopYaml;

namespace Toolbox.Data.Services;

public sealed class ExtensionInventoryService
{
    private readonly IConnectionStringResolver _resolver;
    private readonly IExternalDbContextFactory _factory;

    public ExtensionInventoryService(IConnectionStringResolver resolver, IExternalDbContextFactory factory)
    {
        _resolver = resolver;
        _factory = factory;
    }

    public async Task<List<string>> LoadFromDatabaseAsync(DatabaseConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection == null)
        {
            throw new InvalidOperationException("Keine Datenbankverbindung vorhanden.");
        }

        var connectionString = connection.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Datenbankverbindung ist ungueltig.");
        }

        _resolver.SetCurrent(connectionString);
        await using var db = _factory.Create();

        var names = await db.ObjectExtensions
            .AsNoTracking()
            .Where(x => x.ExtensionTypeId == 2)
            .Select(x => x.ExtensionName)
            .ToListAsync(cancellationToken);

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(name.Trim());
            }
        }

        return result.ToList();
    }

    public List<string> LoadFromCsv(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Kein CSV-Pfad angegeben.");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("CSV-Datei nicht gefunden.", filePath);
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.Contains(","))
            {
                continue;
            }

            result.Add(line.Trim());
        }

        return result.ToList();
    }
}

