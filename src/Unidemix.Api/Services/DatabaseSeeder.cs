using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Services;

public sealed class DatabaseSeeder(AppDbContext db)
{
    private const string TestPassword = "Demo123!";

    public async Task SeedAsync()
    {
        await SeedBusinessAsync();
        await SeedUsersAsync();
        await SeedCatalogAsync();
        await db.SaveChangesAsync();
        await SeedProgressAsync();
        await db.SaveChangesAsync();
    }

    private async Task SeedBusinessAsync()
    {
        if (!await db.Languages.AnyAsync())
            db.Languages.AddRange(new[] { ("en","English","English","🇬🇧"),("de","German","Deutsch","🇩🇪"),("fr","French","Français","🇫🇷"),("es","Spanish","Español","🇪🇸"),("it","Italian","Italiano","🇮🇹"),("tr","Turkish","Türkçe","🇹🇷"),("nl","Dutch","Nederlands","🇳🇱"),("pt","Portuguese","Português","🇵🇹"),("ar","Arabic","العربية","🇸🇦"),("zh","Chinese","中文","🇨🇳") }.Select((x,i)=>new Language { Code=x.Item1,Name=x.Item2,NativeName=x.Item3,FlagEmoji=x.Item4,SortOrder=i+1 }));
        if (!await db.SubscriptionPlans.AnyAsync())
            db.SubscriptionPlans.AddRange(
                new SubscriptionPlan { Code="free",Name="رایگان",Description="تمام آموزش‌های آفلاین A1 تا C1 برای یک زبان",MonthlyPrice=0,YearlyPrice=0,Currency="TOMAN",LanguageLimit=1,MonthlyAiCredits=5 },
                new SubscriptionPlan { Code="premium",Name="Premium",Description="AI، چند زبان و آزمون‌های رسمی",MonthlyPrice=249000m,YearlyPrice=2490000m,Currency="TOMAN",LanguageLimit=10,MonthlyAiCredits=500,HasMockExams=true },
                new SubscriptionPlan { Code="premium-plus",Name="Premium Plus",Description="ظرفیت بالاتر AI و تحلیل پیشرفته",MonthlyPrice=499000m,YearlyPrice=4990000m,Currency="TOMAN",LanguageLimit=10,MonthlyAiCredits=2000,HasMockExams=true });
        if (!await db.AdPlacements.AnyAsync())
            db.AdPlacements.AddRange(
                new AdPlacement { Code="dashboard-banner",Name="بنر داشبورد",Page="dashboard",Position="between-sections",DailyPrice=20,Width=1200,Height=180 },
                new AdPlacement { Code="courses-card",Name="کارت میان دوره‌ها",Page="courses",Position="in-feed",DailyPrice=12,Width=600,Height=400 },
                new AdPlacement { Code="floating-bottom",Name="تبلیغ شناور پایین",Page="global",Position="floating-bottom",DailyPrice=30,Width=320,Height=250 });
        if (!await db.ContentItems.AnyAsync())
        {
            for (var i=1;i<=8;i++) db.ContentItems.Add(new ContentItem { ExternalId=$"news-{i}",Type="news",Slug=$"daily-germany-{i}",Title=$"خبر روز آلمان شماره {i}",Summary="خلاصه‌ای کوتاه از مهم‌ترین رویدادهای روز برای زبان‌آموزان و مهاجران.",Body="این متن نمونه از همان قرارداد JSON است که n8n می‌تواند هر روز به‌روزرسانی کند.",Category=i%2==0?"جامعه":"آلمان",Source="n8n Demo",PublishedAt=DateTimeOffset.UtcNow.AddHours(-i) });
            for (var i=1;i<=6;i++) db.ContentItems.Add(new ContentItem { ExternalId=$"blog-{i}",Type="blog",Slug=$"language-learning-{i}",Title=$"راهنمای یادگیری زبان شماره {i}",Summary="روش‌های عملی برای یادگیری پایدار و سریع‌تر زبان.",Body="مقاله نمونه وبلاگ. محتوای کامل از JSON خروجی n8n در این فیلد قرار می‌گیرد.",Category="یادگیری",Source="Unidemix Blog",PublishedAt=DateTimeOffset.UtcNow.AddDays(-i) });
        }
        await db.SaveChangesAsync();
    }

