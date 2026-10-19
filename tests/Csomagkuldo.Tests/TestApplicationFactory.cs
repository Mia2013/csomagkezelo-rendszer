using Csomagkuldo.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Csomagkuldo.Tests;

public class TestApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestKey = "OnlyForIsolatedTests-1016-NotAProductionSecret-123456789";
    public string DatabaseName { get; } = "Csomagkezelo_Tests_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", TestKey);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(
                $"Server=(localdb)\\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True"));
        });
    }

    public void CheckTestDatabase(AppDbContext context)
    {
        if (!DatabaseName.StartsWith("Csomagkezelo_Tests_") ||
            context.Database.GetDbConnection().Database != DatabaseName)
        {
            throw new InvalidOperationException("Csak a teszt által létrehozott adatbázis módosítható.");
        }
    }
}
