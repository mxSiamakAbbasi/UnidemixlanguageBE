using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Unidemix.Api.Services;

public sealed class ExamGenerationValidator
{
    private static readonly string[] UnsafeMarkers = ["<script", "javascript:", "data:text/html", "ignore previous instructions"];

    public ExamValidationResult Validate(ExamBlueprintDocument blueprint, GeneratedExamEnvelope output,
        string mode, string? sectionKey, string? partKey, IEnumerable<string> excludedFingerprints)
    {
        var issues = new List<ExamValidationIssue>();
        Add(output.SchemaVersion != "exam-generation-v2", "schema", "Unsupported generation schema.");
        Add(output.Provider != blueprint.Provider, "provider", "Provider does not match the locked blueprint.");
        Add(output.ExamKey != blueprint.ExamKey || output.Variant != blueprint.Variant, "exam", "Exam or variant does not match the locked blueprint.");
        Add(output.Cefr != blueprint.Cefr, "cefr", "CEFR does not match the locked blueprint.");
        Add(output.BlueprintVersion != blueprint.Version, "version", "Blueprint version does not match.");
        Add(output.Mode != mode, "mode", "Generation mode does not match the request.");
        Add(blueprint.Status != "Validated", "blueprint-status", "Only Validated blueprints may generate content.");

        var expectedParts = ExamTemplateAssembler.Select(blueprint, mode, sectionKey, partKey);
        Add(expectedParts.Length == 0, "selection", "Requested section or part does not exist in the blueprint.");
        Add(output.Tasks.Length != expectedParts.Length, "completeness", "Generated content does not contain every required blueprint slot exactly once.");

        foreach (var expected in expectedParts)
        {
            var matches = output.Tasks.Where(t => t.Section == expected.Section.Key && t.Part == expected.Part.Key).ToArray();
            Add(matches.Length != 1, "part", $"Part {expected.Section.Key}/{expected.Part.Key} is missing or duplicated.");
            if (matches.Length != 1) continue;
            var task = matches[0];
            Add(task.Type != expected.Part.TaskType, "task-type", $"Wrong task type for {task.Part}.");
            Add(task.ItemCount != expected.Part.ItemCount, "item-count", $"Wrong item count for {task.Part}.");
            Add(task.OptionCount != expected.Part.OptionCount, "option-count", $"Wrong option count for {task.Part}.");
            Add(task.ResponseType != expected.Part.ResponseType, "response-type", $"Wrong response type for {task.Part}.");
            Add(task.SectionOrder != expected.Section.Order || task.SectionTimingMinutes != expected.Section.TimingMinutes || task.PartOrder != expected.Part.Order, "order-timing", $"Section/part order or timing changed for {task.Part}.");
            Add(task.TimingMinutes != expected.Part.TimingMinutes, "timing", $"Wrong part timing for {task.Part}.");
            Add(task.ScoringRule != expected.Part.ScoringRule, "scoring", $"Wrong scoring rule for {task.Part}.");
            Add(expected.Part.ResponseType == "objective" && task.Questions.Length != expected.Part.ItemCount, "question-count", $"Wrong question count for {task.Part}.");
            Add(string.IsNullOrWhiteSpace(task.Prompt), "prompt", $"Prompt is missing for {task.Part}.");
            Add(task.PromptPointCount != expected.Part.GenerationConstraints.RequiredPromptPoints, "prompt-structure", $"Writing/speaking prompt structure is invalid for {task.Part}.");
            Add(task.PlaybackCount != expected.Part.PlaybackRule?.Plays, "playback", $"Playback rule is invalid for {task.Part}.");
            Add(expected.Part.PlaybackRule is not null && string.IsNullOrWhiteSpace(task.Script), "listening-script", $"Listening script is missing for {task.Part}.");
            Add(task.Options is { Length: > 0 } && task.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != task.Options.Length, "duplicate-options", $"Duplicate answer options in {task.Part}.");
            Add(task.Options is { Length: > 0 } && (string.IsNullOrWhiteSpace(task.CorrectAnswer) || !task.Options.Contains(task.CorrectAnswer)), "answer", $"Correct answer is missing or invalid in {task.Part}.");
            foreach (var question in task.Questions)
            {
                Add(expected.Part.OptionCount > 0 && question.Options?.Length != expected.Part.OptionCount, "option-count", $"Question {question.Key} has the wrong option count.");
                Add(question.Options is { Length: > 0 } && question.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != question.Options.Length, "duplicate-options", $"Duplicate options in question {question.Key}.");
                Add(question.Options is { Length: > 0 } && (string.IsNullOrWhiteSpace(question.CorrectAnswer) || !question.Options.Contains(question.CorrectAnswer)), "answer", $"Correct answer is invalid in question {question.Key}.");
            }
            var measured = (task.SourceText ?? task.Script ?? task.Prompt).Length;
            Add(measured < expected.Part.GenerationConstraints.MinTextLength || measured > expected.Part.GenerationConstraints.MaxTextLength,
                "length", $"Generated text length is outside the blueprint range for {task.Part}.");
            Add(UnsafeMarkers.Any(m => JsonSerializer.Serialize(task).Contains(m, StringComparison.OrdinalIgnoreCase)), "safety", $"Unsafe generated content in {task.Part}.");
            Add(ContainsEnglishUiLeakage(task.Prompt), "language-leakage", $"English UI metadata leaked into {task.Part}.");
            Add(!MatchesCefr(task, blueprint.Cefr), "cefr-complexity", $"Generated content exceeds the configured CEFR heuristic for {task.Part}.");
        }

        var unexpected = output.Tasks.Where(t => !expectedParts.Any(p => p.Section.Key == t.Section && p.Part.Key == t.Part)).ToArray();
        Add(unexpected.Length > 0, "extra-part", "Generated content contains a part outside the selected blueprint.");
        Add(StructuralSignature(blueprint,mode,sectionKey,partKey) != StructuralSignature(output), "structure-signature",
            "Generated structure is not equivalent to the immutable fixed template.");
        if (mode == "FullMockExam")
            Add(!output.Tasks.Select(t => t.Section).Distinct().SequenceEqual(blueprint.Scoring.SectionOrder), "section-order", "Full Mock section order differs from the blueprint.");

        var fingerprint = Fingerprint(output);
        Add(excludedFingerprints.Contains(fingerprint, StringComparer.OrdinalIgnoreCase), "duplicate", "Generated content duplicates a stored or recent task.");
        return new ExamValidationResult(issues.Count == 0, issues.ToArray(), fingerprint);

        void Add(bool condition, string code, string message) { if (condition) issues.Add(new(code, message)); }
    }

