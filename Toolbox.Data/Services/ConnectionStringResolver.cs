using Toolbox.Data.Models.Interfaces;
namespace Toolbox.Data.Services;

public sealed class ConnectionStringResolver : IConnectionStringResolver
{
    private static readonly AsyncLocal<string?> _current = new();
    public string GetCurrent() =>
        _current.Value ?? throw new InvalidOperationException("No DB selected.");
    public void SetCurrent(string cs) => _current.Value = cs;

    public void Clear() => _current.Value = null;
}