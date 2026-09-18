using System.Security.Cryptography;
using System.Security.Claims;
using Indamin.Performance.Data;
using Indamin.Performance.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder=WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(o=>o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<PerformanceService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{o.LoginPath="/Account/Login";o.AccessDeniedPath="/Account/Denied";o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=true;});
builder.Services.AddAuthorization(o=>o.AddPolicy("AdminOnly",p=>p.RequireClaim("IsAdmin","1")));
var app=builder.Build();
using(var scope=app.Services.CreateScope())
{
 var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
 await DatabaseInitializer.InitializeAsync(db,app.Environment.ContentRootPath);
 var initialPassword=app.Configuration["InitialAdminPassword"];
 if(string.IsNullOrWhiteSpace(initialPassword))throw new InvalidOperationException("InitialAdminPassword must be configured before the application starts.");
 await Seed.Initialize(db,initialPassword);
}
if(!app.Environment.IsDevelopment())app.UseExceptionHandler("/Home/Error");
app.UseStaticFiles();app.UseRouting();app.UseAuthentication();app.UseAuthorization();
app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
app.Run();
static class Seed
{
 public static async Task Initialize(AppDbContext db,string initialPassword)
 {
  if(await db.Users.AnyAsync(x=>x.IsAdmin))return;
  db.Users.Add(new AppUser{UserName="admin",DisplayName="مدیر سیستم",IsAdmin=true,IsActive=true,PasswordHash=PasswordHasher.Hash(initialPassword)});
  await db.SaveChangesAsync();
 }
}
public static class PasswordHasher
{
 public static string Hash(string value){var salt=RandomNumberGenerator.GetBytes(16);var key=Rfc2898DeriveBytes.Pbkdf2(value,salt,100000,HashAlgorithmName.SHA256,32);return $"100000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";}
 public static bool Verify(string value,string stored){try{var p=stored.Split('.');if(p.Length!=3)return false;var salt=Convert.FromBase64String(p[1]);var expected=Convert.FromBase64String(p[2]);var key=Rfc2898DeriveBytes.Pbkdf2(value,salt,int.Parse(p[0]),HashAlgorithmName.SHA256,expected.Length);return CryptographicOperations.FixedTimeEquals(key,expected);}catch{return false;}}
}