    public static string StructuralSignature(ExamBlueprintDocument blueprint, string mode, string? sectionKey, string? partKey)
    {
        var parts = ExamTemplateAssembler.Select(blueprint, mode, sectionKey, partKey);
        var value = string.Join("|", parts.Select(x => $"{x.Section.Order}:{x.Section.Key}:{x.Section.TimingMinutes}:{x.Part.Order}:{x.Part.Key}:{x.Part.TaskType}:{x.Part.ItemCount}:{x.Part.OptionCount}:{x.Part.ResponseType}:{x.Part.PlaybackRule?.Plays}:{x.Part.TimingMinutes}:{x.Part.ScoringRule}:{string.Join(',',x.Part.ContentSlots.Select(s=>s.Key))}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    public static string StructuralSignature(GeneratedExamEnvelope output)
    {
        var value = string.Join("|", output.Tasks.Select(x => $"{x.SectionOrder}:{x.Section}:{x.SectionTimingMinutes}:{x.PartOrder}:{x.Part}:{x.Type}:{x.ItemCount}:{x.OptionCount}:{x.ResponseType}:{x.PlaybackCount}:{x.TimingMinutes}:{x.ScoringRule}:{string.Join(',',x.ContentSlotKeys)}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    public static string Fingerprint(GeneratedExamEnvelope output)
    {
        var content = string.Join('|', output.Tasks.Select(t => $"{t.Prompt}|{t.SourceText}|{t.Script}|{string.Join('|', t.Options ?? [])}"));
        var normalized = Regex.Replace(content.ToLowerInvariant(), @"\b\d+\b", "#");
        normalized = Regex.Replace(normalized, @"\b[A-ZÄÖÜ][a-zäöüß]{2,}\b", "name", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static bool ContainsEnglishUiLeakage(string text) =>
        Regex.IsMatch(text, @"\b(click|submit|next|previous|correct answer|question)\b", RegexOptions.IgnoreCase);

    private static bool MatchesCefr(GeneratedExamTask task, string cefr)
    {
        var text = task.SourceText ?? task.Script ?? task.Prompt;
        var sentences = Regex.Split(text, @"[.!?]+", RegexOptions.CultureInvariant).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (sentences.Length == 0) return true;
        var averageWords = sentences.Average(x => Regex.Matches(x, @"\p{L}+").Count);
        return cefr switch { "A1" => averageWords <= 15, "A2" => averageWords <= 20, "B1" => averageWords <= 28, "B2" => averageWords <= 36, _ => averageWords <= 50 };
    }
}
