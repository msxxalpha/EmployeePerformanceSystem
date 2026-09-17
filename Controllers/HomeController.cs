using System.Security.Claims;
using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;

[Authorize]
public class HomeController(AppDbContext db, PerformanceService ps) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) return Forbid();
        var u = await db.Users.FindAsync(uid);
        if (u == null) return Forbid();
        if (u.IsAdmin) return View(new HomeVm("مدیر سیستم", 0, 0));
        if (!int.TryParse(User.FindFirstValue("EmployeeId"), out var eid) || !await ps.IsEvaluator(eid)) return Forbid();
        var sub = await ps.GetSubordinates(eid);
        var p = await ps.CurrentPeriod();
        var done = p == null ? 0 : await db.Evaluations.CountAsync(x => x.PeriodId == p.Id && sub.Select(s => s.Id).Contains(x.EmployeeId));
        return View(new HomeVm(User.Identity?.Name ?? "", sub.Count, done));
    }

    public IActionResult Error() => View();
    public record HomeVm(string Name, int Subordinates, int Completed);
}