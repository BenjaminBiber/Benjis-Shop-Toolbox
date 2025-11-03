using Microsoft.EntityFrameworkCore;
using Toolbox.Data.DataContexts;
using Toolbox.Data.Models.Interfaces;

namespace Toolbox.Data.Models;

public sealed class ExternalDbContextFactory : IExternalDbContextFactory
{
    private readonly IConnectionStringResolver _resolver;
    public ExternalDbContextFactory(IConnectionStringResolver resolver) => _resolver = resolver;

    public ExternalAppDbContext Create()
    {
        var cs = _resolver.GetCurrent();
        var options = new DbContextOptionsBuilder<ExternalAppDbContext>()
            .UseSqlServer(cs)
            .Options;
        return new ExternalAppDbContext(options);
    }
}