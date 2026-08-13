namespace Unidemix.Api.Services;

public sealed class ExamAiCapabilityPolicy(IConfiguration configuration)
{
    public IReadOnlyDictionary<string, bool> Resolve(string planCode)
    {
        var capabilities = new[] { ExamAiCapabilities.PartGeneration, ExamAiCapabilities.SectionGeneration,
            ExamAiCapabilities.MockGeneration, ExamAiCapabilities.WritingReview, ExamAiCapabilities.SpeakingReview, ExamAiCapabilities.Examiner };
        return capabilities.ToDictionary(x => x, x => configuration.GetValue<bool>($"ExamAiCapabilities:Plans:{planCode}:{x}"));
    }
}
