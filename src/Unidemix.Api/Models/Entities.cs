namespace Unidemix.Api.Models;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public string NativeLanguage { get; set; } = "fa";
    public string LearningLanguage { get; set; } = "de";
    public string Level { get; set; } = "A1";
    public string Goal { get; set; } = "daily-life";
    public int DailyGoalMinutes { get; set; } = 15;
    public string Role { get; set; } = "User";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<LessonProgress> Progress { get; set; } = [];
}

public sealed class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Slug { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string LanguageCode { get; set; }
    public required string Level { get; set; }
    public ICollection<Lesson> Lessons { get; set; } = [];
}

public sealed class Lesson
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public int Order { get; set; }
    public int DurationMinutes { get; set; }
    public int XpReward { get; set; }
    public string Icon { get; set; } = "📚";
    public ICollection<Exercise> Exercises { get; set; } = [];
    public ICollection<LessonProgress> Progress { get; set; } = [];
}

public sealed class Exercise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public required string Type { get; set; }
    public required string Prompt { get; set; }
    public required string CorrectAnswer { get; set; }
    public string? OptionsJson { get; set; }
    public string? Explanation { get; set; }
    public int Order { get; set; }
}

public sealed class LessonProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public int Percent { get; set; }
    public int BestScore { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
