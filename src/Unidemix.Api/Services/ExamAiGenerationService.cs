using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Services;

public sealed class ExamAiGenerationService(AppDbContext db, IExamAiContentProvider provider,
    ExamGenerationValidator validator, ExamTemplateAssembler assembler, ExamAiCapabilityPolicy capabilityPolicy)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<(GeneratedExamContent? Content, string? Error, int StatusCode)> GenerateAsync(Guid programId, Guid userId,
        ExamAiGenerationRequest request, CancellationToken cancellationToken)
    {
        var blueprintEntity = await db.ExamBlueprints.Include(x => x.Program).ThenInclude(x => x.Provider)
            .Where(x => x.ExamProgramId == programId).OrderByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken);
        if (blueprintEntity is null) return (null, "Exam blueprint was not found.", 404);
        if (blueprintEntity.Status != "Validated") return (null, "AI generation is blocked for a provisional blueprint.", 409);
        var capability = request.Mode switch { "PartPractice" => ExamAiCapabilities.PartGeneration, "SectionPractice" => ExamAiCapabilities.SectionGeneration, "FullMockExam" => ExamAiCapabilities.MockGeneration, _ => "" };
        if (capability.Length == 0) return (null, "Unsupported generation mode.", 400);
        var planCode = await ActivePlanCode(userId, cancellationToken);
        if (!capabilityPolicy.Resolve(planCode).GetValueOrDefault(capability)) return (null, "This plan does not include the requested exam AI capability.", 403);
        if (!provider.IsConfigured) return (null, "AI exam generation is not configured on this environment.", 503);

        var blueprint = JsonSerializer.Deserialize<ExamBlueprintDocument>(blueprintEntity.DefinitionJson, JsonOptions)!;
        var selected = blueprint.Sections.SingleOrDefault(x => x.Key == request.SectionKey);
        var part = selected?.Parts.SingleOrDefault(x => x.Key == request.PartKey);
        if (request.Mode != "FullMockExam" && selected is null) return (null, "Section does not exist in the blueprint.", 400);
        if (request.Mode == "PartPractice" && part is null) return (null, "Part does not exist in the blueprint.", 400);
        var previous = await db.GeneratedExamContents.AsNoTracking().Where(x => x.Fingerprint != null).OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Fingerprint!).Take(100).ToArrayAsync(cancellationToken);
        var context = assembler.Contract(blueprint, request.Mode, request.SectionKey, request.PartKey, previous);
        var entity = new GeneratedExamContent { ExamBlueprintId = blueprintEntity.Id, UserId = userId, Mode = request.Mode,
            SectionKey = request.SectionKey, PartKey = request.PartKey, Status = "Generating", ModelReference = provider.ProviderReference };
        db.GeneratedExamContents.Add(entity); await db.SaveChangesAsync(cancellationToken);
        try
        {
            var replacements = await provider.GenerateAsync(context, cancellationToken);
            var output = assembler.Assemble(blueprint, context, replacements);
            entity.Status = "Validating"; await db.SaveChangesAsync(cancellationToken);
            var result = validator.Validate(blueprint, output, request.Mode, request.SectionKey, request.PartKey, previous);
            entity.ValidatorResultJson = JsonSerializer.Serialize(result);
            entity.Fingerprint = result.Fingerprint;
            if (!result.IsValid)
            {
                entity.Status = "Rejected"; entity.RejectionReason = string.Join("; ", result.Issues.Select(x => x.Code));
                await db.SaveChangesAsync(cancellationToken); return (entity, entity.RejectionReason, 422);
            }
            entity.ContentJson = JsonSerializer.Serialize(output); entity.Status = "Ready"; entity.ReadyAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken); return (entity, null, 200);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            entity.Status = "Rejected"; entity.RejectionReason = "provider-error";
            entity.ValidatorResultJson = JsonSerializer.Serialize(new { error = ex.GetType().Name });
            await db.SaveChangesAsync(cancellationToken); return (entity, "AI provider generation failed.", 502);
        }
    }

    private async Task<string> ActivePlanCode(Guid userId, CancellationToken cancellationToken)
    {
        var active = await db.UserSubscriptions.AsNoTracking().Include(x => x.Plan)
            .Where(x => x.UserId == userId && x.Status == "Active" && x.EndsAt > DateTimeOffset.UtcNow)
            .OrderByDescending(x => x.EndsAt).Select(x => x.Plan.Code).FirstOrDefaultAsync(cancellationToken);
        return active ?? "free";
    }
}
