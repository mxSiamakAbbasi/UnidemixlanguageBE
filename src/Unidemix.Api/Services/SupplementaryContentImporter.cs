using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Services;

public sealed record SupplementaryPackage(string Version, SupplementarySource[] Modules);
public sealed record SupplementarySource(string ContentKey, string LanguageCode, string CefrLevel, string Category, string Title, string Description, int Order, SupplementaryItem[] Items);
public sealed record SupplementaryItem(string Key, string Title, string Kind, string GermanText, string? PersianSupport, string? Incorrect, string? Correct, string? Prompt, string[]? Options, string? CorrectAnswer, string? Explanation, string? GroupKey = null, string? GroupLabel = null, string[]? Chunks = null, string? EvidenceTag = null);

public sealed class SupplementaryContentImporter(AppDbContext db, IWebHostEnvironment environment)
{
    private static readonly string[] Levels = ["A1", "A2", "B1", "B2", "C1"];
    public static readonly string[] Categories = ["PracticalGrammar", "EverydayGerman", "WorkGerman", "ShortStories", "CommonMistakes"];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task ImportAsync(string relativePath)
    {
        await using var stream = File.OpenRead(Path.Combine(environment.ContentRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var package = await JsonSerializer.DeserializeAsync<SupplementaryPackage>(stream, JsonOptions) ?? throw new InvalidOperationException("Supplementary package is empty.");
        Validate(package);
        foreach (var source in package.Modules)
        {
            var module = await db.SupplementaryModules.SingleOrDefaultAsync(x => x.ContentKey == source.ContentKey);
            if (module is null) { module = new SupplementaryModule { ContentKey = source.ContentKey, CefrLevel = source.CefrLevel, Category = source.Category, Title = source.Title, Description = source.Description, ItemsJson = "[]" }; db.SupplementaryModules.Add(module); }
            module.LanguageCode = source.LanguageCode; module.CefrLevel = source.CefrLevel; module.Category = source.Category;
            module.Title = source.Title; module.Description = source.Description; module.Order = source.Order;
            module.ItemsJson = JsonSerializer.Serialize(source.Items); module.ContentVersion = package.Version; module.IsPublished = true;
        }
        await db.SaveChangesAsync();
    }

    public static void Validate(SupplementaryPackage package)
    {
        if (string.IsNullOrWhiteSpace(package.Version) || package.Modules.Length != 25) throw new InvalidOperationException("Supplementary package must contain five categories for every level.");
        foreach (var level in Levels)
            foreach (var category in Categories)
                if (package.Modules.Count(x => x.LanguageCode == "de" && x.CefrLevel == level && x.Category == category) != 1) throw new InvalidOperationException($"Missing {level}/{category}.");
        if (package.Modules.Select(x => x.ContentKey).Distinct().Count() != package.Modules.Length || package.Modules.Any(x => x.Items.Length == 0 || x.Items.Select(i => i.Key).Distinct().Count() != x.Items.Length))
            throw new InvalidOperationException("Supplementary keys/items are invalid.");
        var forbidden = new[] { "placeholder", "situation 1", "example phrase", "information 5", "originaler unidemix-text", "slot" };
        if (package.Modules.SelectMany(x => x.Items).Any(x => string.IsNullOrWhiteSpace(x.GermanText) || forbidden.Any(term => x.GermanText.Contains(term, StringComparison.OrdinalIgnoreCase)) || x.Options is { Length: > 0 } && (x.Options.Distinct().Count() != x.Options.Length || !x.Options.Contains(x.CorrectAnswer))))
            throw new InvalidOperationException("Supplementary content quality validation failed.");
        if (package.Modules.Any(x => x.Items.Length < 6)) throw new InvalidOperationException("Every supplementary category needs beta-level content breadth.");
        if (package.Modules.Any(x => x.Items.Select(i => i.Title.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != x.Items.Length
            || x.Items.Select(i => i.GermanText.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != x.Items.Length))
            throw new InvalidOperationException("Supplementary topics and learner content must be distinct within each category.");
        if (package.Modules.SelectMany(x => x.Items).Any(x => x.Options is not { Length: >= 2 }
            || string.IsNullOrWhiteSpace(x.CorrectAnswer) || string.IsNullOrWhiteSpace(x.Explanation)))
            throw new InvalidOperationException("Every supplementary item needs deterministic practice and feedback.");
        var workItems = package.Modules.Where(x => x.Category == "WorkGerman").SelectMany(x => x.Items);
        if (workItems.Any(x => string.IsNullOrWhiteSpace(x.GroupKey) || string.IsNullOrWhiteSpace(x.GroupLabel) || x.Chunks is not { Length: >= 2 }))
            throw new InvalidOperationException("Every Work German item needs a data-driven profession group and phrase bank.");
        foreach (var level in Levels)
        {
            var levelModules = package.Modules.Where(x => x.CefrLevel == level).ToArray();
            if (levelModules.Any(x => x.Category != "WorkGerman" && x.Items.Length < 8)
                || levelModules.Single(x => x.Category == "WorkGerman").Items.Length < 18)
                throw new InvalidOperationException($"Supplementary beta depth is incomplete for {level}.");
            if (levelModules.Single(x => x.Category == "WorkGerman").Items.Select(x => x.GroupKey).Distinct().Count() < 12)
                throw new InvalidOperationException($"Work German profession taxonomy is incomplete for {level}.");
        }
        var b1Care = package.Modules.Single(x => x.CefrLevel == "B1" && x.Category == "WorkGerman").Items.Count(x => x.GroupKey == "care");
        if (b1Care < 6) throw new InvalidOperationException("B1 Pflege needs a substantial dialogue bank.");
    }
}
