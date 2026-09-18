using Indamin.Performance.Data;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Services;
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db,string root)
    {
        await db.Database.EnsureCreatedAsync();
        var path=Path.Combine(root,"Database","002_upgrade.sql");
        if(File.Exists(path))
        {
            var sql=await File.ReadAllTextAsync(path);
            if(!string.IsNullOrWhiteSpace(sql)) await db.Database.ExecuteSqlRawAsync(sql);
        }
    }
}