using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Services;

public sealed record LearningContentPackage(
    string ContentKey,
    string Language,
    string Cefr,
    int LessonNumber,
    string CourseSlug,
    string Title,
    string LocalizedTitle,
    string Description,
    int EstimatedDurationMinutes,
    string Status,
    string MainSituation,
    string[] CanDoObjectives,
    string[] SectionOrder,
    AudioContentStatus Audio,
    LearningActivitySource[] Activities,
    VocabularySource[] Vocabulary,
    QuestionBankItemSource[]? QuestionBank = null,
    string[]? DeferredGrammar = null);

public sealed record AudioContentStatus(string Status, string? AssetUrl, string ScriptActivityKey);

public sealed record LearningActivitySource(
    string Key,
    string Type,
    string Kind,
    string Section,
    string Prompt,
    string? CorrectAnswer,
    string[]? Options,
    string? CorrectFeedback,
    string? IncorrectFeedback);

public sealed record VocabularySource(
    string Key,
    string Target,
    string Type,
    string Meaning,
    string? Pronunciation,
    string Example,
    string ExampleTranslation,
    string? Note,
    string Classification);

public sealed record QuestionBankItemSource(
    string Key,
    string Skill,
    string Type,
    string Prompt,
    string CorrectAnswer,
    string[]? Options,
    string? CorrectFeedback,
    string? IncorrectFeedback);

