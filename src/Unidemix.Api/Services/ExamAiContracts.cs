using System.Text.Json;

namespace Unidemix.Api.Services;

public static class ExamAiCapabilities
{
    public const string PartGeneration = "ExamAiPartGeneration";
    public const string SectionGeneration = "ExamAiSectionGeneration";
    public const string MockGeneration = "ExamAiMockGeneration";
    public const string WritingReview = "ExamAiWritingReview";
    public const string SpeakingReview = "ExamAiSpeakingReview";
    public const string Examiner = "ExamAiExaminer";
}

public sealed record ExamBlueprintDocument(string Provider, string ExamKey, string DisplayName, string Language, string Cefr,
    string Variant, int Version, string Status, DateTimeOffset ValidFrom, string[] SourceReferences,
    ExamSectionBlueprint[] Sections, ExamScoringBlueprint Scoring, Dictionary<string, string>? Metadata = null);
public sealed record ExamSectionBlueprint(string Key, string Name, int Order, int TimingMinutes, ExamPartBlueprint[] Parts);
public sealed record ExamPartBlueprint(string Key, int Order, string TaskType, int ItemCount, string Instructions,
    string ResponseType, ExamPlaybackRule? PlaybackRule, int? TimingMinutes, string ScoringRule,
    int OptionCount, ExamGenerationConstraints GenerationConstraints, ExamDifficultyProfile DifficultyProfile,
    ExamContentSlotBlueprint[] ContentSlots);
public sealed record ExamPlaybackRule(int Plays, int SpeakerCount, int? ApproximateDurationSeconds, string? PauseStructure);
public sealed record ExamGenerationConstraints(int MinTextLength, int MaxTextLength, int RequiredPromptPoints,
    string Register, string[] ForbiddenBehaviors, string? VisualRequirement = null, string? VisualType = null);
public sealed record ExamDifficultyProfile(int MaxAverageSentenceWords, string VocabularyRange, string InferenceLevel,
    string DistractorDifficulty, int MinResponseWords, int MaxResponseWords, string Register);
public sealed record ExamContentSlotBlueprint(string Key, string Type, int? ItemIndex, bool Required, int MinLength, int MaxLength);
public sealed record ExamScoringBlueprint(int MaximumPoints, int? PassPoints, bool ProductiveEvaluationRequired,
    string Rule, string[] SectionOrder);
public sealed record ExamAiContext(string Provider, string Exam, string Variant, string Cefr, int BlueprintVersion,
    string Mode, FixedExamPartContract[] Parts, string[] PreviousGenerationFingerprints);
public sealed record FixedExamPartContract(string Section, int SectionOrder, int SectionTimingMinutes, string Part, int PartOrder, string TaskType, int QuestionCount, int OptionCount,
    string ResponseType, ExamPlaybackRule? PlaybackRule, int? TimingMinutes, string ScoringRule,
    ExamGenerationConstraints Constraints, ExamDifficultyProfile DifficultyProfile, ExamContentSlotBlueprint[] ContentSlots);
public sealed record ExamAiGenerationRequest(string Mode, string? SectionKey, string? PartKey, string? TopicSeed);
public sealed record GeneratedContentReplacementEnvelope(string SchemaVersion, GeneratedContentReplacement[] Replacements);
public sealed record GeneratedContentReplacement(string SlotKey, string? Text, string[]? Options, string? CorrectAnswer);
public sealed record GeneratedExamEnvelope(string SchemaVersion, string Provider, string ExamKey, string Variant, string Cefr,
    int BlueprintVersion, string Mode, GeneratedExamTask[] Tasks);
public sealed record GeneratedExamTask(string Key, string Section, string Part, string Type, string Prompt, string? SourceText,
    string? Script, string? SpeakerMetadata, int? DurationTargetSeconds, int? PlaybackCount, string? AudioStatus,
    string? CorrectAnswer, string[]? Options, int Points, bool RequiresEvaluation, int ItemCount, int PromptPointCount,
    int OptionCount, string ResponseType, int SectionOrder, int SectionTimingMinutes, int PartOrder,
    int? TimingMinutes, string ScoringRule, string[] ContentSlotKeys, GeneratedExamQuestion[] Questions);
public sealed record GeneratedExamQuestion(string Key, string Prompt, string[]? Options, string? CorrectAnswer);
public sealed record ExamValidationIssue(string Code, string Message);
public sealed record ExamValidationResult(bool IsValid, ExamValidationIssue[] Issues, string? Fingerprint);

public interface IExamAiContentProvider
{
    bool IsConfigured { get; }
    string ProviderReference { get; }
    Task<GeneratedContentReplacementEnvelope> GenerateAsync(ExamAiContext context, CancellationToken cancellationToken);
}

public sealed class UnavailableExamAiContentProvider : IExamAiContentProvider
{
    public bool IsConfigured => false;
    public string ProviderReference => "unconfigured";
    public Task<GeneratedContentReplacementEnvelope> GenerateAsync(ExamAiContext context, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No production AI exam provider is configured.");
}
