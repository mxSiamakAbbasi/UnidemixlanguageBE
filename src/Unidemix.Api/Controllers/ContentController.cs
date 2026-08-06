using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Controllers;

public sealed record ContentInput(string ExternalId, string Type, string Slug, string Title, string Summary, string Body,
    string Category, string Source, string? SourceUrl, string? ImageUrl, DateTimeOffset PublishedAt);

[ApiController, Route("api/content")]
public sealed class ContentController(AppDbContext db, IConfiguration config) : ControllerBase
{
    [HttpGet("{type}")]
    public Task<List<ContentItem>> List(string type, [FromQuery] int take = 30) => db.ContentItems.AsNoTracking()
        .Where(x => x.Type == type && x.IsPublished).OrderByDescending(x => x.PublishedAt).Take(Math.Clamp(take, 1, 100)).ToListAsync();

    [HttpGet("{type}/{slug}")]
    public async Task<IActionResult> Detail(string type, string slug) =>
        await db.ContentItems.AsNoTracking().SingleOrDefaultAsync(x => x.Type == type && x.Slug == slug && x.IsPublished) is { } item ? Ok(item) : NotFound();

    [HttpPost("ingest")]
    public async Task<IActionResult> Ingest([FromBody] List<ContentInput> items, [FromHeader(Name = "X-N8N-Key")] string? key)
    {
        if (key != config["N8n:ApiKey"]) return Unauthorized();
        foreach (var input in items)
        {
            var x = await db.ContentItems.SingleOrDefaultAsync(c => c.Type == input.Type && c.ExternalId == input.ExternalId);
            if (x is null) { x = new ContentItem { ExternalId=input.ExternalId, Type=input.Type, Slug=input.Slug, Title=input.Title, Summary=input.Summary, Body=input.Body }; db.ContentItems.Add(x); }
            x.Slug=input.Slug;x.Title=input.Title;x.Summary=input.Summary;x.Body=input.Body;x.Category=input.Category;x.Source=input.Source;x.SourceUrl=input.SourceUrl;x.ImageUrl=input.ImageUrl;x.PublishedAt=input.PublishedAt;x.IsPublished=true;
        }
        await db.SaveChangesAsync(); return Ok(new { received = items.Count });
    }
}
