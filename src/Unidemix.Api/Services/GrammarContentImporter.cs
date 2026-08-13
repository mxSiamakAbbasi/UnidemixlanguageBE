using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Services;

public sealed record GrammarPackage(string Version, GrammarTopicSource[] Topics);
public sealed record GrammarTopicSource(string ContentKey, string LanguageCode, string CefrLevel, int Order, string TitleDe, string TitleFa, string Category, string Progression, string QualityStatus, string[] RelatedCoreLessons, GrammarExerciseBlueprint ExerciseBlueprint, GrammarReference Reference, GrammarExercise[] Exercises);
public sealed record GrammarExerciseBlueprint(string[] LearningObjectives, string[] Subskills, string[] AllowedExerciseTypes, string[] ValidPatterns, string[] CommonErrorPatterns, string[] DistractorRules, string[] VocabularyDomains, string[] ForbiddenPatterns, int MinimumSemanticVariation, string[] DifficultyProgression);
public sealed record GrammarReference(string UsageFa, string Structure, GrammarTable[] Tables, GrammarExample[] Examples, GrammarContrast[] Contrasts, string[] CommonMistakes, string[] Notes, string SummaryFa);
public sealed record GrammarTable(string Title, string[] Headers, string[][] Rows);
public sealed record GrammarExample(string German, string Persian);
public sealed record GrammarContrast(string Left, string Right, string ExplanationFa);
public sealed record GrammarExercise(string Id, string Type, string Subskill, string PromptFa, string PromptDe, string[] Options, string[] AcceptedAnswers, string ExplanationFa, string QaStatus = "PASS", string[]? NormalizationRules = null, string? ErrorReason = null, string? ContextDomain = null, string? SemanticFamily = null);

