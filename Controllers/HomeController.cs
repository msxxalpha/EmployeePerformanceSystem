using Indamin.Performance.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;
[Authorize]
public class HomeController(AppDbContext db):Controller
{
 public async Task<IActionResult> Index()
 {
  if(!User.HasClaim("IsAdmin","1")) return RedirectToAction("Index","EmployeeDashboard");
  return View(new HomeVm(User.Identity?.Name??"مدیر سیستم",
   await db.Employees.CountAsync(x=>x.IsActive),
   await db.Employees.CountAsync(x=>x.IsActive&&x.IsEvaluator),
   await db.Periods.CountAsync(x=>x.IsOpen),
   await db.Evaluations.CountAsync(x=>x.Status!=EvaluationStatus.Draft),
   await db.Positions.CountAsync(x=>x.IsActive),
   await db.OrgUnits.CountAsync(x=>x.IsActive),
   await db.Questions.CountAsync(x=>x.IsActive)));
 }
 public IActionResult Error()=>View();
 public record HomeVm(string Name,int ActiveEmployees,int Evaluators,int OpenPeriods,int Evaluations,int Positions,int Units,int Questions);
}