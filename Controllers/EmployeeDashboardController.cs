using System.Security.Claims;
using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indamin.Performance.Controllers;

[Authorize]
public class EmployeeDashboardController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(int? periodId)
    {
        if (!int.TryParse(User.FindFirstValue("EmployeeId"), out var employeeId)) return Forbid();
        var employee = await db.Employees.AsNoTracking().Include(x => x.Position).Include(x => x.Unit).SingleOrDefaultAsync(x => x.Id == employeeId && x.IsActive);
        if (employee == null) return Forbid();
        var periods = await db.Periods.AsNoTracking().OrderByDescending(x => x.StartAt).ToListAsync();
        var evaluations = await db.Evaluations.AsNoTracking().Where(x => x.EmployeeId == employeeId).Include(x => x.Period).Include(x => x.Scores).ToListAsync();
        var questionIds = evaluations.SelectMany(x => x.Scores).Select(x => x.QuestionId).Distinct().ToList();
        var questions = await db.Questions.AsNoTracking().Where(x => questionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var rows = evaluations.OrderByDescending(x => x.Period!.StartAt).Select(x => new EvaluationSummaryVm(x.PeriodId, x.Period!.Title, PerformanceService.ToJalali(x.Period.StartAt), x.Scores.Sum(s => s.Score), x.Scores.Sum(s => questions.TryGetValue(s.QuestionId, out var q) ? q.MaxScore : 0), x.Status.ToString())).ToList();
        var selected = periodId.HasValue ? rows.FirstOrDefault(x => x.PeriodId == periodId.Value) : rows.FirstOrDefault();
        var detail = selected == null ? null : BuildDetail(evaluations.First(x => x.PeriodId == selected.PeriodId), questions);
        var trend = rows.AsEnumerable().Reverse().Select(x => new TrendVm(x.Title, x.Percentage)).ToList();
        var questionTrend = BuildQuestionTrend(evaluations, questions);
        var strengths = BuildAreas(evaluations, questions, true);
        var improvements = BuildAreas(evaluations, questions, false);
        return View(new DashboardVm(employee.FullName, employee.PersonnelNo, employee.Position?.Title ?? "", employee.Unit?.Title ?? "", rows, periods.Select(p => new PeriodOptionVm(p.Id, p.Title, rows.Any(r => r.PeriodId == p.Id))).ToList(), selected?.PeriodId, detail, trend, questionTrend, strengths, improvements));
    }

    private static DetailVm BuildDetail(Evaluation evaluation, Dictionary<int, Question> questions)
    {
        var items = evaluation.Scores.OrderBy(s => questions.TryGetValue(s.QuestionId, out var q) ? q.SortOrder : int.MaxValue).Select(s => { questions.TryGetValue(s.QuestionId, out var q); return new QuestionScoreVm(q?.Text ?? "سؤال حذف‌شده", s.Score, q?.MaxScore ?? 0, q?.MaxScore > 0 ? Math.Round(s.Score / q.MaxScore * 100, 1) : 0, s.Comment); }).ToList();
        var max = items.Sum(x => x.MaxScore); var total = items.Sum(x => x.Score);
        return new DetailVm(evaluation.Period!.Title, PerformanceService.ToJalali(evaluation.Period.StartAt), total, max, max > 0 ? Math.Round(total / max * 100, 1) : 0, items);
    }

    private static List<TrendVm> BuildQuestionTrend(List<Evaluation> evaluations, Dictionary<int, Question> questions)
    {
        return questions.Values.OrderBy(q => q.SortOrder).Select(q => new TrendVm(q.Text, Math.Round(evaluations.SelectMany(e => e.Scores.Where(s => s.QuestionId == q.Id)).Select(s => q.MaxScore == 0 ? 0 : s.Score / q.MaxScore * 100).DefaultIfEmpty(0).Average(), 1))).ToList();
    }

    private static List<AreaVm> BuildAreas(List<Evaluation> evaluations, Dictionary<int, Question> questions, bool strengths)
    {
        return questions.Values.OrderBy(q => q.SortOrder).Select(q => new AreaVm(q.Text, Math.Round(evaluations.SelectMany(e => e.Scores.Where(s => s.QuestionId == q.Id)).Select(s => q.MaxScore == 0 ? 0 : s.Score / q.MaxScore * 100).DefaultIfEmpty(0).Average(), 1))).Where(x => strengths ? x.Percentage >= 80 : x.Percentage < 60).OrderByDescending(x => x.Percentage).Take(5).ToList();
    }

    public record DashboardVm(string Name, string PersonnelNo, string Position, string Unit, List<EvaluationSummaryVm> Evaluations, List<PeriodOptionVm> Periods, int? SelectedPeriodId, DetailVm? SelectedDetail, List<TrendVm> Trend, List<TrendVm> QuestionTrend, List<AreaVm> Strengths, List<AreaVm> Improvements);
    public record EvaluationSummaryVm(int PeriodId, string Title, string Date, decimal Score, decimal MaxScore, string Status) { public decimal Percentage => MaxScore == 0 ? 0 : Math.Round(Score / MaxScore * 100, 1); }
    public record PeriodOptionVm(int Id, string Title, bool HasEvaluation);
    public record DetailVm(string Title, string Date, decimal Score, decimal MaxScore, decimal Percentage, List<QuestionScoreVm> Questions);
    public record QuestionScoreVm(string Text, decimal Score, decimal MaxScore, decimal Percentage, string? Comment);
    public record TrendVm(string Label, decimal Percentage);
    public record AreaVm(string Label, decimal Percentage);
}
