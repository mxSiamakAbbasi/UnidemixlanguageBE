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
    public bool IsPublished { get; set; }
    public int ContentVersion { get; set; } = 1;
    public int WrittenDurationMinutes { get; set; }
    public int SpeakingDurationMinutes { get; set; }
    public int PreparationDurationMinutes { get; set; }
    public string? TimingJson { get; set; }
    public string? BlueprintJson { get; set; }
    public string? PracticeBankJson { get; set; }
    public string? MockVariantsJson { get; set; }
    public string? ScoringJson { get; set; }
    public string? SourceReference { get; set; }
    public ICollection<ExamSection> Sections { get; set; } = [];
    public ICollection<ExamLevelMapping> LevelMappings { get; set; } = [];
    public ICollection<MockExamAttempt> Attempts { get; set; } = [];
    public ICollection<ExamBlueprint> Blueprints { get; set; } = [];
}

public sealed class ExamBlueprint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamProgramId { get; set; }
    public ExamProgram Program { get; set; } = null!;
    public required string ProviderCode { get; set; }
    public required string ExamKey { get; set; }
    public required string Variant { get; set; }
    public required string Cefr { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = "Provisional";
    public required string SourceReferencesJson { get; set; }
    public required string DefinitionJson { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<GeneratedExamContent> GeneratedContents { get; set; } = [];
}

public sealed class GeneratedExamContent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamBlueprintId { get; set; }
    public ExamBlueprint Blueprint { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Mode { get; set; }
    public string? SectionKey { get; set; }
    public string? PartKey { get; set; }
    public string Status { get; set; } = "Generating";
    public string? ContentJson { get; set; }
    public string? ValidatorResultJson { get; set; }
    public string? RejectionReason { get; set; }
    public string? Fingerprint { get; set; }
    public string? ModelReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadyAt { get; set; }
}

public sealed class MockExamAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamProgramId { get; set; }
    public ExamProgram Program { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string VariantKey { get; set; }
    public string Status { get; set; } = "InProgress";
    public string AnswersJson { get; set; } = "{}";
    public int CurrentItemIndex { get; set; }
    public string? ResultJson { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; set; }
    public string ExecutionMode { get; set; } = "TimedSimulation";
    public DateTimeOffset? ExpiresAt { get; set; }
    public Guid? GeneratedExamContentId { get; set; }
    public GeneratedExamContent? GeneratedExamContent { get; set; }
}

public sealed class ExamPracticeSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamProgramId { get; set; }
    public ExamProgram Program { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string SectionKey { get; set; }
    public required string PartKey { get; set; }
    public int CurrentItemIndex { get; set; }
    public string AnswersJson { get; set; } = "{}";
    public string SubmittedItemsJson { get; set; } = "[]";
    public string Status { get; set; } = "InProgress";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
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
