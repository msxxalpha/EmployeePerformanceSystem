using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indamin.Performance.Controllers;

[Authorize(Policy = "AdminOnly")]
public class ReportsController(AppDbContext db, ExcelService excel) : Controller
{
    private const string ExcelMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public async Task<IActionResult> Index(int? periodId)
    {
        var periods = await db.Periods.AsNoTracking().OrderByDescending(x => x.StartAt).ToListAsync();
        var selectable = periods.Where(x => x.StartAt <= DateTime.Now).ToList();
        int? selectedId = periodId.HasValue && selectable.Any(x => x.Id == periodId.Value)
            ? periodId.Value
            : selectable.FirstOrDefault()?.Id;
        var report = selectedId.HasValue ? await BuildReport(selectedId.Value) : EmptyReport();
        var rows = selectedId.HasValue ? await QueryRows(selectedId.Value) : [];
        return View(new ReportVm(periods, selectedId, report, rows));
    }

    [HttpGet]
    public async Task<IActionResult> Export(int? periodId)
    {
        var selectable = await db.Periods.AsNoTracking().Where(x => x.StartAt <= DateTime.Now).OrderByDescending(x => x.StartAt).ToListAsync();
        var selectedId = periodId.HasValue && selectable.Any(x => x.Id == periodId.Value) ? periodId.Value : selectable.FirstOrDefault()?.Id ?? 0;
        var rows = await QueryRows(selectedId);
        return File(excel.Evaluations(rows.Select(x => (x.Employee, x.Evaluator, x.Unit, x.Position, x.Score, x.Max, x.Status))), ExcelMime, "ManagementEvaluationReport.xlsx");
    }

    private async Task<ReportData> BuildReport(int periodId)
    {
        var period = await db.Periods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == periodId);
        var rows = await QueryRows(periodId);
        if (period == null) return EmptyReport();
        var evaluated = rows.Where(x => x.Max > 0).ToList();
        var percentages = evaluated.Select(x => Percent(x.Score, x.Max)).ToList();
        var avg = percentages.Count == 0 ? 0 : Math.Round(percentages.Average(), 1);

        var questionScores = await (
            from s in db.Scores.AsNoTracking()
            join ev in db.Evaluations.AsNoTracking() on s.EvaluationId equals ev.Id
            join q in db.Questions.AsNoTracking() on s.QuestionId equals q.Id
            join d in db.EvaluationDomains.AsNoTracking() on q.DomainId equals d.Id
            where ev.PeriodId == periodId
            select new { s.QuestionId, q.Title, Domain = d.Title, s.Score, s.MaxScore })
            .ToListAsync();

        var questionAverages = questionScores.GroupBy(x => new { x.QuestionId, x.Title, x.Domain })
            .Select(g => new QuestionReportRow(g.Key.Title, g.Key.Domain, Math.Round(g.Average(x => x.MaxScore == 0 ? 0 : x.Score * 100 / x.MaxScore), 1), g.Count()))
            .OrderByDescending(x => x.AveragePercentage).ToList();

        var domainAverages = questionScores.GroupBy(x => x.Domain)
            .Select(g => new DomainReportRow(g.Key, Math.Round(g.Average(x => x.MaxScore == 0 ? 0 : x.Score * 100 / x.MaxScore), 1), g.Count()))
            .OrderByDescending(x => x.AveragePercentage).ToList();

        var unitAverages = evaluated.GroupBy(x => x.Unit)
            .Select(g => new UnitReportRow(g.Key, g.Count(), Math.Round(g.Average(x => Percent(x.Score, x.Max)), 1)))
            .OrderByDescending(x => x.AveragePercentage).ToList();

        var tops = evaluated.OrderByDescending(x => Percent(x.Score, x.Max)).Take(10)
            .Select(x => new PersonReportRow(x.Employee, x.Unit, Percent(x.Score, x.Max))).ToList();
        var lows = evaluated.OrderBy(x => Percent(x.Score, x.Max)).Take(10)
            .Select(x => new PersonReportRow(x.Employee, x.Unit, Percent(x.Score, x.Max))).ToList();

        var distribution = new DistributionReport(
            evaluated.Count(x => Percent(x.Score, x.Max) >= 90),
            evaluated.Count(x => Percent(x.Score, x.Max) >= 75 && Percent(x.Score, x.Max) < 90),
            evaluated.Count(x => Percent(x.Score, x.Max) >= 60 && Percent(x.Score, x.Max) < 75),
            evaluated.Count(x => Percent(x.Score, x.Max) < 60));

        var activeEmployees = await db.Employees.CountAsync(x => x.IsActive);
        var completion = activeEmployees == 0 ? 0 : Math.Round(evaluated.Count * 100m / activeEmployees, 1);
        return new ReportData(period, rows.Count, evaluated.Count, avg, completion, tops, lows, questionAverages, domainAverages, unitAverages, distribution);
    }

    private Task<List<Row>> QueryRows(int periodId) =>
        (
            from ev in db.Evaluations.AsNoTracking()
            join employee in db.Employees.AsNoTracking() on ev.EmployeeId equals employee.Id
            join evaluator in db.Employees.AsNoTracking() on ev.EvaluatorId equals evaluator.Id
            join unit in db.OrgUnits.AsNoTracking() on employee.UnitId equals unit.Id
            join position in db.Positions.AsNoTracking() on employee.PositionId equals position.Id
            where ev.PeriodId == periodId
            select new Row(employee.FullName, evaluator.FullName, unit.Title, position.Title, ev.FinalScore, ev.FinalMaxScore, ev.Status.ToString())
        ).OrderBy(x => x.Employee).ToListAsync();

    private static decimal Percent(decimal score, decimal max) => max <= 0 ? 0 : Math.Round(score * 100 / max, 1);
    private static ReportData EmptyReport() => new(null, 0, 0, 0, 0, [], [], [], [], [], new DistributionReport(0, 0, 0, 0));

    public record ReportVm(List<EvaluationPeriod> Periods, int? SelectedPeriodId, ReportData Data, List<Row> Rows);
    public record ReportData(EvaluationPeriod? Period, int RowCount, int EvaluatedCount, decimal AveragePercentage, decimal CompletionPercentage, List<PersonReportRow> TopPerformers, List<PersonReportRow> ImprovementPeople, List<QuestionReportRow> QuestionAverages, List<DomainReportRow> DomainAverages, List<UnitReportRow> UnitAverages, DistributionReport Distribution);
    public record Row(string Employee, string Evaluator, string Unit, string Position, decimal Score, decimal Max, string Status);
    public record PersonReportRow(string Name, string Unit, decimal Percentage);
    public record QuestionReportRow(string Title, string Domain, decimal AveragePercentage, int Responses);
    public record DomainReportRow(string Domain, decimal AveragePercentage, int Responses);
    public record UnitReportRow(string Unit, int EvaluatedCount, decimal AveragePercentage);
    public record DistributionReport(int Excellent, int Good, int Moderate, int NeedsImprovement);
}