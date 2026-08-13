namespace Unidemix.Api.Services;

public sealed class ExamTemplateAssembler
{
    public ExamAiContext Contract(ExamBlueprintDocument blueprint, string mode, string? sectionKey, string? partKey,
        string[] previousFingerprints)
    {
        var selected = Select(blueprint, mode, sectionKey, partKey);
        return new(blueprint.Provider, blueprint.ExamKey, blueprint.Variant, blueprint.Cefr, blueprint.Version, mode,
            selected.Select(x => new FixedExamPartContract(x.Section.Key, x.Section.Order, x.Section.TimingMinutes, x.Part.Key, x.Part.Order, x.Part.TaskType, x.Part.ItemCount,
                x.Part.OptionCount, x.Part.ResponseType, x.Part.PlaybackRule, x.Part.TimingMinutes, x.Part.ScoringRule,
                x.Part.GenerationConstraints, x.Part.DifficultyProfile, x.Part.ContentSlots)).ToArray(), previousFingerprints);
    }

    public GeneratedExamEnvelope Assemble(ExamBlueprintDocument blueprint, ExamAiContext contract,
        GeneratedContentReplacementEnvelope generated)
    {
        if (generated.SchemaVersion != "exam-content-replacements-v1")
            throw new InvalidOperationException("Unsupported replacement schema.");
        var replacements = generated.Replacements.GroupBy(x => x.SlotKey).ToDictionary(x => x.Key, x => x.ToArray());
        if (replacements.Any(x => x.Value.Length != 1)) throw new InvalidOperationException("Duplicate replacement slot.");
        var allowed = contract.Parts.SelectMany(x => x.ContentSlots).Select(x => x.Key).ToHashSet();
        if (replacements.Keys.Any(x => !allowed.Contains(x))) throw new InvalidOperationException("Replacement contains an unknown slot.");
        if (allowed.Any(x => !replacements.ContainsKey(x))) throw new InvalidOperationException("A required template slot was not filled.");

        var tasks = contract.Parts.Select(part =>
        {
            string? Text(string type) => part.ContentSlots.Where(x => x.Type == type).Select(x => replacements[x.Key][0].Text).FirstOrDefault();
            var questions = part.ContentSlots.Where(x => x.Type == "question").OrderBy(x => x.ItemIndex).Select(slot =>
            {
                var value = replacements[slot.Key][0];
                return new GeneratedExamQuestion(slot.Key, value.Text ?? "", value.Options, value.CorrectAnswer);
            }).ToArray();
            var prompt = Text("scenario") ?? questions.FirstOrDefault()?.Prompt ?? "";
            return new GeneratedExamTask($"generated-{part.Section}-{part.Part}", part.Section, part.Part, part.TaskType,
                prompt, Text("reading-text"), Text("listening-script"), Text("speaker-metadata"), part.PlaybackRule?.ApproximateDurationSeconds,
                part.PlaybackRule?.Plays, part.PlaybackRule is null ? null : "script-ready-audio-deferred",
                questions.Length == 1 ? questions[0].CorrectAnswer : null, questions.Length == 1 ? questions[0].Options : null,
                0, part.ResponseType == "productive", part.QuestionCount,
                part.Constraints.RequiredPromptPoints, part.OptionCount, part.ResponseType, part.SectionOrder,
                part.SectionTimingMinutes, part.PartOrder, part.TimingMinutes, part.ScoringRule,
                part.ContentSlots.Select(x=>x.Key).ToArray(), questions);
        }).ToArray();
        return new("exam-generation-v2", blueprint.Provider, blueprint.ExamKey, blueprint.Variant, blueprint.Cefr,
            blueprint.Version, contract.Mode, tasks);
    }

    public static (ExamSectionBlueprint Section, ExamPartBlueprint Part)[] Select(ExamBlueprintDocument blueprint,
        string mode, string? sectionKey, string? partKey) => blueprint.Sections
        .Where(s => mode == "FullMockExam" || s.Key == sectionKey)
        .SelectMany(s => s.Parts.Where(p => mode != "PartPractice" || p.Key == partKey).Select(p => (s, p))).ToArray();
}