    private async Task SeedUsersAsync()
    {
        var people = new[]
        {
            ("demo@unidemix.local", "سارا احمدی", "A2", "daily-life", 20),
            ("ali@unidemix.local", "علی رضایی", "A1", "immigration", 15),
            ("mina@unidemix.local", "مینا کریمی", "B1", "work", 30),
            ("reza@unidemix.local", "رضا محمدی", "A2", "study", 20),
            ("nazanin@unidemix.local", "نازنین مرادی", "B2", "work", 45),
            ("amir@unidemix.local", "امیر حسینی", "A1", "daily-life", 10),
            ("leila@unidemix.local", "لیلا اکبری", "B1", "travel", 25),
            ("parsa@unidemix.local", "پارسا یوسفی", "A2", "exam", 30),
            ("niloofar@unidemix.local", "نیلوفر موسوی", "A1", "family", 15),
            ("arash@unidemix.local", "آرش شریفی", "B2", "university", 40),
            ("shirin@unidemix.local", "شیرین قاسمی", "A2", "immigration", 20),
            ("mehdi@unidemix.local", "مهدی نادری", "B1", "work", 30),
            ("yasaman@unidemix.local", "یاسمن جعفری", "A1", "travel", 15),
            ("pouya@unidemix.local", "پویا رستمی", "A2", "daily-life", 20),
            ("sahar@unidemix.local", "سحر کاظمی", "B1", "exam", 35),
            ("admin@unidemix.local", "مدیر یونیدمیکس", "C1", "administration", 30)
        };

        var existing = (await db.Users.Select(x => x.Email).ToListAsync()).ToHashSet();
        foreach (var person in people.Where(x => !existing.Contains(x.Item1)))
        {
            var user = new User
            {
                Email = person.Item1, DisplayName = person.Item2, PasswordHash = "", Level = person.Item3,
                Goal = person.Item4, DailyGoalMinutes = person.Item5,
                Role = person.Item1.StartsWith("admin") ? "Admin" : "User"
            };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, TestPassword);
            db.Users.Add(user);
        }
    }

    private async Task SeedCatalogAsync()
    {
        var catalogs = new[]
        {
            Catalog("german-for-real-life", "آلمانی برای زندگی واقعی", "موقعیت‌های روزمره از خرید تا مراجعه به پزشک", "A2", "روزمره", "🏠",
                "معرفی خود و دیگران", "خرید از سوپرمارکت", "پرسیدن مسیر در شهر", "قرار ملاقات با پزشک", "اجاره خانه", "کارهای بانکی", "اداره پست", "تماس تلفنی"),
            Catalog("german-starter", "شروع آلمانی از صفر", "پایه‌های ضروری برای زبان‌آموزان تازه‌کار", "A1", "مقدماتی", "🌱",
                "الفبا و تلفظ", "سلام و احوال‌پرسی", "اعداد و ساعت", "خانواده من", "رنگ‌ها و وسایل", "روزهای هفته", "غذا و نوشیدنی", "مرور سطح A1"),
            Catalog("german-at-work", "آلمانی در محیط کار", "ارتباط حرفه‌ای، جلسه و مکاتبه اداری", "B1", "کار", "💼",
                "روز اول کاری", "معرفی در تیم", "نوشتن ایمیل", "جلسه کاری", "مرخصی و بیماری", "ارائه کوتاه", "حل اختلاف", "مصاحبه شغلی"),
            Catalog("german-grammar", "گرامر کاربردی آلمانی", "گرامر همراه با مثال‌های قابل استفاده", "A2", "گرامر", "🧩",
                "افعال جداشدنی", "حالت Akkusativ", "حالت Dativ", "افعال Modal", "گذشته Perfekt", "جملات وابسته", "صفات و مقایسه", "مرور جامع گرامر"),
            Catalog("german-for-travel", "آلمانی برای سفر", "مکالمات فرودگاه، هتل و گردش در شهر", "A2", "سفر", "✈️",
                "در فرودگاه", "تحویل بار", "رزرو هتل", "در رستوران", "خرید بلیت", "مشکل در سفر", "جاذبه‌های گردشگری", "بازگشت به خانه"),
            Catalog("german-exam-b1", "آمادگی آزمون B1", "تمرین مهارت‌های اصلی و نمونه آزمون", "B1", "آزمون", "🎓",
                "درک مطلب ۱", "درک مطلب ۲", "شنیداری ۱", "شنیداری ۲", "نامه رسمی", "نامه دوستانه", "مکالمه آزمون", "آزمون آزمایشی کامل")
        };

        foreach (var catalog in catalogs)
        {
            var course = await db.Courses.Include(x => x.Lessons).SingleOrDefaultAsync(x => x.Slug == catalog.Slug);
            if (course is null)
            {
                course = new Course { Slug = catalog.Slug, Title = catalog.Title, Description = catalog.Description, LanguageCode = "de", Level = catalog.Level };
                db.Courses.Add(course);
            }

            var usedOrders = course.Lessons.Select(x => x.Order).ToHashSet();
            for (var index = 0; index < catalog.Lessons.Length; index++)
            {
                var order = index + 1;
                if (usedOrders.Contains(order)) continue;
                var lesson = MakeLesson(catalog.Lessons[index], catalog.Category, order,
                    12 + (order * 2), 15 + (order * 5), catalog.Icon);
                lesson.Course = course;
                db.Lessons.Add(lesson);
            }
        }
    }

    private async Task SeedProgressAsync()
    {
        var users = await db.Users.OrderBy(x => x.Email).ToListAsync();
        var lessons = await db.Lessons.OrderBy(x => x.CourseId).ThenBy(x => x.Order).ToListAsync();
        var existing = (await db.LessonProgress.Select(x => new { x.UserId, x.LessonId }).ToListAsync())
            .Select(x => (x.UserId, x.LessonId)).ToHashSet();

        for (var userIndex = 0; userIndex < users.Count; userIndex++)
        {
            var activityCount = 4 + (userIndex % 18);
            for (var lessonIndex = 0; lessonIndex < Math.Min(activityCount, lessons.Count); lessonIndex++)
            {
                var key = (users[userIndex].Id, lessons[lessonIndex].Id);
                if (existing.Contains(key)) continue;
                var completed = lessonIndex < activityCount - 2;
                db.LessonProgress.Add(new LessonProgress
                {
                    UserId = key.Item1, LessonId = key.Item2,
                    Percent = completed ? 100 : 25 + ((userIndex + lessonIndex) % 3 * 25),
                    BestScore = completed ? 65 + ((userIndex * 7 + lessonIndex * 3) % 36) : 20 + ((userIndex + lessonIndex) % 45),
                    IsCompleted = completed,
                    UpdatedAt = DateTimeOffset.UtcNow.AddDays(-(userIndex + lessonIndex) % 21)
                });
            }
        }
    }

    private static Lesson MakeLesson(string title, string category, int order, int duration, int xp, string icon)
    {
        var lesson = new Lesson
        {
            Title = title, Description = $"آموزش و تمرین کاربردی: {title}", Category = category,
            Order = order, DurationMinutes = duration, XpReward = xp, Icon = icon
        };
        lesson.Exercises =
        [
            Exercise("multiple-choice", $"گزینه درست را برای «{title}» انتخاب کنید.", "richtig", order * 10 + 1,
                ["richtig", "falsch", "vielleicht"], "به ساختار جمله و جایگاه فعل توجه کنید."),
            Exercise("translation", $"ترجمه کنید: این تمرین درباره {title} است.", $"Diese Übung handelt von {title}.", order * 10 + 2),
            Exercise("fill-blank", "Ich ___ heute Deutsch.", "lerne", order * 10 + 3,
                ["lerne", "lernst", "lernt"], "برای فاعل ich، فعل lernen به صورت lerne صرف می‌شود."),
            Exercise("listening", "عبارت شنیده‌شده را بنویسید: Guten Morgen!", "Guten Morgen!", order * 10 + 4)
        ];
        return lesson;
    }

    private static Exercise Exercise(string type, string prompt, string answer, int order, string[]? options = null, string? explanation = null) =>
        new() { Type = type, Prompt = prompt, CorrectAnswer = answer, Order = order, OptionsJson = options is null ? null : JsonSerializer.Serialize(options), Explanation = explanation };

    private static CourseCatalog Catalog(string slug, string title, string description, string level, string category, string icon, params string[] lessons) =>
        new(slug, title, description, level, category, icon, lessons);

    private sealed record CourseCatalog(string Slug, string Title, string Description, string Level, string Category, string Icon, string[] Lessons);
}
