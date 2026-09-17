using Indamin.Performance.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;
[Authorize(Policy="AdminOnly")]public class SetupController(AppDbContext db):Controller{
 public async Task<IActionResult> Positions()=>View(await db.Positions.Include(x=>x.Questions).OrderBy(x=>x.Title).ToListAsync());
 [HttpPost][ValidateAntiForgeryToken]public async Task<IActionResult>AddPosition(string code,string title,decimal maxScore=100){db.Positions.Add(new Position{Code=code,Title=title,MaxScore=maxScore});await db.SaveChangesAsync();return RedirectToAction(nameof(Positions));}
 [HttpPost][ValidateAntiForgeryToken]public async Task<IActionResult>AddQuestion(int positionId,string text,decimal maxScore,int sortOrder=1){db.Questions.Add(new Question{PositionId=positionId,Text=text,MaxScore=maxScore,SortOrder=sortOrder});await db.SaveChangesAsync();return RedirectToAction(nameof(Positions));}
 public async Task<IActionResult> OrgUnits()=>View(await db.OrgUnits.OrderBy(x=>x.Title).ToListAsync());
 [HttpPost][ValidateAntiForgeryToken]public async Task<IActionResult>AddOrgUnit(string code,string title,int? parentId){db.OrgUnits.Add(new OrgUnit{Code=code,Title=title,ParentId=parentId});await db.SaveChangesAsync();return RedirectToAction(nameof(OrgUnits));}
}