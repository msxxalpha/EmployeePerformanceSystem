using System.Security.Claims;
using Indamin.Performance.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;

[Authorize]
public class HomeController(AppDbContext db) : Controller
{
    public IActionResult Index()
    {
        if (User.HasClaim("IsAdmin", "1")) return View(new HomeVm(User.Identity?.Name ?? "مدیر سیستم", 0, 0));
        return RedirectToAction("Index", "EmployeeDashboard");
    }
    public IActionResult Error() => View();
    public record HomeVm(string Name, int Subordinates, int Completed);
}