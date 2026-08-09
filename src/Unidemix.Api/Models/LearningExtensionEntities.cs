namespace Unidemix.Api.Models;

public sealed class VocabularyItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public required string Term { get; set; }
    public string ContentType { get; set; } = "Chunk";
    public string? Pronunciation { get; set; }
    public required string Meaning { get; set; }
    public required string Example { get; set; }
    public required string ExampleTranslation { get; set; }
    public string? Note { get; set; }
    public int Order { get; set; }
    public ICollection<VocabularyReview> Reviews { get; set; } = [];
}

public sealed class VocabularyReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid VocabularyItemId { get; set; }
    public VocabularyItem VocabularyItem { get; set; } = null!;
    public int SuccessfulReviews { get; set; }
    public int FailedReviews { get; set; }
    public int IntervalDays { get; set; }
    public DateTimeOffset NextReviewAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastReviewedAt { get; set; }
}

public sealed class ExamProvider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string LanguageCode { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ExamProgram> Programs { get; set; } = [];
}

public sealed class ExamProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamProviderId { get; set; }
    public ExamProvider Provider { get; set; } = null!;
    public required string Level { get; set; }
    public string Code { get; set; } = "default";
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ExamSection> Sections { get; set; } = [];
    public ICollection<ExamLevelMapping> LevelMappings { get; set; } = [];
}

public sealed class ExamLevelMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamProgramId { get; set; }
    public ExamProgram Program { get; set; } = null!;
    public required string CefrLevel { get; set; }
    public bool IsApproximate { get; set; }
}

public sealed class ExamSection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamProgramId { get; set; }
    public ExamProgram Program { get; set; } = null!;
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int Order { get; set; }
}
