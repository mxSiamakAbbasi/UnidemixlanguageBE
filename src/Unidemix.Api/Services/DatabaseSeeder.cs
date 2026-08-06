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
        await SeedUsersAsync();
        await SeedCatalogAsync();
        await db.SaveChangesAsync();
        await SeedProgressAsync();
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
