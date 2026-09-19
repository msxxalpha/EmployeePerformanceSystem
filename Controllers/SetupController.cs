using Indamin.Performance.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Indamin.Performance.Services;

namespace Indamin.Performance.Controllers;

[Authorize(Policy = "AdminOnly")]
public class SetupController(AppDbContext db, ExcelService excel) : Controller
{
    public async Task<IActionResult> Positions() =>
        View(await db.Positions.Include(x => x.QuestionMappings).ThenInclude(x => x.Question!).ThenInclude(x => x.EvaluationDomain).OrderBy(x => x.Title).ToListAsync());

    public async Task<IActionResult> Questions() =>
        View(new QuestionsVm(
            await db.Questions.Include(x => x.EvaluationDomain).Include(x => x.PositionMappings).ThenInclude(x => x.Position).OrderBy(x => x.EvaluationDomain!.SortOrder).ThenBy(x => x.Title).ToListAsync(),
            await db.Positions.Where(x => x.IsActive).OrderBy(x => x.Title).ToListAsync(),
            await db.EvaluationDomains.Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToListAsync()));

    [HttpGet]
    public async Task<IActionResult> Domains() =>
        View(await db.EvaluationDomains.OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDomain(string code, string title, string? description, int sortOrder = 1)
    {
        code = code?.Trim() ?? "";
        title = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title))
            TempData["Error"] = "کد و عنوان حوزه ارزیابی الزامی است.";
        else if (await db.EvaluationDomains.AnyAsync(x => x.Code == code))
            TempData["Error"] = "کد حوزه ارزیابی تکراری است.";
        else
        {
            db.EvaluationDomains.Add(new EvaluationDomain
            {
                Code = code, Title = title, Description = description?.Trim(),
                SortOrder = Math.Max(1, sortOrder), IsActive = true
            });
            await db.SaveChangesAsync();
            TempData["Result"] = "حوزه ارزیابی با موفقیت ایجاد شد.";
        }
        return RedirectToAction(nameof(Domains));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDomain(int id, string code, string title, string? description, int sortOrder, bool isActive = true)
    {
        var d = await db.EvaluationDomains.FindAsync(id);
        if (d == null) return NotFound();
        code = code?.Trim() ?? ""; title = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title))
            TempData["Error"] = "کد و عنوان حوزه ارزیابی الزامی است.";
        else if (await db.EvaluationDomains.AnyAsync(x => x.Id != id && x.Code == code))
            TempData["Error"] = "کد حوزه ارزیابی تکراری است.";
        else if (!isActive && await db.Questions.AnyAsync(x => x.DomainId == id && x.IsActive))
            TempData["Error"] = "حوزه دارای سؤال فعال است و نمی‌توان آن را غیرفعال کرد.";
        else
        {
            d.Code = code; d.Title = title; d.Description = description?.Trim();
            d.SortOrder = Math.Max(1, sortOrder); d.IsActive = isActive;
            await db.SaveChangesAsync();
            TempData["Result"] = "حوزه ارزیابی به‌روزرسانی شد.";
        }
        return RedirectToAction(nameof(Domains));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDomain(int id)
    {
        var d = await db.EvaluationDomains.FindAsync(id);
        if (d == null) return NotFound();
        if (d.IsActive && await db.Questions.AnyAsync(x => x.DomainId == id && x.IsActive))
        {
            TempData["Error"] = "حوزه دارای سؤال فعال است و تا زمان تعیین تکلیف سؤال‌ها قابل غیرفعال‌سازی نیست.";
        }
        else
        {
            d.IsActive = !d.IsActive;
            await db.SaveChangesAsync();
            TempData["Result"] = d.IsActive ? "حوزه فعال شد." : "حوزه غیرفعال شد.";
        }
        return RedirectToAction(nameof(Domains));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPosition(string code, string title)
    {
        code = code?.Trim() ?? "";
        title = title?.Trim() ?? "";

        if (code == "" || title == "")
            TempData["Error"] = "کد و عنوان رده پستی الزامی است.";
        else if (await db.Positions.AnyAsync(x => x.Code == code))
            TempData["Error"] = "کد رده پستی تکراری است.";
        else
        {
            db.Positions.Add(new Position { Code = code, Title = title, MaxScore = 0 });
            await db.SaveChangesAsync();
            TempData["Result"] = "رده پستی با موفقیت ایجاد شد.";
        }

        return RedirectToAction(nameof(Positions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuestion(string code, string title, int domainId, string? description, string? text)
    {
        code = code?.Trim() ?? "";
        title = title?.Trim() ?? "";
        text = string.IsNullOrWhiteSpace(text) ? title : text.Trim();

        if (code == "" || title == "" || domainId <= 0)
            TempData["Error"] = "کد، عنوان و حوزه ارزیابی سؤال الزامی است.";
        else if (!await db.EvaluationDomains.AnyAsync(x => x.Id == domainId && x.IsActive))
            TempData["Error"] = "حوزه ارزیابی انتخاب‌شده معتبر یا فعال نیست.";
        else if (await db.Questions.AnyAsync(x => x.Code == code))
            TempData["Error"] = "کد سؤال تکراری است.";
        else
        {
            db.Questions.Add(new Question
            {
                Code = code,
                Title = title,
                Text = text,
                DomainId = domainId,
                Description = description?.Trim()
            });
            await db.SaveChangesAsync();
            TempData["Result"] = "سؤال با موفقیت ایجاد شد.";
        }

        return RedirectToAction(nameof(Questions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignQuestion(int questionId, int positionId, decimal maxScore, int sortOrder = 1)
    {
        var q = await db.Questions.FindAsync(questionId);
        var p = await db.Positions.FindAsync(positionId);

        if (q == null || p == null)
            TempData["Error"] = "سؤال یا رده پستی معتبر نیست.";
        else if (!q.IsActive || !p.IsActive)
            TempData["Error"] = "سؤال یا رده پستی غیرفعال است.";
        else if (maxScore <= 0)
            TempData["Error"] = "سقف امتیاز باید بیشتر از صفر باشد.";
        else
        {
            var map = await db.PositionQuestions.SingleOrDefaultAsync(x => x.QuestionId == questionId && x.PositionId == positionId);

            if (map == null)
                db.PositionQuestions.Add(new PositionQuestion
                {
                    QuestionId = questionId,
                    PositionId = positionId,
                    MaxScore = maxScore,
                    SortOrder = Math.Max(1, sortOrder)
                });
            else
            {
                map.MaxScore = maxScore;
                map.SortOrder = Math.Max(1, sortOrder);
                map.IsActive = true;
            }

            p.MaxScore = await db.PositionQuestions
                .Where(x => x.PositionId == positionId && x.IsActive)
                .SumAsync(x => x.MaxScore);

            await db.SaveChangesAsync();
            TempData["Result"] = "سؤال با موفقیت به رده پستی متصل شد.";
        }

        return RedirectToAction(nameof(Questions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveQuestion(int mappingId)
    {
        var map = await db.PositionQuestions.Include(x => x.Position)
            .SingleOrDefaultAsync(x => x.Id == mappingId);

        if (map == null)
            TempData["Error"] = "اتصال سؤال پیدا نشد.";
        else
        {
            map.IsActive = false;
            map.Position!.MaxScore = await db.PositionQuestions
                .Where(x => x.PositionId == map.PositionId && x.IsActive && x.Id != map.Id)
                .SumAsync(x => x.MaxScore);
            await db.SaveChangesAsync();
            TempData["Result"] = "اتصال سؤال غیرفعال شد.";
        }

        return RedirectToAction(nameof(Questions));
    }

    public async Task<IActionResult> OrgUnits() =>
        View(new OrgUnitsVm(
            await db.OrgUnits.OrderBy(x => x.Title).ToListAsync(),
            await db.OrgUnits.Where(x => x.IsActive).OrderBy(x => x.Title).ToListAsync()));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOrgUnit(string code, string title, int? parentId)
    {
        code = code?.Trim() ?? "";
        title = title?.Trim() ?? "";

        if (code == "" || title == "")
            TempData["Error"] = "کد و عنوان واحد سازمانی الزامی است.";
        else if (await db.OrgUnits.AnyAsync(x => x.Code == code))
            TempData["Error"] = "کد واحد سازمانی تکراری است.";
        else if (parentId.HasValue && parentId.Value == 0)
            parentId = null;
        else if (parentId.HasValue && !await db.OrgUnits.AnyAsync(x => x.Id == parentId.Value && x.IsActive))
            TempData["Error"] = "واحد والد معتبر نیست.";
        else
        {
            db.OrgUnits.Add(new OrgUnit { Code = code, Title = title, ParentId = parentId });
            await db.SaveChangesAsync();
            TempData["Result"] = "واحد سازمانی با موفقیت ایجاد شد.";
        }

        return RedirectToAction(nameof(OrgUnits));
    }

    [HttpGet] public async Task<IActionResult> ExportPositions(){var rows=await db.Positions.Include(x=>x.QuestionMappings).AsNoTracking().OrderBy(x=>x.Title).ToListAsync();return File(excel.Positions(rows),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","Positions.xlsx");}
    [HttpGet] public async Task<IActionResult> ExportQuestions(){var rows=await db.Questions.Include(x=>x.PositionMappings).AsNoTracking().OrderBy(x=>x.EvaluationDomain!.SortOrder).ThenBy(x=>x.Title).ToListAsync();return File(excel.Questions(rows),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","Questions.xlsx");}
    [HttpGet] public async Task<IActionResult> ExportOrgUnits(){var rows=await db.OrgUnits.AsNoTracking().OrderBy(x=>x.Title).ToListAsync();return File(excel.OrgUnits(rows),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","OrgUnits.xlsx");}

    public record QuestionsVm(List<Question> Questions, List<Position> Positions, List<EvaluationDomain> Domains);
    public record OrgUnitsVm(List<OrgUnit> Units, List<OrgUnit> ParentOptions);
}