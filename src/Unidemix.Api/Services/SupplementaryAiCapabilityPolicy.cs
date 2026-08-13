namespace Unidemix.Api.Services;

public static class SupplementaryAiCapabilities
{
    public const string ContextualTutor = "SupplementaryContextualTutor";
}

public sealed class SupplementaryAiCapabilityPolicy(IConfiguration configuration)
{
    private static readonly IReadOnlyDictionary<string, string[]> Actions = new Dictionary<string, string[]>
    {
        ["PracticalGrammar"] = ["ExplainMore", "MoreExamples", "MorePractice", "ExplainMistake"],
        ["EverydayGerman"] = ["SimilarSentence", "NaturalRephrase", "ScenarioRoleplay", "MorePractice"],
        ["WorkGerman"] = ["ConversationPractice", "CorrectSentence", "FormalRephrase", "MoreJobPhrases"],
        ["ShortStories"] = ["ExplainStory", "ExplainVocabulary", "MoreQuestions", "DiscussStory"],
        ["CommonMistakes"] = ["ExplainMistake", "MoreExamples", "MorePractice", "SimilarMistake"]
    };

    public object Resolve(string category, string language, string cefr, string topic, string content)
    {
        if (!Actions.TryGetValue(category, out var actions)) throw new InvalidOperationException("Unsupported supplementary AI category.");
        var maxContextCharacters = configuration.GetValue<int>($"SupplementaryAiCapabilities:{SupplementaryAiCapabilities.ContextualTutor}:MaxContextCharacters");
        var boundedContent = maxContextCharacters > 0 && content.Length > maxContextCharacters
            ? content[..maxContextCharacters]
            : content;

        return new
        {
            Capability = SupplementaryAiCapabilities.ContextualTutor,
            EnabledPlans = configuration.GetSection($"SupplementaryAiCapabilities:{SupplementaryAiCapabilities.ContextualTutor}:EnabledPlans").Get<string[]>() ?? [],
            Context = new { Language = language, Cefr = cefr, Category = category, Topic = topic, CurrentContent = boundedContent },
            Actions = actions,
            Scope = "GermanLanguageLearningOnly",
            OutOfScopeResponse = "من اینجا برای تمرین زبان آلمانی در همین موضوع همراهت هستم.",
            MaxContextCharacters = maxContextCharacters
        };
    }
}
