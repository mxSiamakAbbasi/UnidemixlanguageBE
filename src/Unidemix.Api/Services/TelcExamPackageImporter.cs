using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Services;

public sealed class TelcExamPackageImporter(AppDbContext db, IWebHostEnvironment environment)
{
    private static readonly string[] SupportedLevels = ["A1", "A2", "B1", "B2"];

    public async Task ImportAsync(string relativePath)
    {
        var path = Path.Combine(environment.ContentRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var package = JsonSerializer.Deserialize<TelcExamPackage>(await File.ReadAllTextAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("TELC exam package is empty.");
        Validate(package);
        var provider = db.ExamProviders.Local.SingleOrDefault(x => x.LanguageCode == "de" && x.Code == "telc")
            ?? await db.ExamProviders.Include(x => x.Programs).ThenInclude(x => x.Sections)
                .Include(x => x.Programs).ThenInclude(x => x.LevelMappings)
                .SingleAsync(x => x.LanguageCode == "de" && x.Code == "telc");
        var program = provider.Programs.Single(x => x.Level == package.Cefr);
        program.Name = package.Name;
        program.IsPublished = true;
        program.ContentVersion = package.Version;
        program.WrittenDurationMinutes = package.Timing.WrittenMinutes;
        program.SpeakingDurationMinutes = package.Timing.SpeakingMinutes;
        program.PreparationDurationMinutes = package.Timing.PreparationMinutes;
        program.TimingJson = JsonSerializer.Serialize(package.Timing);
        program.BlueprintJson = JsonSerializer.Serialize(package.Sections);
        program.PracticeBankJson = JsonSerializer.Serialize(package.PracticeBank.Select(task => task.RequiresEvaluation ? task : task with
        {
            Explanation = task.Explanation ?? "پاسخ درست با اطلاعات صریح متن یا فایل شنیداری مطابقت دارد؛ به واژه‌های کلیدی زمان، مکان و هدف توجه کنید."
        }));
        program.MockVariantsJson = JsonSerializer.Serialize(package.MockVariants.Select(variant => variant with
        {
            Tasks = variant.Tasks.Select(task => HasPersian(task.Prompt) ? task with { Prompt = GermanExamPrompt(task) } : task).ToArray()
        }).ToArray());
        program.ScoringJson = JsonSerializer.Serialize(package.Scoring);
        program.SourceReference = package.SourceReference;
        foreach (var section in package.Sections.OrderBy(x => x.Order))
        {
            var entity = program.Sections.SingleOrDefault(x => x.Code == section.Code);
            if (entity is null)
            {
                entity = new ExamSection { Code = section.Code, Name = section.Name };
                program.Sections.Add(entity);
                db.ExamSections.Add(entity);
            }
            entity.Name = section.Name;
            entity.Order = section.Order;
        }
    }

    private static bool HasPersian(string value) => value.Any(character => character is >= '\u0600' and <= '\u06ff');
    private static string GermanExamPrompt(ExamTaskSource task) => task.Section switch
    {
        "listening" => task.Type is "true-false" or "yes-no"
            ? "Hören Sie den Text. Ist die Aussage richtig oder falsch?"
            : "Hören Sie den Text und wählen Sie die richtige Antwort.",
        "reading" or "language-elements" => task.Type is "true-false" or "yes-no"
            ? "Lesen Sie den Text. Ist die Aussage richtig oder falsch?"
            : "Lesen Sie den Text und wählen Sie die richtige Antwort.",
        "writing" => $"Schreiben Sie den geforderten Text für {HumanPart(task.Part)}. Bearbeiten Sie alle Inhaltspunkte und achten Sie auf eine passende Anrede und einen passenden Schluss.",
        "speaking" => $"Bearbeiten Sie die Sprechaufgabe {HumanPart(task.Part)}. Gehen Sie auf alle Punkte ein und reagieren Sie auf Ihren Gesprächspartner.",
        _ => $"Bearbeiten Sie die Aufgabe {HumanPart(task.Part)} und wählen Sie die richtige Lösung."
    };
    private static string HumanPart(string key) => int.TryParse(new string(key.Where(char.IsDigit).ToArray()), out var number) ? $"Teil {number}" : key;

    public static void Validate(TelcExamPackage p)
    {
        if (p.Provider != "telc" || p.Language != "de" || !SupportedLevels.Contains(p.Cefr) || p.Version < 1) throw new InvalidOperationException("Unsupported TELC mapping.");
        if (p.Timing.WrittenMinutes <= 0 || p.Timing.SpeakingMinutes <= 0 || p.Sections.Length < 4) throw new InvalidOperationException("TELC timing or sections are incomplete.");
        if (p.Cefr is "B1" or "B2" && !p.Sections.Any(x => x.Code == "language-elements")) throw new InvalidOperationException("Sprachbausteine is required for this level.");
        if (p.Cefr is "A1" or "A2" && p.Sections.Any(x => x.Code == "language-elements")) throw new InvalidOperationException("A1/A2 must follow their own official blueprints.");
        if (p.Sections.Select(x => x.Code).Distinct().Count() != p.Sections.Length || p.Sections.Any(x => x.Parts.Length == 0)) throw new InvalidOperationException("TELC section or part IDs are invalid.");
        var declaredParts = p.Sections.SelectMany(section => section.Parts.Select(part => (section.Code, part.Key))).ToHashSet();
        if (declaredParts.Count != p.Sections.Sum(x => x.Parts.Length)) throw new InvalidOperationException("Duplicate TELC part IDs.");
        if (declaredParts.Any(part => !p.PracticeBank.Any(task => task.Section == part.Code && task.Part == part.Key))) throw new InvalidOperationException("Practice bank does not cover every declared part.");
        if (p.MockVariants.Length < 2 || p.MockVariants.Select(x => x.Key).Distinct().Count() != p.MockVariants.Length) throw new InvalidOperationException("Two unique mock variants are required.");
        var fullMock = ValidateFullMocks(p);
        if (!fullMock.IsFullMockComplete) throw new InvalidOperationException(string.Join(" ", fullMock.Issues));
        foreach (var variant in p.MockVariants)
        {
            ExamContentQualityValidator.ValidateGermanMock(variant.Tasks);
            if (variant.Tasks.Select(x => x.Key).Distinct().Count() != variant.Tasks.Length) throw new InvalidOperationException("Duplicate mock task IDs.");
            foreach (var task in variant.Tasks)
                if (task.Options is { Length: > 0 } && (task.Options.Distinct().Count() != task.Options.Length || !task.Options.Contains(task.CorrectAnswer))) throw new InvalidOperationException("Invalid mock answer options.");
            if (!variant.Tasks.Any(x => x.Section == "writing") || !variant.Tasks.Any(x => x.Section == "speaking")) throw new InvalidOperationException("Writing and speaking tasks are required.");
            if (variant.Tasks.Any(x => x.Section == "listening" && string.IsNullOrWhiteSpace(x.Script))) throw new InvalidOperationException("Listening script reference is required.");
        }
        if (p.Scoring.MaxPoints <= 0 || string.IsNullOrWhiteSpace(p.Scoring.ResultLabel)) throw new InvalidOperationException("Scoring configuration is incomplete.");
        if (p.Cefr == "A1")
        {
            var expected = new Dictionary<string, int> { ["listening"] = 3, ["reading"] = 3, ["writing"] = 2, ["speaking"] = 3 };
            if (p.Timing.WrittenMinutes != 65 || p.Timing.AdministrativeDurationMinutes != 10 || p.Timing.SessionDurationMinutes != 75 ||
                p.Timing.ListeningDurationMinutes != 20 || p.Timing.ReadingWritingDurationMinutes != 45 || p.Timing.SpeakingMinutes != 15 ||
                p.Timing.WrittenMinutes != p.Timing.ListeningDurationMinutes + p.Timing.ReadingWritingDurationMinutes ||
                p.Timing.SessionDurationMinutes != p.Timing.WrittenMinutes + p.Timing.AdministrativeDurationMinutes ||
                p.Scoring.MaxPoints != 60 || p.Scoring.PassPoints != 36 ||
                expected.Any(x => p.Sections.SingleOrDefault(section => section.Code == x.Key)?.Parts.Length != x.Value) ||
                p.Scoring.SectionPoints?.Values.Sum() != 60 || p.Scoring.ResultBands is not { Length: 5 } || p.Scoring.WritingRubric is null || p.Scoring.SpeakingRubric is null)
                throw new InvalidOperationException("TELC A1 timing, structure, scoring bands or rubrics are incomplete.");
        }
    }

    public static StoredMockValidationResult ValidateFullMocks(TelcExamPackage package)
    {
        var issues = new List<string>();
        var fixedFormat = package.Cefr switch
        {
            "A1" => (65, 15, 0, new[] { ("listening", 3), ("reading", 3), ("writing", 2), ("speaking", 3) }),
            "A2" => (70, 15, 0, new[] { ("listening", 3), ("reading", 2), ("writing", 2), ("speaking", 3) }),
            "B1" => (150, 15, 20, new[] { ("reading", 3), ("language-elements", 2), ("listening", 3), ("writing", 1), ("speaking", 3) }),
            "B2" => (140, 15, 20, new[] { ("reading", 3), ("language-elements", 2), ("listening", 3), ("writing", 1), ("speaking", 3) }),
            _ => (0, 0, 0, Array.Empty<(string, int)>())
        };
        if (package.Timing.WrittenMinutes != fixedFormat.Item1 || package.Timing.SpeakingMinutes != fixedFormat.Item2 || package.Timing.PreparationMinutes != fixedFormat.Item3) issues.Add("Fixed exam timing changed.");
        var sessionDurationMinutes = package.Timing.SessionDurationMinutes ?? package.Timing.WrittenMinutes;
        if (sessionDurationMinutes != package.Timing.WrittenMinutes + package.Timing.AdministrativeDurationMinutes) issues.Add("Written-session timing total is inconsistent.");
        if (!package.Sections.OrderBy(x => x.Order).Select(x => (x.Code, x.Parts.Length)).SequenceEqual(fixedFormat.Item4)) issues.Add("Required section/part structure changed.");
        foreach (var variant in package.MockVariants)
        {
            if (variant.Tasks.Select(x => x.Key).Distinct().Count() != variant.Tasks.Length) issues.Add($"{variant.Key}: duplicate task IDs.");
            foreach (var section in package.Sections)
            foreach (var part in section.Parts)
            {
                var tasks = variant.Tasks.Where(x => x.Section == section.Code && x.Part == part.Key).ToArray();
                var expected = part.TaskType is "writing" or "speaking" ? 1 : part.ItemCount;
                if (tasks.Length != expected) issues.Add($"{variant.Key}/{section.Code}/{part.Key}: expected {expected} items, found {tasks.Length}.");
                foreach (var task in tasks)
                {
                    if (task.Type != part.TaskType) issues.Add($"{task.Key}: wrong task type.");
                    if (task.Options?.Distinct(StringComparer.Ordinal).Count() != task.Options?.Length) issues.Add($"{task.Key}: duplicate options.");
                    if (part.OptionCount > 0 && task.Options?.Length != part.OptionCount) issues.Add($"{task.Key}: wrong option count.");
                    if (part.OptionCount == 0 && task.Options is { Length: > 0 }) issues.Add($"{task.Key}: options are not allowed.");
                    if (part.AnswerType != "productive" && (string.IsNullOrWhiteSpace(task.CorrectAnswer) || (task.Options is { Length: > 0 } && !task.Options.Contains(task.CorrectAnswer)))) issues.Add($"{task.Key}: invalid correct answer.");
                    if (part.AnswerType == "productive" != task.RequiresEvaluation) issues.Add($"{task.Key}: wrong evaluation mode.");
                    if (section.Code == "listening" && (string.IsNullOrWhiteSpace(task.Script) || task.PlaybackCount != part.PlaybackCount || task.AudioStatus != "script-ready-audio-deferred")) issues.Add($"{task.Key}: listening metadata is incomplete.");
                }
            }
            if (variant.Tasks.Any(x => !package.Sections.Any(s => s.Code == x.Section && s.Parts.Any(p => p.Key == x.Part)))) issues.Add($"{variant.Key}: undeclared task.");
        }
        return new(issues.Count == 0, issues.ToArray());
    }
}

public sealed record TelcExamPackage(string Provider, string Language, string Cefr, string Name, int Version, string SourceReference, ExamTimingSource Timing, ExamSectionSource[] Sections, ExamTaskSource[] PracticeBank, MockVariantSource[] MockVariants, ExamScoringSource Scoring);
public sealed record ExamTimingSource(int WrittenMinutes, int SpeakingMinutes, int PreparationMinutes,
    int AdministrativeDurationMinutes = 0, int? SessionDurationMinutes = null,
    int? ListeningDurationMinutes = null, int? ReadingWritingDurationMinutes = null);
public sealed record ExamSectionSource(string Code, string Name, int Order, ExamPartSource[] Parts);
public sealed record ExamPartSource(string Key, string Name, string TaskType, int ItemCount, int OptionCount = 0,
    string AnswerType = "productive", int? PlaybackCount = null, int? TimingMinutes = null);
public sealed record MockVariantSource(string Key, string Name, ExamTaskSource[] Tasks);
public sealed record ExamTaskSource(string Key, string Section, string Part, string Type, string Prompt, string? SourceText, string? Script, string? SpeakerMetadata, int? DurationTargetSeconds, int? PlaybackCount, string? AudioStatus, string? CorrectAnswer, string[]? Options, int Points, bool RequiresEvaluation, string? Explanation = null, ExamFormFieldSource[]? FormFields = null);
public sealed record ExamFormFieldSource(string Key, string Label, string CorrectAnswer);
public sealed record ExamScoringSource(int MaxPoints, int? PassPoints, bool RequiresSeparateWrittenAndOralThresholds, string ResultLabel, Dictionary<string, decimal>? SectionPoints = null, ExamResultBandSource[]? ResultBands = null, JsonElement? WritingRubric = null, JsonElement? SpeakingRubric = null);
public sealed record ExamResultBandSource(decimal Min, decimal Max, string Label);
public sealed record StoredMockValidationResult(bool IsFullMockComplete, string[] Issues);
