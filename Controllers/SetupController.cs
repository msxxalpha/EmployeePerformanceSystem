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
        View(new PositionsVm(
            await db.Positions
                .Include(x => x.QuestionMappings)
                .ThenInclude(x => x.Question!)
                .ThenInclude(x => x.EvaluationDomain)
                .OrderBy(x => x.Title)
                .ToListAsync(),
            await db.Questions
                .Include(x => x.EvaluationDomain)
                .Where(x => x.IsActive)
                .OrderBy(x => x.Title)
                .AsNoTracking()
                .ToListAsync(),
            AutoCodeGenerator.Next(await db.Positions.AsNoTracking().Select(x => x.Code).ToListAsync(), "POS")));

    public async Task<IActionResult> Questions(string? q, int page = 1, int pageSize = 25) 
    {
        pageSize = Paging.NormalizePageSize(pageSize);
        q = (q ?? "").Trim();

        var query = db.Questions
            .Include(x => x.EvaluationDomain)
            .Include(x => x.PositionMappings).ThenInclude(x => x.Position)
            .AsQueryable();

        if (q != "")
            query = query.Where(x => x.Code.Contains(q) || x.Title.Contains(q) || x.Text.Contains(q) || x.EvaluationDomain!.Title.Contains(q));

        var total = await query.CountAsync();
        var totalPages = Paging.TotalPages(total, pageSize);
        page = Paging.NormalizePage(page, totalPages);

        var rows = await query
            .OrderBy(x => x.EvaluationDomain!.SortOrder).ThenBy(x => x.Title)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return View(new QuestionsVm(
            rows,
            await db.Positions.Where(x => x.IsActive).OrderBy(x => x.Title).ToListAsync(),
            await db.EvaluationDomains.Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToListAsync(),
            total, page, pageSize, q,
            AutoCodeGenerator.Next(await db.Questions.AsNoTracking().Select(x => x.Code).ToListAsync(), "Q")));
    }

    [HttpGet]
    public async Task<IActionResult> Domains(string? q, int page = 1, int pageSize = 25)
    {
        pageSize = Paging.NormalizePageSize(pageSize);
        q = (q ?? "").Trim();
        var query = db.EvaluationDomains.AsQueryable();

        if (q != "")
            query = query.Where(x => x.Code.Contains(q) || x.Title.Contains(q) || (x.Description ?? "").Contains(q));

        var total = await query.CountAsync();
        var totalPages = Paging.TotalPages(total, pageSize);
        page = Paging.NormalizePage(page, totalPages);
        var rows = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Title)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return View(new DomainsVm(
            rows, total, page, pageSize, q,
            AutoCodeGenerator.Next(await db.EvaluationDomains.AsNoTracking().Select(x => x.Code).ToListAsync(), "DOM")));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDomain(string code, string title, string? description, int sortOrder = 1)
    {
        code = code?.Trim() ?? "";
        title = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(code))
            code = AutoCodeGenerator.Next(await db.EvaluationDomains.AsNoTracking().Select(x => x.Code).ToListAsync(), "DOM");
        if (string.IsNullOrWhiteSpace(title))
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
        if (string.IsNullOrWhiteSpace(code))
            code = AutoCodeGenerator.Next(await db.Positions.AsNoTracking().Select(x => x.Code).ToListAsync(), "POS");

        if (title == "")
            TempData["Error"] = "عنوان رده پستی الزامی است.";
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
        if (string.IsNullOrWhiteSpace(code))
            code = AutoCodeGenerator.Next(await db.Questions.AsNoTracking().Select(x => x.Code).ToListAsync(), "Q");

        if (title == "" || domainId <= 0)
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
            {
                db.PositionQuestions.Add(new PositionQuestion
                {
                    QuestionId = questionId,
                    PositionId = positionId,
                    MaxScore = maxScore,
                    SortOrder = Math.Max(1, sortOrder),
                    IsActive = true
                });
            }
            else
            {
                map.MaxScore = maxScore;
                map.SortOrder = Math.Max(1, sortOrder);
                map.IsActive = true;
            }

            await db.SaveChangesAsync();
            await RecalculatePositionMaxScore(positionId);
            TempData["Result"] = "سؤال با موفقیت به رده پستی اضافه یا مجدداً فعال شد.";
        }

        return RedirectToAction(nameof(Positions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuestionMapping(int mappingId, decimal maxScore, int sortOrder = 1)
    {
        var map = await db.PositionQuestions.SingleOrDefaultAsync(x => x.Id == mappingId);
        if (map == null) return NotFound();

        if (maxScore <= 0)
        {
            TempData["Error"] = "سقف امتیاز باید بیشتر از صفر باشد.";
            return RedirectToAction(nameof(Positions));
        }

        if (!await db.Questions.AnyAsync(x => x.Id == map.QuestionId && x.IsActive) ||
            !await db.Positions.AnyAsync(x => x.Id == map.PositionId && x.IsActive))
        {
            TempData["Error"] = "سؤال یا رده پستی غیرفعال است.";
            return RedirectToAction(nameof(Positions));
        }

        map.MaxScore = maxScore;
        map.SortOrder = Math.Max(1, sortOrder);
        map.IsActive = true;
        await db.SaveChangesAsync();
        await RecalculatePositionMaxScore(map.PositionId);
        TempData["Result"] = "سقف امتیاز و ترتیب سؤال در رده پستی به‌روزرسانی شد.";
        return RedirectToAction(nameof(Positions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveQuestion(int mappingId)
    {
        var map = await db.PositionQuestions.SingleOrDefaultAsync(x => x.Id == mappingId);
        if (map == null)
            TempData["Error"] = "اتصال سؤال پیدا نشد.";
        else
        {
            map.IsActive = false;
            await db.SaveChangesAsync();
            await RecalculatePositionMaxScore(map.PositionId);
            TempData["Result"] = "اتصال سؤال از این رده پستی حذف شد.";
        }

        return RedirectToAction(nameof(Positions));
    }

    private async Task RecalculatePositionMaxScore(int positionId)
    {
        var position = await db.Positions.FindAsync(positionId);
        if (position == null) return;

        position.MaxScore = await db.PositionQuestions
            .Where(x => x.PositionId == positionId && x.IsActive &&
                        x.Question != null && x.Question.IsActive)
            .SumAsync(x => x.MaxScore);

        await db.SaveChangesAsync();
    }

    public async Task<IActionResult> OrgUnits(string? q, int page = 1, int pageSize = 25)
    {
        pageSize = Paging.NormalizePageSize(pageSize);
        q = (q ?? "").Trim();
        var query = db.OrgUnits.AsQueryable();
        if (q != "")
            query = query.Where(x => x.Code.Contains(q) || x.Title.Contains(q));

        var total = await query.CountAsync();
        var totalPages = Paging.TotalPages(total, pageSize);
        page = Paging.NormalizePage(page, totalPages);

        var rows = await query.OrderBy(x => x.Title)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return View(new OrgUnitsVm(
            rows,
            await db.OrgUnits.Where(x => x.IsActive).OrderBy(x => x.Title).AsNoTracking().ToListAsync(),
            total, page, pageSize, q,
            AutoCodeGenerator.Next(await db.OrgUnits.AsNoTracking().Select(x => x.Code).ToListAsync(), "ORG")));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOrgUnit(string code, string title, int? parentId)
    {
        code = code?.Trim() ?? "";
        title = title?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(code))
            code = AutoCodeGenerator.Next(await db.OrgUnits.AsNoTracking().Select(x => x.Code).ToListAsync(), "ORG");

        if (title == "")
            TempData["Error"] = "عنوان واحد سازمانی الزامی است.";
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

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportOrgUnits(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "فایل Excel انتخاب نشده است.";
            return RedirectToAction(nameof(OrgUnits));
        }

        try
        {
            var rows = excel.ReadOrgUnits(file.OpenReadStream()).ToList();
            var duplicateCodes = rows.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1).Select(g => g.Key).ToList();

            if (duplicateCodes.Count > 0)
            {
                TempData["Error"] = "کد واحدهای تکراری در فایل: " + string.Join("، ", duplicateCodes);
                return RedirectToAction(nameof(OrgUnits));
            }

            if (rows.Any(x => string.IsNullOrWhiteSpace(x.Code) || string.IsNullOrWhiteSpace(x.Title)))
            {
                TempData["Error"] = "کد و عنوان تمام واحدها در فایل باید تکمیل باشد.";
                return RedirectToAction(nameof(OrgUnits));
            }

            var all = await db.OrgUnits.ToListAsync();
            foreach (var r in rows)
            {
                var existing = all.SingleOrDefault(x => x.Code.Equals(r.Code, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    existing = new OrgUnit { Code = r.Code.Trim(), Title = r.Title.Trim(), IsActive = true };
                    db.OrgUnits.Add(existing);
                    all.Add(existing);
                }
                else
                {
                    existing.Title = r.Title.Trim();
                    existing.IsActive = true;
                }
            }

            await db.SaveChangesAsync();
            all = await db.OrgUnits.ToListAsync();

            var byCode = all.ToDictionary(x => x.Code.Trim(), StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows)
            {
                var node = byCode[r.Code.Trim()];
                if (string.IsNullOrWhiteSpace(r.ParentCode))
                    node.ParentId = null;
                else if (!byCode.TryGetValue(r.ParentCode.Trim(), out var parent) || parent.Id == node.Id)
                {
                    TempData["Error"] = $"والد واحد «{r.Code}» معتبر نیست: «{r.ParentCode}».";
                    return RedirectToAction(nameof(OrgUnits));
                }
                else
                    node.ParentId = parent.Id;
            }

            var cycleIds = FindOrgCycles(all);
            if (cycleIds.Count > 0)
            {
                TempData["Error"] = "فایل باعث ایجاد حلقه در ساختار سازمانی می‌شود. واحدهای درگیر: " +
                                    string.Join("، ", all.Where(x => cycleIds.Contains(x.Id)).Select(x => x.Code));
                return RedirectToAction(nameof(OrgUnits));
            }

            await db.SaveChangesAsync();
            TempData["Result"] = $"{rows.Count} واحد سازمانی از Excel وارد/به‌روزرسانی شد.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "فایل ساختار سازمانی قابل پردازش نیست: " + ex.Message;
        }

        return RedirectToAction(nameof(OrgUnits));
    }

    private static HashSet<int> FindOrgCycles(List<OrgUnit> units)
    {
        var map = units.ToDictionary(x => x.Id, x => x.ParentId);
        var cyclic = new HashSet<int>();

        foreach (var start in map.Keys)
        {
            var seen = new Dictionary<int, int>();
            var current = start;
            while (map.ContainsKey(current))
            {
                if (seen.TryGetValue(current, out var index))
                {
                    foreach (var id in seen.Where(x => x.Value >= index).Select(x => x.Key))
                        cyclic.Add(id);
                    break;
                }

                seen[current] = seen.Count;
                var parent = map[current];
                if (!parent.HasValue) break;
                current = parent.Value;
            }
        }

        return cyclic;
    }

    [HttpGet] public async Task<IActionResult> ExportPositions(){var rows=await db.Positions.Include(x=>x.QuestionMappings).AsNoTracking().OrderBy(x=>x.Title).ToListAsync();return File(excel.Positions(rows),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","Positions.xlsx");}
    [HttpGet] public async Task<IActionResult> ExportQuestions(){var rows=await db.Questions.Include(x=>x.PositionMappings).AsNoTracking().OrderBy(x=>x.EvaluationDomain!.SortOrder).ThenBy(x=>x.Title).ToListAsync();return File(excel.Questions(rows),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","Questions.xlsx");}
    [HttpGet] public async Task<IActionResult> ExportOrgUnits(){var rows=await db.OrgUnits.AsNoTracking().OrderBy(x=>x.Title).ToListAsync();return File(excel.OrgUnits(rows),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","OrgUnits.xlsx");}

    public record QuestionsVm(
        List<Question> Questions,
        List<Position> Positions,
        List<EvaluationDomain> Domains,
        int TotalCount,
        int Page,
        int PageSize,
        string Search,
        string NextCode);
    public record DomainsVm(List<EvaluationDomain> Rows, int TotalCount, int Page, int PageSize, string Search, string NextCode);
    public record PositionsVm(List<Position> Positions, List<Question> Questions, string NextCode);
    public record OrgUnitsVm(List<OrgUnit> Units, List<OrgUnit> ParentOptions, int TotalCount, int Page, int PageSize, string Search, string NextCode);
}