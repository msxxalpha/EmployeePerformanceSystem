using System.Security.Claims;
using Indamin.Performance.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Indamin.Performance.Controllers;
public class AccountController(AppDbContext db):Controller{
[HttpGet][AllowAnonymous]public IActionResult Login(string? returnUrl=null)=>View(new LoginVm{returnUrl=returnUrl});
[HttpPost][AllowAnonymous][ValidateAntiForgeryToken]public async Task<IActionResult> Login(LoginVm m){
 if(string.IsNullOrWhiteSpace(m.UserName)||string.IsNullOrWhiteSpace(m.Password)){ModelState.AddModelError("","نام کاربری و رمز عبور الزامی است.");return View(m);}
 var u=await db.Users.SingleOrDefaultAsync(x=>x.UserName==m.UserName.Trim()&&x.IsActive);
 if(u==null||!PasswordHasher.Verify(m.Password,u.PasswordHash)){ModelState.AddModelError("","نام کاربری یا رمز عبور صحیح نیست.");return View(m);}
 if(u.EmployeeId.HasValue){var e=await db.Employees.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==u.EmployeeId.Value&&x.IsActive);if(e==null){ModelState.AddModelError("","حساب کاربری کارمند فعال نیست.");return View(m);}}
 var claims=new List<Claim>{new(ClaimTypes.NameIdentifier,u.Id.ToString()),new(ClaimTypes.Name,u.DisplayName),new("IsAdmin",u.IsAdmin?"1":"0")};
 if(u.EmployeeId.HasValue){claims.Add(new Claim("EmployeeId",u.EmployeeId.Value.ToString()));var e=await db.Employees.AsNoTracking().SingleAsync(x=>x.Id==u.EmployeeId.Value);claims.Add(new Claim("IsEvaluator",e.IsEvaluator?"1":"0"));}
 await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)));
 if(!string.IsNullOrWhiteSpace(m.returnUrl)&&Url.IsLocalUrl(m.returnUrl))return Redirect(m.returnUrl);
 if(u.IsAdmin)return Redirect("/");
 if(u.EmployeeId.HasValue){var emp=await db.Employees.AsNoTracking().SingleAsync(x=>x.Id==u.EmployeeId.Value);return Redirect(emp.IsEvaluator?"/Evaluation":"/EmployeeDashboard");}
 return Redirect("/");}
[HttpGet][Authorize]public IActionResult ChangePassword()=>View(new ChangePasswordVm());

[HttpPost][Authorize][ValidateAntiForgeryToken]
public async Task<IActionResult> ChangePassword(ChangePasswordVm m){
 var userIdText=User.FindFirstValue(ClaimTypes.NameIdentifier);
 if(!int.TryParse(userIdText,out var userId))return Challenge();
 if(string.IsNullOrWhiteSpace(m.CurrentPassword)||string.IsNullOrWhiteSpace(m.NewPassword)||string.IsNullOrWhiteSpace(m.ConfirmPassword)){
  ModelState.AddModelError("","تکمیل همه فیلدهای رمز عبور الزامی است.");
  return View(m);
 }
 if(m.NewPassword.Length<8){
  ModelState.AddModelError(nameof(m.NewPassword),"رمز عبور جدید باید حداقل ۸ کاراکتر باشد.");
  return View(m);
 }
 if(m.NewPassword!=m.ConfirmPassword){
  ModelState.AddModelError(nameof(m.ConfirmPassword),"تکرار رمز عبور جدید با آن یکسان نیست.");
  return View(m);
 }
 var u=await db.Users.SingleOrDefaultAsync(x=>x.Id==userId&&x.IsActive);
 if(u==null)return Challenge();
 if(!PasswordHasher.Verify(m.CurrentPassword,u.PasswordHash)){
  ModelState.AddModelError(nameof(m.CurrentPassword),"رمز عبور فعلی صحیح نیست.");
  return View(m);
 }
 if(PasswordHasher.Verify(m.NewPassword,u.PasswordHash)){
  ModelState.AddModelError(nameof(m.NewPassword),"رمز عبور جدید باید با رمز عبور فعلی متفاوت باشد.");
  return View(m);
 }
 u.PasswordHash=PasswordHasher.Hash(m.NewPassword);
 await db.SaveChangesAsync();
 TempData["Result"]="رمز عبور شما با موفقیت تغییر کرد.";
 return RedirectToAction("Index","Home");
}

[HttpPost][Authorize][ValidateAntiForgeryToken]public async Task<IActionResult> Logout(){await HttpContext.SignOutAsync();return RedirectToAction(nameof(Login));}
[AllowAnonymous]public IActionResult Denied()=>Content("دسترسی غیرمجاز است.");public record LoginVm{public string UserName{get;set;}="";public string Password{get;set;}="";public string? returnUrl{get;set;}}public class ChangePasswordVm{public string CurrentPassword{get;set;}="";public string NewPassword{get;set;}="";public string ConfirmPassword{get;set;}="";}}