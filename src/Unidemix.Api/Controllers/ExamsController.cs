using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;

namespace Unidemix.Api.Controllers;

[ApiController, Authorize, Route("api/exams")]
public sealed class ExamsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Catalog([FromQuery] string languageCode, [FromQuery] string level)
    {
        var providers = await db.ExamProviders.AsNoTracking()
            .Where(x => x.IsActive && x.LanguageCode == languageCode)
            .Select(x => new
            {
                x.Id, x.Code, x.Name, x.LanguageCode,
                Programs = x.Programs.Where(p => p.IsActive && p.LevelMappings.Any(m => m.CefrLevel == level)).Select(p => new
                {
                    p.Id, p.Code, p.Level, p.Name,
                    CefrMappings = p.LevelMappings.OrderBy(m => m.CefrLevel).Select(m => new { m.CefrLevel, m.IsApproximate }),
                    Sections = p.Sections.OrderBy(s => s.Order).Select(s => new { s.Id, s.Code, s.Name, s.Order })
                })
            })
            .Where(x => x.Programs.Any()).OrderBy(x => x.Name).ToListAsync();
        return Ok(providers);
    }
}
