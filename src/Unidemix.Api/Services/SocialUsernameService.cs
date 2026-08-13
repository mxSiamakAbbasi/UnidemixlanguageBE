using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Unidemix.Api.Data;
using Unidemix.Api.Models;

namespace Unidemix.Api.Services;

public interface ISocialUsernameService
{
    string Normalize(string value);
    Task<string> GenerateAsync(string email, string displayName, CancellationToken cancellationToken = default);
    Task SaveNewProfileAsync(SocialProfile profile, string email, string displayName, CancellationToken cancellationToken = default);
}

public sealed partial class SocialUsernameService(AppDbContext db) : ISocialUsernameService
{
    private const int MaximumLength = 24;
    private static readonly HashSet<string> Reserved = ["admin", "administrator", "support", "unidemix", "system", "moderator"];

    public string Normalize(string value) => InvalidCharacters()
        .Replace(RemoveMarks(value).Trim().TrimStart('@').ToLowerInvariant(), "")
        .Trim('.', '_');

    public async Task<string> GenerateAsync(string email, string displayName, CancellationToken cancellationToken = default)
    {
        var baseName = BaseName(email, displayName);
        if (!await Exists(baseName, cancellationToken)) return baseName;

        for (var attempt = 0; attempt < 32; attempt++)
        {
            var suffix = RandomNumberGenerator.GetInt32(100, 1000).ToString();
            var candidate = WithSuffix(baseName, suffix);
            if (!await Exists(candidate, cancellationToken)) return candidate;
        }

        return WithSuffix(baseName, RandomNumberGenerator.GetInt32(1000, 1_000_000).ToString());
    }

    public async Task SaveNewProfileAsync(SocialProfile profile, string email, string displayName, CancellationToken cancellationToken = default)
    {
        if (db.Entry(profile).State == EntityState.Detached) db.SocialProfiles.Add(profile);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            profile.Username = await GenerateAsync(email, displayName, cancellationToken);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException exception) when (IsUsernameCollision(exception) && attempt < 7)
            {
                // A concurrent request claimed the candidate after the availability check.
            }
        }
    }

    private string BaseName(string email, string displayName)
    {
        var at = email.IndexOf('@');
        var localPart = at > 0 ? email[..at] : "";
        var candidate = Normalize(localPart);
        if (!IsUsable(candidate)) candidate = Normalize(displayName.Replace(' ', '.'));
        if (!IsUsable(candidate)) candidate = $"learner{RandomNumberGenerator.GetInt32(1000, 10000)}";
        return candidate[..Math.Min(candidate.Length, MaximumLength)];
    }

    private static bool IsUsable(string value) => value.Length >= 3 && !Reserved.Contains(value);
    private static string WithSuffix(string baseName, string suffix) =>
        $"{baseName[..Math.Min(baseName.Length, MaximumLength - suffix.Length)]}{suffix}";
    private Task<bool> Exists(string username, CancellationToken cancellationToken) =>
        db.SocialProfiles.AsNoTracking().AnyAsync(x => x.Username == username, cancellationToken);
    private static bool IsUsernameCollision(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_SocialProfiles_Username" };
    private static string RemoveMarks(string value) => new(value.Normalize(NormalizationForm.FormD)
        .Where(x => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(x) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());

    [GeneratedRegex("[^a-z0-9_.]")]
    private static partial Regex InvalidCharacters();
}
