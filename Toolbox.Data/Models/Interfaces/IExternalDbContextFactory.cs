using Toolbox.Data.DataContexts;

namespace Toolbox.Data.Models.Interfaces;

public interface IExternalDbContextFactory
{
    ExternalAppDbContext Create();
}