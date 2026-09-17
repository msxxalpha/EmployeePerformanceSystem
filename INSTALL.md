# راهنمای نصب سامانه ارزیابی عملکرد ایندامین

این راهنما برای استقرار نسخه Windows x64 روی Windows Server و IIS تهیه شده است.

## 1. پیش‌نیازها

- Windows Server 2019/2022/2025 x64
- IIS 10 یا بالاتر
- SQL Server 2019 یا بالاتر (یا نسخه سازگار با EF Core/SQL Server provider پروژه)
- دسترسی مدیریتی برای ایجاد Application Pool و Database
- دسترسی شبکه‌ای IIS Server به SQL Server

> نسخه CI فعلی به صورت **self-contained win-x64** منتشر می‌شود؛ بنابراین برای اجرای خود برنامه به نصب .NET Runtime روی سرور نیاز نیست. IIS همچنان به ASP.NET Core Module (ANCM) نیاز دارد.

## 2. دریافت بسته انتشار

از GitHub Actions، آخرین artifact با نام `EmployeePerformanceSystem-win-x64` را دریافت و ZIP را در یک مسیر ثابت مانند زیر استخراج کنید:

`C:\inetpub\EmployeePerformanceSystem`

## 3. ایجاد Database

1. در SQL Server یک Database خالی ایجاد کنید، مثلاً `IndaminPerformance`.
2. Connection String را متناسب با SQL Server خود تنظیم کنید.
3. فایل‌های موجود در پوشه `Database` یا migrationهای پروژه را مطابق `MIGRATION.md` اجرا کنید.
4. پس از ایجاد جداول، وجود جداول اصلی شامل Employees، AppUsers، EvaluationPeriods، Questions، Evaluations، EvaluationScores، EvaluationScoreHistory، EvaluatorChangeHistory و AuditLogs را کنترل کنید.

## 4. تنظیم Connection String و رمز Admin

تنظیمات حساس را ترجیحاً در `appsettings.Production.json` یا Environment Variable/Secret سرور قرار دهید و رمز واقعی را در Git commit نکنید.

نمونه ساختار:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=SQLSERVER;Database=IndaminPerformance;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "InitialAdminPassword": "CHANGE-THIS-BEFORE-FIRST-USE"
}
```

در محیط عملیاتی، مقدار `InitialAdminPassword` باید یک رمز موقت قوی باشد و پس از اولین ورود مطابق سیاست امنیتی سازمان تغییر کند.

اگر تنظیم رمز اولیه Admin وجود نداشته باشد، برنامه نباید با یک رمز پیش‌فرض ناامن راه‌اندازی شود.

## 5. نصب IIS

1. در Server Manager نقش **Web Server (IIS)** را نصب کنید.
2. **ASP.NET Core Module / Hosting Bundle** مورد نیاز محیط IIS را نصب کنید. حتی در حالت self-contained، IIS برای میزبانی ASP.NET Core به ANCM نیاز دارد.
3. یک Application Pool جدید بسازید:
   - Name: `IndaminPerformance`
   - Managed Runtime Version: `No Managed Code`
   - Pipeline: `Integrated`
4. یک IIS Website بسازید و Physical Path را روی مسیر انتشار قرار دهید.
5. Binding مورد نیاز سازمان را تعریف کنید؛ در محیط واقعی ترجیحاً HTTPS استفاده شود.

## 6. دسترسی‌های فایل و SQL

Identity مربوط به Application Pool باید حداقل دسترسی Read & Execute به پوشه انتشار داشته باشد.

اگر از Windows Authentication برای SQL Server استفاده می‌شود، حساب سرویس IIS باید مجوز لازم روی Database داشته باشد. اگر SQL Authentication استفاده می‌شود، Secret را در فایل عمومی GitHub قرار ندهید.

## 7. اولین راه‌اندازی

پس از Start کردن سایت:

1. صفحه Login را باز کنید.
2. حساب Admin اولیه را با رمز تنظیم‌شده در configuration وارد کنید.
3. اطلاعات پایه رده‌های پستی، سؤال‌ها و کارکنان را وارد کنید.
4. برای کارکنان، شماره پرسنلی به عنوان username و کد ملی به عنوان password اولیه استفاده می‌شود؛ رمز به صورت hash در Database نگهداری می‌شود.
5. ویژگی `IsEvaluator` و `SupervisorId` کارکنان را کنترل کنید.
6. یک دوره ارزیابی ایجاد کنید و بازه شروع/پایان ارزیابی را تنظیم کنید.

## 8. کنترل‌های قبل از بهره‌برداری

- [ ] Login کارمند عادی فقط داشبورد شخصی را نمایش می‌دهد.
- [ ] فقط `IsEvaluator=true` اجازه ورود به امکانات ارزیابی را دارد.
- [ ] ارزیاب فقط درخت زیرمجموعه واقعی خود را مشاهده می‌کند.
- [ ] ارزیاب بالادست می‌تواند ارزیابی ارزیاب زیرمجموعه را بازبینی کند.
- [ ] تغییر ارزیاب در `EvaluatorChangeHistory` ثبت می‌شود.
- [ ] تغییر امتیاز در `EvaluationScoreHistory` ثبت می‌شود.
- [ ] `OriginalEvaluatorId` پس از بازنگری تغییر نمی‌کند.
- [ ] مجموع امتیاز از امتیازهای جاری محاسبه می‌شود.
- [ ] امتیاز بیشتر از سقف سؤال پذیرفته نمی‌شود.
- [ ] خارج از بازه ارزیابی، تغییر اطلاعات ممنوع و مشاهده سابقه مجاز است.
- [ ] کارمند غیرفعال وارد scope ارزیاب نمی‌شود.
- [ ] چرخه در ساختار سرپرستی باعث loop یا افزایش scope نمی‌شود.
- [ ] Export/Import Excel با قالب مصوب کنترل شده است.
- [ ] Audit Log برای عملیات مهم ایجاد/ثبت شده است.
- [ ] Backup منظم SQL Server و تست Restore انجام شده است.

## 9. نکات عملیاتی

- قبل از هر تغییر عمده، از Database و فایل‌های configuration نسخه پشتیبان تهیه کنید.
- برای ارتقای نسخه، ابتدا روی یک محیط آزمایشی Restore/Upgrade انجام شود.
- فایل‌های `appsettings.Production.json` دارای Secret را در GitHub قرار ندهید.
- لاگ‌های IIS و Application را در مانیتورینگ سازمان ثبت کنید.
- زمان سرور و Time Zone را کنترل کنید؛ بازه ارزیابی بر مبنای زمان سرور برنامه بررسی می‌شود.

## 10. وضعیت اعتبارسنجی CI

Pipeline پروژه مراحل Restore، Build، Test، Self-contained Publish و Package را اجرا می‌کند. موفقیت این Pipeline به معنی تست عملیاتی روی IIS واقعی نیست؛ پیش از بهره‌برداری، نصب روی یک Windows Server آزمایشی و اجرای چک‌لیست بالا الزامی است.
