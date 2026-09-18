using Indamin.Performance.Data;
using Microsoft.EntityFrameworkCore;

namespace Indamin.Performance.Services;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, string root)
    {
        await db.Database.EnsureCreatedAsync();

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('SchemaVersions','U') IS NULL
            BEGIN
                CREATE TABLE SchemaVersions
                (
                    Id INT IDENTITY PRIMARY KEY,
                    VersionNumber INT NOT NULL UNIQUE,
                    AppliedAt DATETIME2 NOT NULL
                );
            END
            """);

        var current = await db.Database
            .SqlQueryRaw<int>("SELECT ISNULL(MAX(VersionNumber),0) AS [Value] FROM SchemaVersions")
            .SingleAsync();

        if (current < 2)
        {
            var path = Path.Combine(root, "Database", "002_upgrade.sql");
            if (!File.Exists(path))
                throw new FileNotFoundException("فایل ارتقای پایگاه داده پیدا نشد.", path);

            var sql = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(sql))
                throw new InvalidOperationException("اسکریپت ارتقای پایگاه داده خالی است.");

            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync(sql);
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO SchemaVersions(VersionNumber,AppliedAt) VALUES (2,SYSUTCDATETIME())");
            await tx.CommitAsync();
        }
    }
}