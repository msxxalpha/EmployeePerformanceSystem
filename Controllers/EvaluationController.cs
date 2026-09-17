using System.Security.Claims;
using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;

[Authorize]
public class EvaluationController(AppDbContext db, PerformanceService ps, ExcelService excel) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (!int.TryParse(User.FindFirstValue("EmployeeId"), out var eid) || !await ps.IsEvaluator(eid)) return Forbid();
        var p = await ps.CurrentPeriod();
        if (p == null) return View(new List<Row>());
        var employees = await ps.GetSubordinates(eid);
        var ids = employees.Select(x => x.Id).ToList();
        var evaluations = await db.Evaluations.AsNoTracking().Where(x => x.PeriodId == p.Id && ids.Contains(x.EmployeeId)).ToListAsync();
        var evaluationIds = evaluations.Select(x => x.Id).ToList();
        var totals = await db.Scores.AsNoTracking().Where(x => evaluationIds.Contains(x.EvaluationId)).GroupBy(x => x.EvaluationId).Select(g => new { EvaluationId = g.Key, Total = g.Sum(x => x.Score) }).ToDictionaryAsync(x => x.EvaluationId, x => x.Total);
        var positionIds = employees.Select(e => e.PositionId).Distinct().ToList();
        var maxByPosition = await db.Questions.AsNoTracking().Where(x => x.IsActive && positionIds.Contains(x.PositionId)).GroupBy(x => x.PositionId).Select(g => new { PositionId = g.Key, Max = g.Sum(x => x.MaxScore) }).ToDictionaryAsync(x => x.PositionId, x => x.Max);
        var evaluatorIds = evaluations.Select(x => x.EvaluatorId).Distinct().ToList();
        var evaluatorNames = await db.Employees.AsNoTracking().Where(x => evaluatorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName);
        var rows = employees.Select(x =>
        {
            var ev = evaluations.FirstOrDefault(e => e.EmployeeId == x.Id);
            var max = maxByPosition.GetValueOrDefault(x.PositionId, 0m);
            var total = ev == null ? 0m : totals.GetValueOrDefault(ev.Id, 0m);
            var percentage = max > 0 ? Math.Round(total * 100m / max, 2) : 0m;
            return new Row(x.Id, x.FullName, x.PersonnelNo, x.Position?.Title ?? "", x.Unit?.Title ?? "", x.IsEvaluator, ev?.Status.ToString() ?? "ثبت نشده", total, max, percentage, ev == null ? "-" : evaluatorNames.GetValueOrDefault(ev.EvaluatorId, "-"));
        }).ToList();
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> ExportMyList()
    {
        if (!int.TryParse(User.FindFirstValue("EmployeeId"), out var eid) || !await ps.IsEvaluator(eid)) return Forbid();
        var rows = await ps.GetSubordinates(eid);
        return File(excel.Employees(rows), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "MySubordinates.xlsx");
    }

    public record Row(int Id, string Name, string PersonnelNo, string Position, string Unit, bool IsEvaluator, string Status, decimal Total, decimal Max, decimal Percentage, string CurrentEvaluator);

    [HttpGet]
    public async Task<IActionResult> Form(int id)
    {
        if (!int.TryParse(User.FindFirstValue("EmployeeId"), out var actor) || !await ps.IsEvaluator(actor)) return Forbid();
        var p = await ps.CurrentPeriod();
        if (p == null) return NotFound();
        var employee = await db.Employees.Include(x => x.Position).FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (employee == null || !await ps.CanEvaluate(actor, id)) return Forbid();
        var ev = await db.Evaluations.Include(x => x.Scores).SingleOrDefaultAsync(x => x.PeriodId == p.Id && x.EmployeeId == id);
        var questions = await Questions(employee.PositionId);
        var history = ev == null ? new List<HistoryRow>() : await History(ev.Id);
        if (ev != null && !await ps.CanReviewEvaluation(actor, ev))
            return View(new FormVm(p, employee, questions, ev, false, "این ارزیابی قبلاً توسط ارزیاب بالادست بازنگری شده و شما فقط مجاز به مشاهده آن هستید.", history));
        var canEdit = await ps.CanEdit(p) && (ev == null || await ps.CanReviewEvaluation(actor, ev));
        var note = !await ps.CanEdit(p) ? "بازه ارزیابی پایان یافته است؛ اطلاعات این دوره فقط قابل مشاهده است." : null;
        return View(new FormVm(p, employee, questions, ev, canEdit, note, history));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Form(FormPost m)
    {
        if (!int.TryParse(User.FindFirstValue("EmployeeId"), out var actor) || !await ps.IsEvaluator(actor)) return Forbid();
        var p = await ps.CurrentPeriod();
        if (p == null) return NotFound();
        if (!await ps.CanEdit(p)) return BadRequest("بازه ارزیابی پایان یافته است و امکان تغییر وجود ندارد.");
        var employee = await db.Employees.Include(x => x.Position).FirstOrDefaultAsync(x => x.Id == m.EmployeeId && x.IsActive);
        if (employee == null) return NotFound();
        if (!await ps.CanEvaluate(actor, employee.Id)) return Forbid();
        var ev = await db.Evaluations.Include(x => x.Scores).SingleOrDefaultAsync(x => x.PeriodId == p.Id && x.EmployeeId == employee.Id);
        var isNew = ev == null;
        if (isNew)
        {
            ev = new Evaluation { PeriodId = p.Id, EmployeeId = employee.Id, EvaluatorId = actor, OriginalEvaluatorId = actor, Status = EvaluationStatus.Draft };
            db.Evaluations.Add(ev);
        }
        else if (!await ps.CanReviewEvaluation(actor, ev!)) return Forbid();
        var qs = await Questions(employee.PositionId);
        foreach (var q in qs)
        {
            var score = m.Scores.TryGetValue(q.Id, out var suppliedScore) ? suppliedScore : 0m;
            if (score < 0 || score > q.MaxScore)
            {
                ModelState.AddModelError($"Scores[{q.Id}]", $"امتیاز سؤال «{q.Text}» باید بین صفر و {q.MaxScore} باشد.");
                continue;
            }
            var comment = m.Comments.TryGetValue(q.Id, out var c) ? c?.Trim() : null;
            var old = ev!.Scores.FirstOrDefault(x => x.QuestionId == q.Id);
            if (old == null)
                ev.Scores.Add(new EvaluationScore { QuestionId = q.Id, Score = score, Comment = comment });
            else if (old.Score != score || old.Comment != comment)
            {
                db.ScoreHistory.Add(new EvaluationScoreHistory { EvaluationId = ev.Id, QuestionId = q.Id, OldScore = old.Score, NewScore = score, ChangedBy = actor, Reason = ev.EvaluatorId == actor ? "اصلاح ارزیابی" : "بازنگری ارزیاب بالادست" });
                old.Score = score; old.Comment = comment; old.UpdatedAt = DateTime.UtcNow;
            }
        }
        if (!ModelState.IsValid)
        {
            var history = ev.Id > 0 ? await History(ev.Id) : new List<HistoryRow>();
            return View("Form", new FormVm(p, employee, qs, ev, true, "برخی امتیازها نامعتبر هستند؛ لطفاً موارد مشخص‌شده را اصلاح کنید.", history));
        }
        if (ev!.EvaluatorId != actor)
        {
            db.EvaluatorHistory.Add(new EvaluatorChangeHistory { EvaluationId = ev.Id, PreviousEvaluatorId = ev.EvaluatorId, NewEvaluatorId = actor, ChangedBy = actor, Reason = "بازنگری ارزیاب بالادست" });
            ev.EvaluatorId = actor;
        }
        ev.Status = EvaluationStatus.Submitted;
        ev.UpdatedAt = DateTime.UtcNow;
        int? appUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId) ? parsedUserId : null;
        db.AuditLogs.Add(new AuditLog { Action = isNew ? "EvaluationCreated" : "EvaluationUpdated", Entity = "Evaluation", EntityId = isNew ? "new" : ev.Id.ToString(), Details = $"EmployeeId={employee.Id};EvaluatorId={actor};Total={ev.Scores.Sum(x => x.Score)};Max={qs.Sum(x => x.MaxScore)}", UserId = appUserId });
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private Task<List<Question>> Questions(int positionId) => db.Questions.Where(x => x.PositionId == positionId && x.IsActive).OrderBy(x => x.SortOrder).ToListAsync();
    private Task<List<HistoryRow>> History(int evaluationId) => db.ScoreHistory.AsNoTracking().Where(x => x.EvaluationId == evaluationId).OrderByDescending(x => x.ChangedAt).Select(x => new HistoryRow(x.QuestionId, x.OldScore, x.NewScore, x.ChangedBy, x.ChangedAt, x.Reason)).ToListAsync();
    public record FormVm(EvaluationPeriod Period, Employee Employee, List<Question> Questions, Evaluation? Evaluation, bool CanEdit, string? Notice, List<HistoryRow> History);
    public record HistoryRow(int QuestionId, decimal OldScore, decimal NewScore, int ChangedBy, DateTime ChangedAt, string Reason);
    public class FormPost
    {
        public int EmployeeId { get; set; }
        public int PositionId { get; set; }
        public Dictionary<int, decimal> Scores { get; set; } = [];
        public Dictionary<int, string> Comments { get; set; } = [];
    }
}