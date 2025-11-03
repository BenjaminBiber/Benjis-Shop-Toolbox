namespace Toolbox.Data.Models.Interfaces;

public interface IConnectionStringResolver
{
    string GetCurrent();
    void SetCurrent(string connectionString);
    void Clear();
}