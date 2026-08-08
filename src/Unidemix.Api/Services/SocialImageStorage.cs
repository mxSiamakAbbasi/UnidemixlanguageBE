namespace Unidemix.Api.Services;

public sealed record StoredSocialImage(string StorageKey, string ImageUrl, string? ThumbnailUrl = null);

public interface ISocialImageStorage
{
    Task<StoredSocialImage> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken);
    string GetPublicUrl(string storageKey);
}

public sealed class LocalSocialImageStorage(IWebHostEnvironment environment, IConfiguration configuration) : ISocialImageStorage
{
    private readonly string root = configuration["SocialStorage:RootPath"]
        ?? Path.Combine(environment.ContentRootPath, "uploads", "social");

    public async Task<StoredSocialImage> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        var storageKey = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}.{extension.TrimStart('.').ToLowerInvariant()}";
        var fullPath = FullPath(storageKey); Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await content.CopyToAsync(output, cancellationToken);
        return new StoredSocialImage(storageKey, GetPublicUrl(storageKey));
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult(IsSafeKey(storageKey) && File.Exists(FullPath(storageKey)));
    public string GetPublicUrl(string storageKey) => $"/uploads/social/{storageKey.Replace('\\', '/')}";
    private string FullPath(string storageKey) => Path.Combine(root, storageKey.Replace('/', Path.DirectorySeparatorChar));
    private static bool IsSafeKey(string key) => !string.IsNullOrWhiteSpace(key) && !Path.IsPathRooted(key) && !key.Contains("..", StringComparison.Ordinal) && key.All(c => char.IsLetterOrDigit(c) || c is '/' or '-' or '_' or '.');
}
