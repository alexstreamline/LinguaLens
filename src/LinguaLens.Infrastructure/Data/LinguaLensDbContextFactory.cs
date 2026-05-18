using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LinguaLens.Infrastructure.Data;

/// <summary>
/// Design-time factory used exclusively by EF Core CLI tools (dotnet ef migrations).
/// Not used at runtime — the actual DbContext is registered in App.xaml.cs via DI.
/// </summary>
public class LinguaLensDbContextFactory : IDesignTimeDbContextFactory<LinguaLensDbContext>
{
    public LinguaLensDbContext CreateDbContext(string[] args)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LinguaLens", "lingualens.db");

        var options = new DbContextOptionsBuilder<LinguaLensDbContext>()
            .UseSqlite($"Data Source={dbPath};")
            .Options;

        return new LinguaLensDbContext(options);
    }
}
