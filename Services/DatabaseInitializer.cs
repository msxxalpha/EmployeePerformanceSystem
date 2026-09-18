using Indamin.Performance.Data;
using Microsoft.EntityFrameworkCore;

namespace Indamin.Performance.Services;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, string _)
    {
        // The SQL script in Database/001_initial.sql is the source of truth for a
        // new installation. EnsureCreated is retained only as a safe fallback for
        // a brand-new empty database; it never runs upgrade scripts.
        await db.Database.EnsureCreatedAsync();
    }
}