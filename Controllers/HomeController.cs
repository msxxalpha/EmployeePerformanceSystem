using Indamin.Performance.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indamin.Performance.Controllers;

[Authorize]
public class HomeController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (User.HasClaim("IsAdmin", "1"))
        {
            var activeEmployees = await db.Employees.CountAsync(x => x.IsActive);
            var evaluators = await db.Employees.CountAsync(x => x.IsActive && x.IsEvaluator);
            var openPeriods = await db.Periods.CountAsync(x => x.IsOpen);
            var evaluations = await db.Evaluations.CountAsync(x => x.Status != EvaluationStatus.Draft);
            var positions = await db.Positions.CountAsync(x => x.IsActive);
            var units = await db.OrgUnits.CountAsync(x => x.IsActive);
            var questions = await db.Questions.CountAsync(x => x.IsActive);
            return View(new HomeVm(User.Identity?.Name ?? "مدیر سیستم", activeEmployees, evaluators, openPeriods, evaluations, positions, units, questions));
        }

        return RedirectToAction("Index", "EmployeeDashboard");
    }

    public IActionResult Error() => View();

    public record HomeVm(
        string Name,
        int ActiveEmployees,
        int Evaluators,
        int OpenPeriods,
        int Evaluations,
        int Positions,
        int Units,
        int Questions);
}