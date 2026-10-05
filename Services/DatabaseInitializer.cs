using Indamin.Performance.Data;
using Microsoft.EntityFrameworkCore;

namespace Indamin.Performance.Services;

public static class DatabaseInitializer
{
    private const string RequiredColumnSql = """
        SELECT COUNT(*) AS [Value]
        FROM sys.columns
        WHERE object_id = OBJECT_ID('EvaluationPeriods')
          AND name IN ('IsDeleted','DeletedAt')
        """;

    private const string RequiredTableSql = """
        SELECT COUNT(*) AS [Value]
        FROM sys.tables
        WHERE name IN (
            'AppUsers','OrgUnits','Positions','EvaluationDomains','Questions','PositionQuestions',
            'Employees','EvaluationPeriods','Evaluations','EvaluationScores',
            'EvaluationScoreHistory','EvaluatorChangeHistory','AuditLogs'
        )
        """;

    public static async Task InitializeAsync(AppDbContext db, string _)
    {
        var count = await db.Database.SqlQueryRaw<int>(RequiredTableSql).SingleAsync();
        var columnCount = count == 13
            ? await db.Database.SqlQueryRaw<int>(RequiredColumnSql).SingleAsync()
            : 0;

        if (count != 13 || columnCount != 2)
            throw new InvalidOperationException(
                "ساختار پایگاه داده کامل نیست. برای نسخه فعلی، دیتابیس آزمایشی را خالی ایجاد و فقط فایل Database/001_initial.sql را اجرا کنید.");
    }
}