public sealed class GrammarContentImporter(AppDbContext db, IWebHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> SupportedLevels = ["A1", "A2", "B1", "B2", "C1"];

    public async Task ImportAsync(string relativePath)
    {
        await using var stream = File.OpenRead(Path.Combine(environment.ContentRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var package = await JsonSerializer.DeserializeAsync<GrammarPackage>(stream, JsonOptions) ?? throw new InvalidOperationException("Grammar package is empty.");
        Validate(package);
        foreach (var source in package.Topics)
        {
            var topic = await db.GrammarTopics.SingleOrDefaultAsync(x => x.ContentKey == source.ContentKey);
            if (topic is null) { topic = new GrammarTopic { ContentKey = source.ContentKey, CefrLevel = source.CefrLevel, TitleDe = source.TitleDe, TitleFa = source.TitleFa, Category = source.Category, Progression = source.Progression, ReferenceJson = "{}", ExercisesJson = "[]", RelatedCoreLessonsJson = "[]" }; db.GrammarTopics.Add(topic); }
            topic.LanguageCode = source.LanguageCode; topic.CefrLevel = source.CefrLevel; topic.Order = source.Order;
            topic.TitleDe = source.TitleDe; topic.TitleFa = source.TitleFa; topic.Category = source.Category; topic.Progression = source.Progression;
            topic.ReferenceJson = JsonSerializer.Serialize(source.Reference); topic.ExercisesJson = JsonSerializer.Serialize(source.Exercises);
            topic.ExerciseBlueprintJson = JsonSerializer.Serialize(source.ExerciseBlueprint); topic.RelatedCoreLessonsJson = JsonSerializer.Serialize(source.RelatedCoreLessons); topic.ContentVersion = package.Version; topic.QualityStatus = source.QualityStatus; topic.IsPublished = source.QualityStatus is "PASS" or "PASS WITH MINOR FIXES";
        }
        await db.SaveChangesAsync();
    }

    public static void Validate(GrammarPackage package)
    {
        if (string.IsNullOrWhiteSpace(package.Version) || package.Topics.Length == 0) throw new InvalidOperationException("Grammar version/topics are required.");
        if (package.Topics.Select(x => x.ContentKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != package.Topics.Length) throw new InvalidOperationException("Duplicate grammar topic key.");
        if (package.Topics.Select(x => $"{x.LanguageCode}:{x.CefrLevel}").Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1) throw new InvalidOperationException("A grammar package must contain exactly one language and CEFR level.");
        if (package.Topics.Select(x => x.Order).Distinct().Count() != package.Topics.Length) throw new InvalidOperationException("Duplicate grammar topic order.");
        foreach (var topic in package.Topics)
        {
            if (topic.LanguageCode != "de" || !SupportedLevels.Contains(topic.CefrLevel) || topic.QualityStatus is not ("PASS" or "PASS WITH MINOR FIXES" or "REVISION REQUIRED" or "REJECT") || topic.Reference.Examples.Length < 4 || topic.Reference.CommonMistakes.Length == 0 || topic.Reference.Tables.Length == 0 || topic.Exercises.Length < 12 || topic.ExerciseBlueprint.Subskills.Length < 2)
                throw new InvalidOperationException($"Incomplete {topic.CefrLevel} grammar topic: {topic.ContentKey}.");
            if (topic.Exercises.Select(x => x.Id).Distinct().Count() != topic.Exercises.Length) throw new InvalidOperationException($"Duplicate question ID: {topic.ContentKey}.");
            foreach (var exercise in topic.Exercises)
            {
                var invalidCore = string.IsNullOrWhiteSpace(exercise.Subskill) || !topic.ExerciseBlueprint.Subskills.Contains(exercise.Subskill) || !topic.ExerciseBlueprint.AllowedExerciseTypes.Contains(exercise.Type) || string.IsNullOrWhiteSpace(exercise.PromptFa) || string.IsNullOrWhiteSpace(exercise.PromptDe) || exercise.AcceptedAnswers.Length == 0 || (exercise.Type is not ("shortProduction" or "fillBlank" or "transformation") && (exercise.Options.Length < 2 || exercise.Options.Distinct().Count() != exercise.Options.Length || !exercise.AcceptedAnswers.All(exercise.Options.Contains)));
                var invalidPublishMetadata = topic.QualityStatus == "PASS" && (string.IsNullOrWhiteSpace(exercise.ContextDomain) || string.IsNullOrWhiteSpace(exercise.SemanticFamily) || (exercise.Options.Length > 0 && string.IsNullOrWhiteSpace(exercise.ErrorReason)));
                if (invalidCore || invalidPublishMetadata)
                    throw new InvalidOperationException($"Invalid exercise {exercise.Id}.");
                ValidateNaturalness(topic, exercise);
            }
            var normalized = topic.Exercises.Select(x => $"{x.PromptDe.Trim().ToLowerInvariant()}|{string.Join('|', x.Options.Select(o => o.Trim().ToLowerInvariant()).Order())}").ToArray();
            if (normalized.Distinct().Count() != normalized.Length) throw new InvalidOperationException($"Repetitive exercise variants: {topic.ContentKey}.");
            var covered = topic.Exercises.Where(x => x.QaStatus == "PASS").Select(x => x.Subskill).Distinct().ToHashSet();
            if (topic.ExerciseBlueprint.Subskills.Any(x => !covered.Contains(x))) throw new InvalidOperationException($"Subskill coverage is incomplete: {topic.ContentKey}.");
            if (topic.QualityStatus == "PASS" && topic.Exercises.Any(x => x.QaStatus != "PASS")) throw new InvalidOperationException($"A passed topic contains unapproved exercises: {topic.ContentKey}.");
        }
    }

    private static void ValidateNaturalness(GrammarTopicSource topic, GrammarExercise exercise)
    {
        var forbidden = new[] { "heute Deutsch heute", "zu Hause zu Hause", "gekauft heute", " am Montag am Montag", " in Berlin in Berlin" };
        foreach (var answer in exercise.AcceptedAnswers)
        {
            if (forbidden.Any(answer.Contains) || System.Text.RegularExpressions.Regex.IsMatch(answer, @"^(ich|du|er|sie|wir|ihr|Sie|[A-ZÄÖÜ][a-zäöüß]+):\s"))
                throw new InvalidOperationException($"Suspicious correct answer in {exercise.Id}.");
            var tokens = System.Text.RegularExpressions.Regex.Matches(answer.ToLowerInvariant(), @"[a-zäöüß]+") .Select(x => x.Value).ToArray();
            if (tokens.Zip(tokens.Skip(1)).Any(x => x.First == x.Second)) throw new InvalidOperationException($"Repeated adjacent token in {exercise.Id}.");
        }
        if (exercise.Options.Any(x => System.Text.RegularExpressions.Regex.IsMatch(x, @"^(ich|du|er|sie|wir|ihr|Sie|[A-ZÄÖÜ][a-zäöüß]+):\s"))) throw new InvalidOperationException($"Malformed labelled distractor in {exercise.Id}.");
        if (topic.ExerciseBlueprint.ForbiddenPatterns.Any(pattern => exercise.Options.Any(x => x.Contains(pattern, StringComparison.OrdinalIgnoreCase)))) throw new InvalidOperationException($"Topic-forbidden pattern in {exercise.Id}.");
    }
}
