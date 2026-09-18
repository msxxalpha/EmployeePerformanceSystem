# راهنمای نصب سامانه ارزیابی عملکرد ایندامین

## نسخه آزمایشی اولیه
این پروژه به صورت Self-contained win-x64 منتشر می‌شود؛ برای اجرای خود برنامه Visual Studio یا .NET Runtime روی سرور لازم نیست. برای IIS نصب ASP.NET Core Hosting Bundle 10.x / ANCM لازم است.

## 1. استقرار روی IIS
1. فایل ZIP انتشار را استخراج کنید؛ دو پوشه Application و Database و فایل INSTALL.md داخل آن است.
2. پوشه Application را مثلاً در C:\inetpub\EmployeePerformanceSystem قرار دهید.
3. در IIS یک Application Pool با Managed Runtime Version = No Managed Code و Pipeline = Integrated بسازید.
4. Website را با Physical Path روی پوشه Application تنظیم کنید.
5. Connection String را در Application/appsettings.json تنظیم کنید. کلید آن در این نسخه Default است.
6. یک Database خالی مانند IndaminPerformance بسازید.
7. در حالت Database خالی، برنامه در اولین اجرا با EnsureCreated جداول را می‌سازد. اجرای Database/001_initial.sql اختیاری است.
8. اگر Database متعلق به نسخه‌های قبلی همین پروژه است، 001_initial.sql را اجرا نکنید؛ برنامه SchemaVersions را کنترل و Database/002_upgrade.sql را به‌صورت تراکنشی اجرا می‌کند.
9. سایت را Start کنید و Login را باز کنید.

## 2. ورود
- مدیر اولیه: admin / مقدار InitialAdminPassword
- کارمند: شماره پرسنلی / کد ملی اولیه
- رمز کارکنان به صورت Hash در Database ذخیره می‌شود.
- کارمند عادی فقط داشبورد شخصی خود را می‌بیند.
- کارمند دارای IsEvaluator علاوه بر داشبورد شخصی، امکانات ارزیابی زیرمجموعه را دارد.

## 3. ترتیب راه‌اندازی
1. ساخت واحدهای سازمانی
2. تعریف رده‌های پستی
3. تعریف سؤال در بانک سؤال
4. اتصال سؤال به رده پستی با سقف امتیاز و ترتیب
5. ورود کارکنان از Excel
6. کنترل IsEvaluator و SupervisorId
7. تعریف دوره و بازه ارزیابی با تقویم شمسی
8. انجام ارزیابی

## 4. منطق ارزیابی
- SupervisorId محدوده ارزیابی را در کل درخت سازمانی تعیین می‌کند.
- ارزیاب بالادست که خودش ارزیاب است می‌تواند کل subtree را مشاهده و ارزیابی کند.
- هنگام بازنگری ارزیابی ارزیاب پایین‌دست، OriginalEvaluatorId حفظ می‌شود.
- تغییر امتیازها در EvaluationScoreHistory و تغییر ارزیاب در EvaluatorChangeHistory ثبت می‌شود.
- سقف هر سؤال از PositionQuestions خوانده و در EvaluationScores به عنوان snapshot ذخیره می‌شود.
- امتیاز نهایی دوره از جمع Score سؤال‌های همان ارزیابی و سقف نهایی از جمع MaxScore همان snapshot محاسبه می‌شود.
- بعد از پایان بازه، فرم‌ها فقط قابل مشاهده هستند.

## 5. کنترل اولیه
- [ ] Login Admin
- [ ] ایجاد دوره با تاریخ شمسی
- [ ] ایجاد رده پستی
- [ ] تعریف سؤال با کد، عنوان، حوزه و توضیحات
- [ ] اتصال سؤال به رده با سقف امتیاز
- [ ] Import چهار کارمند نمونه A→B→C→D از Excel
- [ ] Login B و ارزیابی C
- [ ] Login A و بازنگری C
- [ ] بررسی ScoreHistory و EvaluatorChangeHistory
- [ ] Login C و مشاهده فقط داشبورد C
- [ ] بررسی گزارش مدیریتی و Export Excel

## 6. نکات امنیتی و عملیاتی
رمز اولیه Admin را پیش از بهره‌برداری واقعی تغییر دهید، Secretهای SQL را در Git قرار ندهید، Backup/Restore SQL Server را آزمایش کنید و قبل از جایگزینی نسخه روی IIS از Database و configuration نسخه پشتیبان بگیرید.

## 7. اعتبارسنجی
CI پروژه Restore، Build، Test، Self-contained Publish و Package را اجرا می‌کند. موفقیت CI جایگزین تست عملی روی IIS و SQL Server سازمان نیست.