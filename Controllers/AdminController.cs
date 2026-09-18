using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indamin.Performance.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController(AppDbContext db, ExcelService excel, PerformanceService ps) : Controller
{
    private static readonly string ExcelMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<IActionResult> Periods()
    {
        await ps.SyncExpiredPeriodsAsync();
        var rows = await db.Periods.AsNoTracking().OrderByDescending(x => x.StartAt).ToListAsync();
        return View(rows);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePeriod(string title, string startJalali, string endJalali, string? description)
    {
        title = title?.Trim() ?? "";
        startJalali = startJalali?.Trim() ?? "";
        endJalali = endJalali?.Trim() ?? "";

        if (title == "" || startJalali == "" || endJalali == "")
        {
            TempData["Error"] = "عنوان و تاریخ‌های شروع و پایان دوره الزامی هستند.";
            return RedirectToAction(nameof(Periods));
        }

        try
        {
            var start = PerformanceService.Jalali(startJalali);
            var end = PerformanceService.Jalali(endJalali).Date.AddDays(1).AddTicks(-1);

            if (end < start)
                TempData["Error"] = "تاریخ پایان نمی‌تواند قبل از شروع باشد.";
            else if (await db.Periods.AnyAsync(x => x.StartAt <= end && x.EndAt >= start && x.IsOpen))
                TempData["Error"] = "بازه این دوره با یک دوره فعال دیگر هم‌پوشانی دارد.";
            else
            {
                db.Periods.Add(new EvaluationPeriod
                {
                    Title = title,
                    StartJalali = PerformanceService.ToJalali(start),
                    EndJalali = PerformanceService.ToJalali(end),
                    StartAt = start,
                    EndAt = end,
                    Description = description?.Trim(),
                    IsOpen = end >= DateTime.Now
                });
                await db.SaveChangesAsync();
                TempData["Result"] = "دوره ارزیابی با موفقیت ایجاد شد.";
            }
        }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }

        return RedirectToAction(nameof(Periods));
    }

    [HttpGet]
    public async Task<IActionResult> PeriodDetails(int id)
    {
        await ps.SyncExpiredPeriodsAsync();
        var period = await db.Periods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (period == null) return NotFound();

        var evaluations = await (
            from ev in db.Evaluations.AsNoTracking()
            join emp in db.Employees.AsNoTracking() on ev.EmployeeId equals emp.Id
            join evaluator in db.Employees.AsNoTracking() on ev.EvaluatorId equals evaluator.Id
            join pos in db.Positions.AsNoTracking() on emp.PositionId equals pos.Id
            join unit in db.OrgUnits.AsNoTracking() on emp.UnitId equals unit.Id
            where ev.PeriodId == id
            orderby emp.FullName
            select new PeriodEvaluationRow(
                ev.Id, emp.Id, emp.FullName, emp.PersonnelNo, pos.Title, unit.Title,
                evaluator.FullName, ev.FinalScore, ev.FinalMaxScore, ev.Status.ToString()))
            .ToListAsync();

        return View(new PeriodDetailsVm(period, evaluations));
    }

    [HttpGet]
    public async Task<IActionResult> EditPeriod(int id)
    {
        var p = await db.Periods.FindAsync(id);
        if (p == null) return NotFound();
        await ps.SyncExpiredPeriodsAsync();
        p = await db.Periods.FindAsync(id);
        if (p == null) return NotFound();
        return View(new PeriodEditVm(p.Id, p.Title, p.StartJalali, p.EndJalali, p.Description, p.IsOpen && p.EndAt >= DateTime.Now));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPeriod(PeriodEditVm model)
    {
        var p = await db.Periods.FindAsync(model.Id);
        if (p == null) return NotFound();

        try
        {
            var start = PerformanceService.Jalali(model.StartJalali.Trim());
            var end = PerformanceService.Jalali(model.EndJalali.Trim()).Date.AddDays(1).AddTicks(-1);

            if (string.IsNullOrWhiteSpace(model.Title) || end < start)
                TempData["Error"] = "عنوان و تاریخ‌های دوره را به‌درستی تکمیل کنید.";
            else if (await db.Periods.AnyAsync(x => x.Id != p.Id && x.StartAt <= end && x.EndAt >= start && x.IsOpen))
                TempData["Error"] = "بازه جدید با یک دوره فعال دیگر هم‌پوشانی دارد.";
            else
            {
                p.Title = model.Title.Trim();
                p.StartAt = start;
                p.EndAt = end;
                p.StartJalali = PerformanceService.ToJalali(start);
                p.EndJalali = PerformanceService.ToJalali(end);
                p.Description = model.Description?.Trim();
                p.IsOpen = model.IsOpen && end >= DateTime.Now;
                await db.SaveChangesAsync();
                TempData["Result"] = "اطلاعات دوره با موفقیت ویرایش شد.";
            }
        }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }

        return RedirectToAction(nameof(Periods));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivatePeriod(int id)
    {
        var p = await db.Periods.FindAsync(id);
        if (p == null) return NotFound();
        p.IsOpen = false;
        await db.SaveChangesAsync();
        TempData["Result"] = "دوره غیرفعال شد و دیگر نتیجه جدید دریافت نمی‌کند.";
        return RedirectToAction(nameof(Periods));
    }

    [HttpGet]
    public async Task<IActionResult> ExportPeriods()
    {
        await ps.SyncExpiredPeriodsAsync();
        var rows = await db.Periods.AsNoTracking().OrderByDescending(x => x.StartAt).ToListAsync();
        return File(excel.Periods(rows), ExcelMime, "EvaluationPeriods.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> Employees(string? q)
    {
        var query = db.Employees.Include(e => e.Position).Include(e => e.Unit).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(e => e.FullName.Contains(q) || e.PersonnelNo.Contains(q) || e.NationalNo.Contains(q));
        }

        var rows = await query.OrderBy(e => e.FullName).ToListAsync();
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> CreateEmployee()
    {
        return View(await BuildEmployeeFormVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmployee(EmployeeFormPost model)
    {
        model.Normalize();
        if (string.IsNullOrWhiteSpace(model.PersonnelNo) || string.IsNullOrWhiteSpace(model.NationalNo) || string.IsNullOrWhiteSpace(model.FullName))
        {
            TempData["Error"] = "شماره پرسنلی، کد ملی و نام و نام خانوادگی الزامی است.";
            return RedirectToAction(nameof(CreateEmployee));
        }
        if (await db.Employees.AnyAsync(x => x.PersonnelNo == model.PersonnelNo))
        {
            TempData["Error"] = "شماره پرسنلی تکراری است.";
            return RedirectToAction(nameof(CreateEmployee));
        }
        if (await db.Employees.AnyAsync(x => x.NationalNo == model.NationalNo))
        {
            TempData["Error"] = "کد ملی تکراری است.";
            return RedirectToAction(nameof(CreateEmployee));
        }
        if (!await db.Positions.AnyAsync(x => x.Id == model.PositionId && x.IsActive) || !await db.OrgUnits.AnyAsync(x => x.Id == model.UnitId && x.IsActive))
        {
            TempData["Error"] = "رده پستی یا واحد سازمانی معتبر نیست.";
            return RedirectToAction(nameof(CreateEmployee));
        }
        if (model.SupervisorId == modelPositionSentinel()) model.SupervisorId = null;
        if (model.SupervisorId.HasValue)
        {
            var supervisor = await db.Employees.SingleOrDefaultAsync(x => x.Id == model.SupervisorId.Value && x.IsActive);
            if (supervisor == null || supervisor.Id == model.Id) { TempData["Error"] = "سرپرست انتخاب‌شده معتبر نیست."; return RedirectToAction(nameof(CreateEmployee)); }
        }

        var employee = new Employee
        {
            PersonnelNo = model.PersonnelNo,
            NationalNo = model.NationalNo,
            FullName = model.FullName,
            Mobile = model.Mobile,
            PositionId = model.PositionId,
            UnitId = model.UnitId,
            IsEvaluator = model.IsEvaluator,
            SupervisorId = model.SupervisorId,
            IsActive = model.IsActive
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        db.Users.Add(new AppUser
        {
            UserName = employee.PersonnelNo,
            DisplayName = employee.FullName,
            EmployeeId = employee.Id,
            IsAdmin = false,
            IsActive = employee.IsActive,
            PasswordHash = PasswordHasher.Hash(employee.NationalNo)
        });
        await db.SaveChangesAsync();

        TempData["Result"] = $"کارمند «{employee.FullName}» ایجاد شد و حساب ورود او آماده است.";
        return RedirectToAction(nameof(Employees));
    }

    [HttpGet]
    public async Task<IActionResult> EditEmployee(int id)
    {
        var e = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        return View(await BuildEmployeeFormVm(e));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEmployee(EmployeeFormPost model)
    {
        model.Normalize();
        var e = await db.Employees.SingleOrDefaultAsync(x => x.Id == model.Id);
        if (e == null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.PersonnelNo) || string.IsNullOrWhiteSpace(model.NationalNo) || string.IsNullOrWhiteSpace(model.FullName))
        {
            TempData["Error"] = "شماره پرسنلی، کد ملی و نام و نام خانوادگی الزامی است.";
            return RedirectToAction(nameof(EditEmployee), new { id = model.Id });
        }
        if (await db.Employees.AnyAsync(x => x.Id != e.Id && x.PersonnelNo == model.PersonnelNo))
        {
            TempData["Error"] = "شماره پرسنلی تکراری است.";
            return RedirectToAction(nameof(EditEmployee), new { id = model.Id });
        }
        if (await db.Employees.AnyAsync(x => x.Id != e.Id && x.NationalNo == model.NationalNo))
        {
            TempData["Error"] = "کد ملی تکراری است.";
            return RedirectToAction(nameof(EditEmployee), new { id = model.Id });
        }
        if (!await db.Positions.AnyAsync(x => x.Id == model.PositionId && x.IsActive) || !await db.OrgUnits.AnyAsync(x => x.Id == model.UnitId && x.IsActive))
        {
            TempData["Error"] = "رده پستی یا واحد سازمانی معتبر نیست.";
            return RedirectToAction(nameof(EditEmployee), new { id = model.Id });
        }
        if (model.SupervisorId == e.Id)
        {
            TempData["Error"] = "یک کارمند نمی‌تواند سرپرست خودش باشد.";
            return RedirectToAction(nameof(EditEmployee), new { id = model.Id });
        }
        if (model.SupervisorId.HasValue && !await db.Employees.AnyAsync(x => x.Id == model.SupervisorId.Value && x.IsActive))
        {
            TempData["Error"] = "سرپرست انتخاب‌شده معتبر نیست.";
            return RedirectToAction(nameof(EditEmployee), new { id = model.Id });
        }

        e.PersonnelNo = model.PersonnelNo;
        e.NationalNo = model.NationalNo;
        e.FullName = model.FullName;
        e.Mobile = model.Mobile;
        e.PositionId = model.PositionId;
        e.UnitId = model.UnitId;
        e.IsEvaluator = model.IsEvaluator;
        e.SupervisorId = model.SupervisorId;
        e.IsActive = model.IsActive;

        var user = await db.Users.SingleOrDefaultAsync(x => x.EmployeeId == e.Id);
        if (user == null)
        {
            db.Users.Add(new AppUser
            {
                UserName = e.PersonnelNo, DisplayName = e.FullName, EmployeeId = e.Id,
                IsAdmin = false, IsActive = e.IsActive, PasswordHash = PasswordHasher.Hash(e.NationalNo)
            });
        }
        else
        {
            user.UserName = e.PersonnelNo;
            user.DisplayName = e.FullName;
            user.IsActive = e.IsActive;
        }

        await db.SaveChangesAsync();
        TempData["Result"] = "اطلاعات کارمند و دسترسی ورود او به‌روزرسانی شد.";
        return RedirectToAction(nameof(Employees));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateEmployee(int id)
    {
        var e = await db.Employees.FindAsync(id);
        if (e == null) return NotFound();
        e.IsActive = false;
        var user = await db.Users.SingleOrDefaultAsync(x => x.EmployeeId == id);
        if (user != null) user.IsActive = false;
        await db.SaveChangesAsync();
        TempData["Result"] = "کارمند غیرفعال شد و حساب ورود او نیز غیرفعال شد.";
        return RedirectToAction(nameof(Employees));
    }

    [HttpGet]
    public IActionResult ImportEmployees() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportEmployees(IFormFile file)
    {
        if (file == null || file.Length == 0) { TempData["Error"] = "فایل Excel انتخاب نشده است."; return RedirectToAction(nameof(ImportEmployees)); }

        try
        {
            var rows = excel.ReadEmployees(file.OpenReadStream());
            var dup = rows.GroupBy(x => x.PersonnelNo, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dup.Count > 0) TempData["Error"] = "کدهای پرسنلی تکراری: " + string.Join(", ", dup);
            else if (rows.Any(x => string.IsNullOrWhiteSpace(x.NationalNo))) TempData["Error"] = "کد ملی همه کارکنان باید تکمیل باشد.";
            else
            {
                var pos = await db.Positions.ToDictionaryAsync(x => x.Code);
                var units = await db.OrgUnits.ToDictionaryAsync(x => x.Code);
                var allRows = rows.ToList();
                var invalid = allRows.Where(r => !pos.ContainsKey(r.PositionCode) || !units.ContainsKey(r.UnitCode)).Select(r => r.PersonnelNo).ToList();
                if (invalid.Count > 0) TempData["Error"] = "رده پستی یا واحد سازمانی برای این کدها پیدا نشد: " + string.Join(", ", invalid);
                else
                {
                    var allEmployees = await db.Employees.ToListAsync();
                    foreach (var r in allRows)
                    {
                        var p = pos[r.PositionCode]; var u = units[r.UnitCode];
                        var e = allEmployees.SingleOrDefault(x => x.PersonnelNo == r.PersonnelNo);
                        var isEval = r.Evaluator is "بله" or "1" or "yes" or "true";
                        if (e == null) { e = new Employee { PersonnelNo = r.PersonnelNo, NationalNo = r.NationalNo, FullName = r.FullName, PositionId = p.Id, UnitId = u.Id, IsEvaluator = isEval, IsActive = true }; db.Employees.Add(e); allEmployees.Add(e); }
                        else { e.NationalNo = r.NationalNo; e.FullName = r.FullName; e.PositionId = p.Id; e.UnitId = u.Id; e.IsEvaluator = isEval; e.IsActive = true; }
                    }
                    await db.SaveChangesAsync();
                    allEmployees = await db.Employees.ToListAsync();
                    foreach (var r in allRows)
                    {
                        var e = allEmployees.Single(x => x.PersonnelNo == r.PersonnelNo);
                        e.SupervisorId = string.IsNullOrWhiteSpace(r.SupervisorPersonnelNo) ? null : allEmployees.FirstOrDefault(x => x.PersonnelNo == r.SupervisorPersonnelNo)?.Id;
                    }
                    foreach (var e in allEmployees.Where(x => x.IsActive))
                    {
                        var u = await db.Users.SingleOrDefaultAsync(x => x.EmployeeId == e.Id);
                        if (u == null) db.Users.Add(new AppUser { UserName = e.PersonnelNo, DisplayName = e.FullName, EmployeeId = e.Id, PasswordHash = PasswordHasher.Hash(e.NationalNo), IsActive = true });
                        else { u.UserName = e.PersonnelNo; u.DisplayName = e.FullName; u.IsActive = true; }
                    }
                    await db.SaveChangesAsync();
                    TempData["Result"] = $"{allRows.Count} رکورد پردازش شد و حساب‌های ورود ایجاد یا به‌روزرسانی شدند.";
                }
            }
        }
        catch (Exception ex) { TempData["Error"] = "فایل Excel قابل پردازش نیست: " + ex.Message; }
        return RedirectToAction(nameof(Employees));
    }

    [HttpGet]
    public async Task<IActionResult> ExportEmployees()
    {
        var rows = await db.Employees.Include(x => x.Position).Include(x => x.Unit).AsNoTracking().OrderBy(x => x.FullName).ToListAsync();
        return File(excel.Employees(rows), ExcelMime, "Employees.xlsx");
    }

    private async Task<EmployeeFormVm> BuildEmployeeFormVm(Employee? employee = null)
    {
        var positions = await db.Positions.Where(x => x.IsActive).OrderBy(x => x.Title).AsNoTracking().ToListAsync();
        var units = await db.OrgUnits.Where(x => x.IsActive).OrderBy(x => x.Title).AsNoTracking().ToListAsync();
        var supervisors = await db.Employees.Where(x => x.IsActive && (employee == null || x.Id != employee.Id)).OrderBy(x => x.FullName).AsNoTracking().ToListAsync();
        return new EmployeeFormVm(employee, positions, units, supervisors);
    }

    private static int modelPositionSentinel() => int.MinValue;

    public record EmployeeFormVm(Employee? Employee, List<Position> Positions, List<OrgUnit> Units, List<Employee> Supervisors);
    public class EmployeeFormPost
    {
        public int Id { get; set; }
        public string PersonnelNo { get; set; } = "";
        public string NationalNo { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Mobile { get; set; }
        public int PositionId { get; set; }
        public int UnitId { get; set; }
        public bool IsEvaluator { get; set; }
        public int? SupervisorId { get; set; }
        public bool IsActive { get; set; } = true;
        public void Normalize() { PersonnelNo = PersonnelNo.Trim(); NationalNo = NationalNo.Trim(); FullName = FullName.Trim(); Mobile = string.IsNullOrWhiteSpace(Mobile) ? null : Mobile.Trim(); }
    }

    public record PeriodEditVm(int Id, string Title, string StartJalali, string EndJalali, string? Description, bool IsOpen);
    public record PeriodDetailsVm(EvaluationPeriod Period, List<PeriodEvaluationRow> Evaluations);
    public record PeriodEvaluationRow(int EvaluationId, int EmployeeId, string Employee, string PersonnelNo, string Position, string Unit, string Evaluator, decimal Score, decimal Max, string Status);
}