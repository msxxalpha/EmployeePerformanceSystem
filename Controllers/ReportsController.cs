using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;
[Authorize(Policy="AdminOnly")] public class ReportsController(AppDbContext db,ExcelService excel):Controller{
 public async Task<IActionResult> Index(int? periodId){var p=periodId??await db.Periods.OrderByDescending(x=>x.StartAt).Select(x=>x.Id).FirstOrDefaultAsync();var rows=await Query(p);ViewBag.Periods=await db.Periods.OrderByDescending(x=>x.StartAt).ToListAsync();ViewBag.PeriodId=p;return View(rows);}
 [HttpGet]public async Task<IActionResult> Export(int? periodId){var p=periodId??await db.Periods.OrderByDescending(x=>x.StartAt).Select(x=>x.Id).FirstOrDefaultAsync();var rows=await Query(p);return File(excel.Evaluations(rows.Select(x=>(x.Employee,x.Evaluator,x.Unit,x.Position,x.Score,x.Max,x.Status))),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","EvaluationReport.xlsx");}
 async Task<List<Row>> Query(int p){return await db.Evaluations.Where(x=>x.PeriodId==p).Join(db.Employees,ev=>ev.EmployeeId,e=>e.Id,(ev,e)=>new{ev,e}).Join(db.Employees,z=>z.ev.EvaluatorId,a=>a.Id,(z,a)=>new{z.ev,z.e,a}).Join(db.OrgUnits,z=>z.e.UnitId,u=>u.Id,(z,u)=>new{z.ev,z.e,z.a,u}).Join(db.Positions,z=>z.e.PositionId,pos=>pos.Id,(z,pos)=>new Row(z.e.FullName,z.a.FullName,z.u.Title,pos.Title,z.ev.Scores.Sum(s=>s.Score),db.Questions.Where(q=>q.PositionId==pos.Id&&q.IsActive).Sum(q=>q.MaxScore),z.ev.Status.ToString())).ToListAsync();}
 public record Row(string Employee,string Evaluator,string Unit,string Position,decimal Score,decimal Max,string Status);
}