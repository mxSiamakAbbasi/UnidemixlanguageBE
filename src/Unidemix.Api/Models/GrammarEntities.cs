namespace Unidemix.Api.Models;

public sealed class GrammarTopic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ContentKey { get; set; }
    public string LanguageCode { get; set; } = "de";
    public required string CefrLevel { get; set; }
    public int Order { get; set; }
    public required string TitleDe { get; set; }
    public required string TitleFa { get; set; }
    public required string Category { get; set; }
    public required string Progression { get; set; }
    public required string ReferenceJson { get; set; }
    public required string ExercisesJson { get; set; }
    public string ExerciseBlueprintJson { get; set; } = "{}";
    public required string RelatedCoreLessonsJson { get; set; }
    public string ContentVersion { get; set; } = "1.0.0";
    public string QualityStatus { get; set; } = "RevisionRequired";
    public bool IsPublished { get; set; } = true;
}

public sealed class GrammarPracticeSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid? GrammarTopicId { get; set; }
    public GrammarTopic? GrammarTopic { get; set; }
    public required string LanguageCode { get; set; }
    public required string CefrLevel { get; set; }
    public required string Mode { get; set; }
    public required string SelectedQuestionIdsJson { get; set; }
    public string QuestionOptionOrderJson { get; set; } = "{}";
    public int CurrentQuestionIndex { get; set; }
    public string Status { get; set; } = "InProgress";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public ICollection<GrammarPracticeAnswer> Answers { get; set; } = [];
}

public sealed class GrammarPracticeAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GrammarPracticeSessionId { get; set; }
    public GrammarPracticeSession? Session { get; set; }
    public Guid GrammarTopicId { get; set; }
    public GrammarTopic? GrammarTopic { get; set; }
    public required string QuestionId { get; set; }
    public required string LearnerAnswer { get; set; }
    public required string CorrectAnswer { get; set; }
    public bool IsCorrect { get; set; }
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
}

public sealed class GrammarTopicProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid GrammarTopicId { get; set; }
    public GrammarTopic? GrammarTopic { get; set; }
    public DateTime? ReferenceViewedAt { get; set; }
    public int SessionsCompleted { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalAnswers { get; set; }
    public DateTime? LastPracticedAt { get; set; }
}
