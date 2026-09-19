using System.Security.Claims;
using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Indamin.Performance.Controllers;

[Authorize]
public class EvaluationController(AppDbContext db, PerformanceService ps, ExcelService excel) : Controller
{
    private const string ExcelMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    public async Task<IActionResult> Index(int? periodId)
    {
        if (!TryEvaluator(out var evaluatorId) || !await ps.IsEvaluator(evaluatorId)) return Forbid();
        await ps.SyncExpiredPeriodsAsync();

        var periods = await db.Periods.AsNoTracking().OrderByDescending(x => x.StartAt).ToListAsync();
        var active = periods.FirstOrDefault(x => x.IsOpen && x.StartAt <= DateTime.Now && x.EndAt >= DateTime.Now);

        var employees = await ps.GetSubordinates(evaluatorId);
        var employeeIds = employees.Select(x => x.Id).ToList();

        var relevantPeriodIds = employeeIds.Count == 0
            ? new HashSet<int>()
            : (await db.Evaluations.AsNoTracking()
                .Where(x => employeeIds.Contains(x.EmployeeId) && (x.EvaluatorId == evaluatorId || x.OriginalEvaluatorId == evaluatorId))
                .Select(x => x.PeriodId).Distinct().ToListAsync()).ToHashSet();

        var selectablePeriods = periods.Where(p =>
            (active != null && p.Id == active.Id) ||
            (p.StartAt <= DateTime.Now && p.EndAt < DateTime.Now && relevantPeriodIds.Contains(p.Id))).ToList();

        var selected = periodId.HasValue
            ? selectablePeriods.FirstOrDefault(x => x.Id == periodId.Value)
            : active ?? selectablePeriods.FirstOrDefault();

        List<Evaluation> evaluations = [];
        if (selected != null && employeeIds.Count > 0)
            evaluations = await db.Evaluations.AsNoTracking()
                .Where(x => x.PeriodId == selected.Id && employeeIds.Contains(x.EmployeeId))
                .Include(x => x.Scores)
                .ToListAsync();

        var evaluatorIds = evaluations.Select(x => x.EvaluatorId).Distinct().ToList();
        var evaluatorNames = evaluatorIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Employees.AsNoTracking().Where(x => evaluatorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName);

        var rows = employees.Select(e =>
        {
            var ev = evaluations.FirstOrDefault(x => x.EmployeeId == e.Id);
            var max = ev?.FinalMaxScore ?? 0;
            var score = ev?.FinalScore ?? 0;
            return new Row(
                e.Id, ev?.Id, e.FullName, e.PersonnelNo, e.Position?.Title ?? "—", e.Unit?.Title ?? "—",
                e.IsEvaluator,
                ev?.Status.ToString() ?? "ثبت نشده",
                score, max, Percent(score, max),
                ev == null ? "—" : evaluatorNames.GetValueOrDefault(ev.EvaluatorId, "—"),
                ev != null && ev.EvaluatorId != evaluatorId);
        }).Where(x => selected == null || selected.EndAt >= DateTime.Now || x.EvaluationId.HasValue).ToList();

        var questionIds = evaluations.SelectMany(x => x.Scores).Select(x => x.QuestionId).Distinct().ToList();
        var questionMap = questionIds.Count == 0 ? new Dictionary<int, Question>() : await db.Questions.AsNoTracking().Where(x => questionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var analytics = BuildAnalytics(evaluations, employees, questionMap);
        var periodOptions = selectablePeriods.Select(p => new PeriodOptionVm(p.Id, p.Title, p.StartAt <= DateTime.Now && p.EndAt >= DateTime.Now && p.IsOpen, p.EndAt < DateTime.Now, relevantPeriodIds.Contains(p.Id))).ToList();

        var message = active == null
            ? "در حال حاضر دوره فعالی برای ثبت ارزیابی وجود ندارد. دوره‌های قبلی را انتخاب کنید تا سوابق و تحلیل عملکرد را مشاهده کنید."
            : null;

        return View(new DashboardVm(
            selected, active, periods, periodOptions, rows, analytics.TopPerformers, analytics.Improvements,
            analytics.QuestionAverages, message));
    }

    [HttpGet]
    public async Task<IActionResult> Form(int id, int? periodId = null)
    {
        if (!TryEvaluator(out var actor) || !await ps.IsEvaluator(actor)) return Forbid();
        await ps.SyncExpiredPeriodsAsync();

        var period = periodId.HasValue
            ? await db.Periods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == periodId.Value)
            : await ps.CurrentPeriod();

        if (period == null)
            return NotFound("دوره‌ای انتخاب یا فعال نشده است.");

        var employee = await db.Employees.AsNoTracking().Include(x => x.Position).SingleOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (employee == null || !await ps.CanEvaluate(actor, id)) return Forbid();

        var ev = await db.Evaluations.Include(x => x.Scores).SingleOrDefaultAsync(x => x.PeriodId == period.Id && x.EmployeeId == id);
        var qs = await QuestionList(employee.PositionId);
        var history = ev == null ? [] : await History(ev.Id);
        var canEdit = period.StartAt <= DateTime.Now && period.EndAt >= DateTime.Now && period.IsOpen && (ev == null || await ps.CanReviewEvaluation(actor, ev));
        var notice = period.EndAt < DateTime.Now || !period.IsOpen
            ? "این دوره بسته است؛ اطلاعات فقط قابل مشاهده است."
            : (ev != null && !await ps.CanReviewEvaluation(actor, ev) ? "این ارزیابی توسط ارزیاب بالادست بازنگری شده و شما فقط مجاز به مشاهده هستید." : null);

        var viewQs = qs.Select(q => q with
        {
            Score = ev?.Scores.FirstOrDefault(s => s.QuestionId == q.QuestionId)?.Score ?? 0,
            Comment = ev?.Scores.FirstOrDefault(s => s.QuestionId == q.QuestionId)?.Comment
        }).ToList();

        return View(new FormVm(period, employee, viewQs, ev, canEdit, notice, history, ev?.FinalScore ?? 0, ev?.FinalMaxScore ?? qs.Sum(x => x.MaxScore)));
    }

