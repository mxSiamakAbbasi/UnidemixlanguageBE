using System.Text.RegularExpressions;

namespace Unidemix.Api.Services;

public static partial class ExamContentQualityValidator
{
    private static readonly string[] EnglishInstructionFragments =
        ["choose the", "read the", "listen to", "write a", "select the", "answer the"];

    public static void ValidateGermanMock(IEnumerable<ExamTaskSource> tasks)
    {
        foreach (var task in tasks)
        {
            var learnerContent = new List<(string Field, string? Value)>
            {
                ("prompt", task.Prompt),
                ("sourceText", task.SourceText),
                ("script", task.Script),
                ("correctAnswer", task.CorrectAnswer),
            };
            if (task.Options is not null)
                learnerContent.AddRange(task.Options.Select((value, index) => ($"option[{index}]", (string?)value)));
            if (task.FormFields is not null)
                learnerContent.AddRange(task.FormFields.SelectMany(field => new[]
                {
                    ($"formField[{field.Key}].label", (string?)field.Label),
                    ($"formField[{field.Key}].correctAnswer", field.CorrectAnswer),
                }));

            foreach (var (field, value) in learnerContent.Where(item => !string.IsNullOrWhiteSpace(item.Value)))
            {
                if (PersianScript().IsMatch(value!))
                    throw new InvalidOperationException($"{task.Key}/{field}: Persian is not allowed in German mock content.");
                if (PlaceholderContent().IsMatch(value!))
                    throw new InvalidOperationException($"{task.Key}/{field}: placeholder or template content is not publishable.");
                if (EnglishInstructionFragments.Any(fragment => value!.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"{task.Key}/{field}: English task instructions are not allowed in German mock content.");
            }

            if (task.Section == "listening" && string.IsNullOrWhiteSpace(task.Script))
                throw new InvalidOperationException($"{task.Key}: a playable listening script is required.");
            if (task.Section != "listening" && task.Type is not "writing" and not "speaking" &&
                string.IsNullOrWhiteSpace(task.SourceText) && !TaskContainsLanguageMaterial(task))
                throw new InvalidOperationException($"{task.Key}: objective task has no learner-facing source material.");
        }
    }

    private static bool TaskContainsLanguageMaterial(ExamTaskSource task) =>
        task.Section == "language-elements" && task.Prompt.Length >= 30;

    [GeneratedRegex("[\\u0600-\\u06ff]")]
    private static partial Regex PersianScript();

    [GeneratedRegex("Quelle\\s+[A-Z]\\s*:\\s*Information\\s+\\d+|Originaler\\s+Unidemix|niveaugerechte\\s+Information|Situation\\s+\\d+\\s*:\\s*\\.{3}|slot|template label", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderContent();
}
