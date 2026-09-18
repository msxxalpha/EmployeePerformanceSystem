using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;
[Authorize(Policy="AdminOnly")]
public class ReportsController(AppDbContext db,ExcelService excel):Controller
{
 public async Task<IActionResult> Index(int? periodId)
 {
  var id=periodId??await db.Periods.OrderByDescending(x=>x.StartAt).Select(x=>(int?)x.Id).FirstOrDefaultAsync()??0;
  ViewBag.Periods=await db.Periods.OrderByDescending(x=>x.StartAt).ToListAsync();ViewBag.PeriodId=id;
  return View(await Query(id));
 }
 [HttpGet]public async Task<IActionResult> Export(int? periodId)
 {
  var id=periodId??await db.Periods.OrderByDescending(x=>x.StartAt).Select(x=>(int?)x.Id).FirstOrDefaultAsync()??0;var rows=await Query(id);
  return File(excel.Evaluations(rows.Select(x=>(x.Employee,x.Evaluator,x.Unit,x.Position,x.Score,x.Max,x.Status))),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","EvaluationReport.xlsx");
 }
 private Task<List<Row>> Query(int periodId)=>db.Evaluations.AsNoTracking().Where(x=>x.PeriodId==periodId)
  .Join(db.Employees,ev=>ev.EmployeeId,e=>e.Id,(ev,e)=>new{ev,e})
  .Join(db.Employees,x=>x.ev.EvaluatorId,a=>a.Id,(x,a)=>new{x.ev,x.e,a})
  .Join(db.OrgUnits,x=>x.e.UnitId,u=>u.Id,(x,u)=>new{x.ev,x.e,x.a,u})
  .Join(db.Positions,x=>x.e.PositionId,p=>p.Id,(x,p)=>new Row(x.e.FullName,x.a.FullName,x.u.Title,p.Title,x.ev.FinalScore,x.ev.FinalMaxScore,x.ev.Status.ToString())).ToListAsync();
 public record Row(string Employee,string Evaluator,string Unit,string Position,decimal Score,decimal Max,string Status);
}