    [HttpGet]
    public async Task<IActionResult> Review(int evaluationId)
    {
        if (!TryEvaluator(out var actor) || !await ps.IsEvaluator(actor)) return Forbid();
        await ps.SyncExpiredPeriodsAsync();

        var ev = await db.Evaluations.Include(x => x.Scores).Include(x => x.Period).SingleOrDefaultAsync(x => x.Id == evaluationId);
        if (ev == null) return NotFound();
        var employee = await db.Employees.AsNoTracking().Include(x => x.Position).SingleOrDefaultAsync(x => x.Id == ev.EmployeeId && x.IsActive);
        if (employee == null || !await ps.CanEvaluate(actor, employee.Id) || !await ps.CanReviewEvaluation(actor, ev)) return Forbid();

        var qs = await QuestionList(employee.PositionId);
        var history = await History(ev.Id);
        var canEdit = ev.Period != null && ev.Period.IsOpen && ev.Period.StartAt <= DateTime.Now && ev.Period.EndAt >= DateTime.Now;
        var viewQs = qs.Select(q => q with
        {
            Score = ev.Scores.FirstOrDefault(s => s.QuestionId == q.QuestionId)?.Score ?? 0,
            Comment = ev.Scores.FirstOrDefault(s => s.QuestionId == q.QuestionId)?.Comment
        }).ToList();

        return View("Form", new FormVm(ev.Period!, employee, viewQs, ev, canEdit, canEdit ? "این ارزیابی در دوره فعال قابل بازنگری است." : "این دوره بسته است؛ اطلاعات فقط قابل مشاهده است.", history, ev.FinalScore, ev.FinalMaxScore));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Form(FormPost m)
    {
        if (!TryEvaluator(out var actor) || !await ps.IsEvaluator(actor)) return Forbid();

        var period = await ps.CurrentPeriod();
        if (period == null)
        {
            TempData["Error"] = "در حال حاضر دوره فعالی برای ثبت یا ویرایش ارزیابی وجود ندارد.";
            return RedirectToAction(nameof(Index));
        }

        var employee = await db.Employees.Include(x => x.Position).SingleOrDefaultAsync(x => x.Id == m.EmployeeId && x.IsActive);
        if (employee == null || !await ps.CanEvaluate(actor, employee.Id)) return Forbid();

        var ev = await db.Evaluations.Include(x => x.Scores).SingleOrDefaultAsync(x => x.PeriodId == period.Id && x.EmployeeId == employee.Id);
        var isNew = ev == null;

        if (!isNew && !await ps.CanReviewEvaluation(actor, ev!)) return Forbid();

        var qs = await QuestionList(employee.PositionId);
        if (isNew)
        {
            ev = new Evaluation { PeriodId = period.Id, EmployeeId = employee.Id, EvaluatorId = actor, OriginalEvaluatorId = actor, Status = EvaluationStatus.Draft };
            foreach (var q in qs)
            {
                var score = m.Scores.GetValueOrDefault(q.QuestionId);
                ValidateScore(q, score, m.Scores, ModelState);
                ev.Scores.Add(new EvaluationScore { QuestionId = q.QuestionId, Score = score, MaxScore = q.MaxScore, Comment = m.Comments.GetValueOrDefault(q.QuestionId)?.Trim() });
            }
            db.Evaluations.Add(ev);
        }
        else
        {
            foreach (var q in qs)
            {
                var old = ev!.Scores.FirstOrDefault(x => x.QuestionId == q.QuestionId);
                var newScore = m.Scores.GetValueOrDefault(q.QuestionId, old?.Score ?? 0);
                if (newScore < 0 || newScore > (old?.MaxScore ?? q.MaxScore))
                    ModelState.AddModelError($"Scores[{q.QuestionId}]", $"امتیاز «{q.Title}» باید بین صفر و {(old?.MaxScore ?? q.MaxScore):0.##} باشد.");

                var comment = m.Comments.GetValueOrDefault(q.QuestionId)?.Trim();
                if (old == null)
                    ev.Scores.Add(new EvaluationScore { QuestionId = q.QuestionId, Score = newScore, MaxScore = q.MaxScore, Comment = comment, UpdatedAt = DateTime.UtcNow });
                else if (old.Score != newScore || old.Comment != comment)
                {
                    db.ScoreHistory.Add(new EvaluationScoreHistory
                    {
                        EvaluationId = ev.Id, QuestionId = q.QuestionId, OldScore = old.Score, NewScore = newScore,
                        ChangedBy = actor, Reason = ev.EvaluatorId == actor ? "اصلاح ارزیابی" : "بازنگری ارزیاب بالادست"
                    });
                    old.Score = newScore; old.Comment = comment; old.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        if (!ModelState.IsValid)
        {
            var posted = qs.Select(q => q with { Score = m.Scores.GetValueOrDefault(q.QuestionId), Comment = m.Comments.GetValueOrDefault(q.QuestionId) }).ToList();
            return View(new FormVm(period, employee, posted, ev, true, "برخی امتیازها نامعتبر هستند؛ سقف هر سؤال را رعایت کنید.", ev?.Id == 0 ? [] : await History(ev!.Id), ev?.Scores.Sum(x => x.Score) ?? 0, ev?.Scores.Sum(x => x.MaxScore) ?? 0));
        }

        if (ev!.EvaluatorId != actor)
        {
            db.EvaluatorHistory.Add(new EvaluatorChangeHistory
            {
                EvaluationId = ev.Id, PreviousEvaluatorId = ev.EvaluatorId, NewEvaluatorId = actor, ChangedBy = actor,
                Reason = "بازنگری ارزیاب بالادست"
            });
            ev.EvaluatorId = actor;
        }

        ev.FinalScore = ev.Scores.Sum(x => x.Score);
        ev.FinalMaxScore = ev.Scores.Sum(x => x.MaxScore);
        ev.Status = EvaluationStatus.Submitted;
        ev.UpdatedAt = DateTime.UtcNow;

        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : (int?)null;
        db.AuditLogs.Add(new AuditLog
        {
            Action = isNew ? "EvaluationCreated" : "EvaluationUpdated", Entity = "Evaluation", EntityId = ev.Id.ToString(),
            Details = $"EmployeeId={employee.Id};EvaluatorId={actor};FinalScore={ev.FinalScore};FinalMaxScore={ev.FinalMaxScore}", UserId = userId
        });

        await db.SaveChangesAsync();
        TempData["Result"] = "ارزیابی با موفقیت ثبت و امتیاز نهایی محاسبه شد.";
        return RedirectToAction(nameof(Index), new { periodId = period.Id });
    }

    [HttpGet]
    public async Task<IActionResult> ExportMyList(int? periodId)
    {
        if (!TryEvaluator(out var eid) || !await ps.IsEvaluator(eid)) return Forbid();
        var employees = await ps.GetSubordinates(eid);
        var ids = employees.Select(x => x.Id).ToList();
        var period = periodId.HasValue ? await db.Periods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == periodId.Value) : await ps.CurrentPeriod();
        var evs = period == null || ids.Count == 0 ? [] : await db.Evaluations.AsNoTracking().Where(x => x.PeriodId == period.Id && ids.Contains(x.EmployeeId)).Include(x => x.Period).ToListAsync();
        var rows = employees.Select(e => { var ev = evs.FirstOrDefault(x => x.EmployeeId == e.Id); return (e.FullName, "—", e.Unit?.Title ?? "—", e.Position?.Title ?? "—", ev?.FinalScore ?? 0, ev?.FinalMaxScore ?? 0, ev?.Status.ToString() ?? "ثبت نشده"); });
        return File(excel.Evaluations(rows), ExcelMime, "MyEvaluationDashboard.xlsx");
    }

    private async Task<List<EvalQuestionVm>> QuestionList(int positionId) =>
        await db.PositionQuestions.AsNoTracking()
            .Where(x => x.PositionId == positionId && x.IsActive && x.Question != null && x.Question.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new EvalQuestionVm(x.QuestionId, x.Question!.Code, x.Question.Title, x.Question.Domain, x.Question.Description, x.MaxScore, x.SortOrder, 0, null))
            .ToListAsync();

    private async Task<List<HistoryRow>> History(int id) =>
        await db.ScoreHistory.AsNoTracking()
            .Where(x => x.EvaluationId == id)
            .Join(db.Questions, h => h.QuestionId, q => q.Id, (h, q) => new HistoryRow(q.Title, h.OldScore, h.NewScore, h.ChangedBy, h.ChangedAt, h.Reason))
            .OrderByDescending(x => x.ChangedAt).ToListAsync();

    private static decimal Percent(decimal score, decimal max) => max <= 0 ? 0 : Math.Round(score * 100 / max, 1);

    private static void ValidateScore(EvalQuestionVm q, decimal score, Dictionary<int, decimal> _, ModelStateDictionary state)
    {
        if (score < 0 || score > q.MaxScore)
            state.AddModelError($"Scores[{q.QuestionId}]", $"امتیاز «{q.Title}» باید بین صفر و {q.MaxScore:0.##} باشد.");
    }

    private bool TryEvaluator(out int id)
    {
        return int.TryParse(User.FindFirstValue("EmployeeId"), out id) && id > 0;
    }

    private static Analytics BuildAnalytics(List<Evaluation> evaluations, List<Employee> employees, Dictionary<int, Question> questionMap)
    {
        var evaluated = evaluations.Where(x => x.FinalMaxScore > 0).Select(x => new PerformanceRow(x.EmployeeId, employees.FirstOrDefault(e => e.Id == x.EmployeeId)?.FullName ?? "—", Percent(x.FinalScore, x.FinalMaxScore))).OrderByDescending(x => x.Percentage).ToList();
        var tops = evaluated.Take(5).ToList();
        var lows = evaluated.OrderBy(x => x.Percentage).Take(5).ToList();

        var questionAverages = evaluations.SelectMany(e => e.Scores)
            .GroupBy(s => s.QuestionId)
            .Select(g => new QuestionAverageVm(g.Key, questionMap.GetValueOrDefault(g.Key)?.Title ?? ("سؤال " + g.Key), Math.Round(g.Average(s => s.MaxScore == 0 ? 0 : s.Score * 100 / s.MaxScore), 1), g.Count()))
            .OrderByDescending(x => x.AveragePercentage).ToList();

        return new Analytics(tops, lows, questionAverages);
    }

    public record DashboardVm(EvaluationPeriod? SelectedPeriod, EvaluationPeriod? ActivePeriod, List<EvaluationPeriod> Periods, List<PeriodOptionVm> PeriodOptions, List<Row> Employees, List<PerformanceRow> TopPerformers, List<PerformanceRow> Improvements, List<QuestionAverageVm> QuestionAverages, string? Message);
    public record PeriodOptionVm(int Id, string Title, bool IsActive, bool IsClosed, bool HasEvaluations);
    public record Row(int Id,int? EvaluationId,string Name,string PersonnelNo,string Position,string Unit,bool IsEvaluator,string Status,decimal Total,decimal Max,decimal Percentage,string CurrentEvaluator,bool IsTakeover);
    public record PerformanceRow(int EmployeeId,string Name,decimal Percentage);
    public record QuestionAverageVm(int QuestionId,string Label,decimal AveragePercentage,int Responses);
    public record Analytics(List<PerformanceRow> TopPerformers,List<PerformanceRow> Improvements,List<QuestionAverageVm> QuestionAverages);
    public record EvalQuestionVm(int QuestionId,string Code,string Title,string Domain,string? Description,decimal MaxScore,int SortOrder,decimal Score,string? Comment);
    public record FormVm(EvaluationPeriod Period,Employee Employee,List<EvalQuestionVm> Questions,Evaluation? Evaluation,bool CanEdit,string? Notice,List<HistoryRow> History,decimal FinalScore,decimal FinalMaxScore);
    public record HistoryRow(string QuestionTitle,decimal OldScore,decimal NewScore,int ChangedBy,DateTime ChangedAt,string Reason);
    public class FormPost { public int EmployeeId{get;set;} public Dictionary<int,decimal> Scores{get;set;}=[]; public Dictionary<int,string> Comments{get;set;}=[]; }
}