using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Services;

namespace Unidemix.Api.Controllers;

[ApiController, Authorize, Route("api/learning/supplementary")]
public sealed class SupplementaryLearningController(AppDbContext db, SupplementaryAiCapabilityPolicy aiPolicy) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string languageCode = "de", [FromQuery] string? level = null)
    {
        var query = db.SupplementaryModules.AsNoTracking().Where(x => x.IsPublished && x.LanguageCode == languageCode);
        if (!string.IsNullOrWhiteSpace(level)) query = query.Where(x => x.CefrLevel == level);
        var modules = await query.OrderBy(x => x.CefrLevel).ThenBy(x => x.Order).ToListAsync();
        return Ok(modules.Select(x => new { x.Id, x.ContentKey, x.LanguageCode, x.CefrLevel, x.Category, x.Title, x.Description, x.Order, Items = JsonSerializer.Deserialize<SupplementaryItem[]>(x.ItemsJson) ?? [], x.ContentVersion,
            Ai = aiPolicy.Resolve(x.Category, x.LanguageCode, x.CefrLevel, x.Title, x.ItemsJson) }));
    }
}
