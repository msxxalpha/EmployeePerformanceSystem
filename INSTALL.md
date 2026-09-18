# راهنمای نصب سامانه ارزیابی عملکرد ایندامین

این راهنما برای استقرار نسخه Windows x64 روی Windows Server و IIS تهیه شده است.

## 1. پیش‌نیازها

- Windows Server 2019/2022/2025 x64
- IIS 10 یا بالاتر
- SQL Server 2019 یا بالاتر
- دسترسی مدیریتی برای ایجاد Application Pool و Database
- دسترسی شبکه‌ای IIS Server به SQL Server

نسخه انتشار به صورت **self-contained win-x64** ساخته می‌شود؛ برای اجرای خود برنامه روی سرور نیازی به نصب .NET Runtime نیست. IIS همچنان به ASP.NET Core Module (ANCM) نیاز دارد.

## 2. ایجاد پایگاه داده

برای نسخه اولیه، فقط فایل زیر را در دیتابیس هدف اجرا کنید:

`Database/001_initial.sql`

این فایل ساختار کامل فعلی را ایجاد می‌کند و شامل:
- AppUsers
- OrgUnits
- Positions
- Questions
- PositionQuestions
- Employees
- EvaluationPeriods
- Evaluations
- EvaluationScores
- EvaluationScoreHistory
- EvaluatorChangeHistory
- AuditLogs

همچنین کلیدهای خارجی، ایندکس‌ها، محدودیت‌های امتیاز و Precision فیلدهای Decimal در همین فایل قرار دارند.

**فایل upgrade جداگانه‌ای برای نصب اولیه وجود ندارد و نباید اسکریپت دیگری اجرا شود.**

## 3. تنظیم Connection String

تنظیمات حساس را در Git commit نکنید. در محیط آزمایشی می‌توانید `appsettings.json` یا تنظیمات محیط اجرا را مطابق سرور تنظیم کنید.

نمونه:

```json
{
  "ConnectionStrings": {
    "Default": "Server=SQLSERVER;Database=IndaminPerformance;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "InitialAdminPassword": "CHANGE-THIS-BEFORE-FIRST-USE"
}
```

## 4. اولین ورود

- مدیر سیستم: `admin` / مقدار `InitialAdminPassword`
- کارمند: شماره پرسنلی / کد ملی

در Import کارکنان، برای کارمند جدید حساب کاربری ساخته می‌شود و کد ملی به صورت Hash ذخیره می‌شود.

## 5. نصب IIS

1. Web Server (IIS) را نصب کنید.
2. ASP.NET Core Module / Hosting Bundle مورد نیاز IIS را نصب کنید.
3. Application Pool بسازید:
   - Name: `IndaminPerformance`
   - Managed Runtime Version: `No Managed Code`
   - Pipeline: `Integrated`
4. Website را به پوشه `Application` بسته انتشار متصل کنید.
5. در محیط واقعی HTTPS را فعال کنید.

## 6. راه‌اندازی اولیه

پس از اجرای سایت:
1. ورود Admin
2. تعریف واحدهای سازمانی
3. تعریف رده‌های پستی
4. تعریف بانک سؤالات
5. اتصال سؤال به رده و تعیین سقف امتیاز
6. ورود کارکنان از Excel
7. کنترل Supervisor و IsEvaluator
8. ایجاد دوره ارزیابی و بازه شروع/پایان

## 7. کنترل‌های قبل از تست عملیاتی

- Login مدیر
- ایجاد واحد
- ایجاد رده پستی
- ایجاد سؤال با عنوان، متن، حوزه و راهنمای ارزیاب
- اتصال سؤال به رده با سقف امتیاز
- تعریف دوره با تقویم شمسی
- Import کارکنان
- مشاهده داشبورد شخصی
- نمایش فقط زیرمجموعه واقعی برای ارزیاب
- ارزیابی و محاسبه مجموع امتیاز
- بازنگری توسط ارزیاب بالادست
- ثبت تاریخچه تغییر امتیاز و تغییر ارزیاب
- محدودیت بازه زمانی
- Export Excel

## 8. نکته مهم درباره دیتابیس

برای این نسخه، چون دیتابیس شما بدون داده عملیاتی است، در صورت مشاهده خطای ساختار:
1. دیتابیس آزمایشی را حذف کنید.
2. یک دیتابیس خالی با نام موردنظر ایجاد کنید.
3. فقط `Database/001_initial.sql` را اجرا کنید.
4. برنامه را مجدداً اجرا کنید.

برنامه نباید در هر اجرا اسکریپت Update را اعمال کند.