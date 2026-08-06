using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Services;

public sealed class DatabaseSeeder(AppDbContext db)
{
    public async Task SeedAsync()
    {
        if (!await db.Users.AnyAsync())
        {
            var demo = new User { Email = "demo@unidemix.local", DisplayName = "کاربر آزمایشی", PasswordHash = "" , Level = "A2" };
            demo.PasswordHash = new PasswordHasher<User>().HashPassword(demo, "Demo123!");
            db.Users.Add(demo);
        }
        if (!await db.Courses.AnyAsync())
        {
            var course = new Course { Slug = "german-for-real-life", Title = "آلمانی برای زندگی واقعی", Description = "درس‌های کاربردی زبان آلمانی برای زندگی، کار و مهاجرت", LanguageCode = "de", Level = "A2" };
            course.Lessons =
            [
                MakeLesson("قرار ملاقات با پزشک", "جمله‌های کاربردی برای گرفتن وقت و توضیح علائم", "روزمره", 1, 18, 30, "🩺",
                    ("multiple-choice", "Ich ___ einen Termin.", "brauche", new[] { "brauche", "brauchst", "braucht" }, "برای فاعل ich فعل brauchen به شکل brauche صرف می‌شود."),
                    ("translation", "ترجمه کنید: من سردرد دارم.", "Ich habe Kopfschmerzen.", null, null)),
                MakeLesson("پرسیدن مسیر در شهر", "پرسیدن و فهمیدن مسیرهای رایج", "روزمره", 2, 15, 25, "🗺️",
                    ("multiple-choice", "Wo ist ___ Bahnhof?", "der", new[] { "der", "die", "das" }, "Bahnhof اسم مذکر است.")),
                MakeLesson("گفت‌وگو در محل کار", "واژگان و جمله‌های کاربردی محیط کار", "کار", 3, 22, 35, "💼",
                    ("translation", "ترجمه کنید: جلسه ساعت نه شروع می‌شود.", "Die Besprechung beginnt um neun Uhr.", null, null)),
                MakeLesson("خرید از سوپرمارکت", "قیمت، مقدار و درخواست کالا", "روزمره", 4, 14, 20, "🛒",
                    ("multiple-choice", "Was ___ das?", "kostet", new[] { "kostet", "kosten", "koste" }, null))
            ];
            db.Courses.Add(course);
        }
        await db.SaveChangesAsync();
    }

    private static Lesson MakeLesson(string title, string description, string category, int order, int duration, int xp, string icon,
        params (string Type, string Prompt, string Answer, string[]? Options, string? Explanation)[] exercises)
    {
        var lesson = new Lesson { Title = title, Description = description, Category = category, Order = order, DurationMinutes = duration, XpReward = xp, Icon = icon };
        lesson.Exercises = exercises.Select((x, i) => new Exercise { Type = x.Type, Prompt = x.Prompt, CorrectAnswer = x.Answer, OptionsJson = x.Options is null ? null : JsonSerializer.Serialize(x.Options), Explanation = x.Explanation, Order = i + 1 }).ToList();
        return lesson;
    }
}
