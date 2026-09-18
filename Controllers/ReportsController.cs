using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Indamin.Performance.Controllers;

[Authorize(Policy = "AdminOnly")]
public class ReportsController(AppDbContext db, ExcelService excel) : Controller
{
    public async Task<IActionResult> Index(int? periodId)
    {
        var selectedId = periodId ?? await db.Periods.OrderByDescending(x => x.StartAt).Select(x => (int?)x.Id).FirstOrDefaultAsync() ?? 0;
        ViewBag.Periods = await db.Periods.OrderByDescending(x => x.StartAt).ToListAsync();
        ViewBag.PeriodId = selectedId;
        return View(await Query(selectedId));
    }

    [HttpGet]
    public async Task<IActionResult> Export(int? periodId)
    {
        var selectedId = periodId ?? await db.Periods.OrderByDescending(x => x.StartAt).Select(x => (int?)x.Id).FirstOrDefaultAsync() ?? 0;
        var rows = await Query(selectedId);
        return File(
            excel.Evaluations(rows.Select(x => (x.Employee, x.Evaluator, x.Unit, x.Position, x.Score, x.Max, x.Status))),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "EvaluationReport.xlsx");
    }

    private async Task<List<Row>> Query(int periodId)
    {
        var query =
            from ev in db.Evaluations.AsNoTracking()
            join employee in db.Employees.AsNoTracking() on ev.EmployeeId equals employee.Id
            join evaluator in db.Employees.AsNoTracking() on ev.EvaluatorId equals evaluator.Id
            join unit in db.OrgUnits.AsNoTracking() on employee.UnitId equals unit.Id
            join position in db.Positions.AsNoTracking() on employee.PositionId equals position.Id
            where ev.PeriodId == periodId
            select new Row(
                employee.FullName,
                evaluator.FullName,
                unit.Title,
                position.Title,
                ev.FinalScore,
                ev.FinalMaxScore,
                ev.Status.ToString());

        return await query.OrderBy(x => x.Employee).ToListAsync();
    }

    public record Row(string Employee, string Evaluator, string Unit, string Position, decimal Score, decimal Max, string Status);
}