namespace Unidemix.Api.Models;

public sealed class SupplementaryModule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ContentKey { get; set; }
    public string LanguageCode { get; set; } = "de";
    public required string CefrLevel { get; set; }
    public required string Category { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public int Order { get; set; }
    public required string ItemsJson { get; set; }
    public string ContentVersion { get; set; } = "1.0.0";
    public bool IsPublished { get; set; } = true;
}
