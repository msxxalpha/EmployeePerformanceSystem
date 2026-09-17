using System.Security.Claims;
using Indamin.Performance.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;
public class AccountController(AppDbContext db):Controller{
[HttpGet]public IActionResult Login(string? returnUrl=null)=>View(new LoginVm{returnUrl=returnUrl});
[HttpPost][ValidateAntiForgeryToken]public async Task<IActionResult> Login(LoginVm m){var u=await db.Users.SingleOrDefaultAsync(x=>x.UserName==m.UserName&&x.IsActive);if(u==null||!PasswordHasher.Verify(m.Password,u.PasswordHash)){ModelState.AddModelError("","نام کاربری یا رمز عبور صحیح نیست.");return View(m);}if(u.EmployeeId.HasValue){var e=await db.Employees.FindAsync(u.EmployeeId.Value);if(e==null||!e.IsEvaluator){ModelState.AddModelError("","این کاربر مجوز ارزیابی ندارد.");return View(m);}}
var claims=new List<Claim>{new(ClaimTypes.NameIdentifier,u.Id.ToString()),new(ClaimTypes.Name,u.DisplayName),new("IsAdmin",u.IsAdmin?"1":"0")};if(u.EmployeeId.HasValue)claims.Add(new Claim("EmployeeId",u.EmployeeId.Value.ToString()));await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)));return Redirect("/");}
[HttpPost]public async Task<IActionResult> Logout(){await HttpContext.SignOutAsync();return RedirectToAction(nameof(Login));}
public IActionResult Denied()=>Content("دسترسی غیرمجاز است.");public record LoginVm{public string UserName{get;set;}="";public string Password{get;set;}="";public string? returnUrl{get;set;}}}