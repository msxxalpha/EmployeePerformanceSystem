using Indamin.Performance.Data;
using Microsoft.EntityFrameworkCore;

namespace Indamin.Performance.Services;

public static class DatabaseInitializer
{
    private static readonly string[] RequiredTables =
    [
        "AppUsers","OrgUnits","Positions","Questions","PositionQuestions",
        "Employees","EvaluationPeriods","Evaluations","EvaluationScores",
        "EvaluationScoreHistory","EvaluatorChangeHistory","AuditLogs"
    ];

    public static async Task InitializeAsync(AppDbContext db, string _)
    {
        var names = string.Join(",", RequiredTables.Select((_, i) => $"@p{i}"));
        var parameters = RequiredTables.Select((name, i) => new Microsoft.Data.SqlClient.SqlParameter($"@p{i}", name)).ToArray();

        var count = await db.Database
            .SqlQueryRaw<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name IN ({names})", parameters)
            .SingleAsync();

        if (count != RequiredTables.Length)
            throw new InvalidOperationException(
                "ساختار پایگاه داده کامل نیست. ابتدا فقط فایل Database/001_initial.sql را روی پایگاه داده هدف اجرا کنید.");
    }
}