public sealed class LearningContentPackageImporter(AppDbContext db, IWebHostEnvironment environment)
{
    private static readonly HashSet<string> Sections =
        ["vocabulary", "listening", "speaking", "reading", "writing", "grammar", "final-practice"];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task ImportAsync(string relativePath)
    {
        var path = Path.Combine(environment.ContentRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        await using var stream = File.OpenRead(path);
        var package = await JsonSerializer.DeserializeAsync<LearningContentPackage>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Learning content package '{relativePath}' is empty.");
        Validate(package);

        var course = await db.Courses.Include(x => x.Lessons).SingleOrDefaultAsync(x => x.Slug == package.CourseSlug)
            ?? throw new InvalidOperationException($"Course '{package.CourseSlug}' was not seeded before content import.");
        if (course.LanguageCode != package.Language || course.Level != package.Cefr)
            throw new InvalidOperationException($"Package '{package.ContentKey}' does not match its target course.");

        var lesson = await db.Lessons.Include(x => x.Exercises)
            .SingleOrDefaultAsync(x => x.CourseId == course.Id && x.Order == package.LessonNumber);
        if (lesson is null)
        {
            lesson = new Lesson
            {
                CourseId = course.Id,
                Title = package.LocalizedTitle,
                Description = package.Description,
                Category = package.LessonNumber == 0 ? "پایه" : "مقدماتی",
                Order = package.LessonNumber
            };
            db.Lessons.Add(lesson);
        }

        lesson.Title = package.LocalizedTitle;
        lesson.Description = package.Description;
        lesson.DurationMinutes = package.EstimatedDurationMinutes;
        lesson.Category = package.LessonNumber == 0 ? "پایه" : "مقدماتی";
        lesson.XpReward = package.LessonNumber == 0 ? 20 : 40;
        lesson.Icon = package.LessonNumber == 0 ? "🔤" : "👋";
        lesson.SectionTitle = package.LessonNumber == 0 ? "شروع پایه" : "یادگیری اصلی";
        lesson.SectionOrderJson = JsonSerializer.Serialize(package.SectionOrder);
        lesson.CanDoObjectivesJson = JsonSerializer.Serialize(package.CanDoObjectives);
        lesson.DeferredGrammarJson = JsonSerializer.Serialize(package.DeferredGrammar ?? []);
        lesson.AudioStatus = package.Audio.Status;
        lesson.AudioAssetUrl = package.Audio.AssetUrl;
        lesson.AudioScriptExerciseOrder = Array.FindIndex(package.Activities, x => x.Key == package.Audio.ScriptActivityKey) + 1;

        for (var index = 0; index < package.Activities.Length; index++)
        {
            var source = package.Activities[index];
            var order = index + 1;
            var activity = lesson.Exercises.SingleOrDefault(x => x.Order == order);
            if (activity is null)
            {
                activity = new Exercise { Lesson = lesson, Type = source.Type, Prompt = source.Prompt, Order = order };
                db.Exercises.Add(activity);
            }
            activity.Type = source.Type;
            activity.Kind = source.Kind;
            activity.SectionCode = source.Section;
            activity.Prompt = source.Prompt;
            activity.CorrectAnswer = source.CorrectAnswer;
            activity.OptionsJson = source.Options is null ? null : JsonSerializer.Serialize(source.Options);
            activity.Explanation = Feedback(source);
            activity.IsQuestionBankItem = false;
            activity.QuestionSkill = null;
        }
        foreach (var stale in lesson.Exercises.Where(x => !x.IsQuestionBankItem && x.Order > package.Activities.Length && x.Order < 1001).ToList())
            db.Exercises.Remove(stale);

        var questionBank = package.QuestionBank ?? [];
        var existingBankItems = lesson.Exercises.Where(x => x.IsQuestionBankItem).ToList();
        for (var index = 0; index < questionBank.Length; index++)
        {
            var source = questionBank[index];
            var order = 1001 + index;
            var item = existingBankItems.SingleOrDefault(x => x.Order == order);
            if (item is null)
            {
                item = new Exercise { Lesson = lesson, Type = source.Type, Prompt = source.Prompt, Order = order };
                db.Exercises.Add(item);
            }
            item.Type = source.Type;
            item.Kind = "Exercise";
            item.SectionCode = "final-practice";
            item.Prompt = source.Prompt;
            item.CorrectAnswer = source.CorrectAnswer;
            item.OptionsJson = source.Options is null ? null : JsonSerializer.Serialize(source.Options);
            item.Explanation = Feedback(source.CorrectFeedback, source.IncorrectFeedback);
            item.IsQuestionBankItem = true;
            item.QuestionSkill = source.Skill;
        }
        foreach (var stale in existingBankItems.Where(x => x.Order >= 1001 + questionBank.Length))
            db.Exercises.Remove(stale);

        var vocabulary = await db.VocabularyItems.Where(x => x.LessonId == lesson.Id).ToListAsync();
        for (var index = 0; index < package.Vocabulary.Length; index++)
        {
            var source = package.Vocabulary[index];
            var order = index + 1;
            var item = vocabulary.SingleOrDefault(x => x.Order == order);
            if (item is null)
            {
                item = new VocabularyItem
                {
                    Lesson = lesson,
                    Term = source.Target,
                    Meaning = source.Meaning,
                    Example = source.Example,
                    ExampleTranslation = source.ExampleTranslation,
                    Order = order
                };
                db.VocabularyItems.Add(item);
            }
            item.Term = source.Target;
            item.ContentType = source.Type;
            item.Pronunciation = source.Pronunciation;
            item.Meaning = source.Meaning;
            item.Example = source.Example;
            item.ExampleTranslation = source.ExampleTranslation;
            item.Note = source.Note is null ? source.Classification : $"{source.Note} · {source.Classification}";
        }

        await db.SaveChangesAsync();
    }

    public static void Validate(LearningContentPackage package)
    {
        if (string.IsNullOrWhiteSpace(package.ContentKey) || package.Language != "de" || package.Cefr != "A1" || package.LessonNumber < 0)
            throw new InvalidOperationException("Learning package metadata is invalid.");
        var minimumActivities = package.LessonNumber == 0 ? 9 : 20;
        if (package.CanDoObjectives.Length < 2 || package.Activities.Length < minimumActivities || package.Vocabulary.Length is < 8 or > 18)
            throw new InvalidOperationException("Learning package content coverage is incomplete or overloaded.");
        if (package.SectionOrder.Length != Sections.Count || package.SectionOrder.Distinct().Count() != Sections.Count || package.SectionOrder.Any(x => !Sections.Contains(x)))
            throw new InvalidOperationException("Lesson sections are invalid.");
        if (package.Activities.Select(x => x.Key).Distinct().Count() != package.Activities.Length || package.Vocabulary.Select(x => x.Key).Distinct().Count() != package.Vocabulary.Length)
            throw new InvalidOperationException("Learning content keys must be unique.");
        if (package.Activities.Any(x => !Sections.Contains(x.Section) || string.IsNullOrWhiteSpace(x.Prompt)))
            throw new InvalidOperationException("An activity has an invalid section or empty prompt.");
        if (package.Activities.Any(x => x.Kind == "Exercise" && x.Type is not ("speaking" or "writing" or "final-task") && string.IsNullOrWhiteSpace(x.CorrectAnswer)))
            throw new InvalidOperationException("A scored activity is missing its answer.");
        if (package.Activities.Any(x => x.Options is { Length: > 0 } && !x.Options.Contains(x.CorrectAnswer)))
            throw new InvalidOperationException("An activity answer is missing from its options.");
        if (!package.Activities.Any(x => x.Key == package.Audio.ScriptActivityKey))
            throw new InvalidOperationException("Audio script activity reference is invalid.");
        if (package.Audio.Status == "ready" && string.IsNullOrWhiteSpace(package.Audio.AssetUrl))
            throw new InvalidOperationException("Ready audio requires an asset URL.");
        if (package.Vocabulary.Any(x => string.IsNullOrWhiteSpace(x.Target) || string.IsNullOrWhiteSpace(x.Meaning) || string.IsNullOrWhiteSpace(x.Example)))
            throw new InvalidOperationException("A vocabulary item is incomplete.");
        var questionBank = package.QuestionBank ?? [];
        if (questionBank.Select(x => x.Key).Distinct().Count() != questionBank.Length ||
            questionBank.Any(x => string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Skill) || string.IsNullOrWhiteSpace(x.Prompt) || string.IsNullOrWhiteSpace(x.CorrectAnswer)))
            throw new InvalidOperationException("Question bank content is invalid.");
        if (questionBank.Any(x => x.Options is { Length: > 0 } && !x.Options.Contains(x.CorrectAnswer)))
            throw new InvalidOperationException("A question bank answer is missing from its options.");
    }

    private static string? Feedback(LearningActivitySource source)
    {
        if (source.CorrectFeedback is null && source.IncorrectFeedback is null) return null;
        if (source.IncorrectFeedback is null) return source.CorrectFeedback;
        return JsonSerializer.Serialize(new { correct = source.CorrectFeedback, incorrect = source.IncorrectFeedback });
    }

    private static string? Feedback(string? correct, string? incorrect)
    {
        if (correct is null && incorrect is null) return null;
        if (incorrect is null) return correct;
        return JsonSerializer.Serialize(new { correct, incorrect });
    }
}
