using Microsoft.Extensions.Logging;

namespace ADSUS_BE.DAL.ExternalServices;

/// <summary>
/// No-operation implementation khi SupabaseStorage chưa được cấu hình.
/// Trả về giá trị null/rỗng thay vì throw exception để app vẫn hoạt động.
/// </summary>
public sealed class NoOpFileStorageService : IFileStorageService
{
    private readonly ILogger<NoOpFileStorageService> _logger;

    public NoOpFileStorageService(ILogger<NoOpFileStorageService> logger)
    {
        _logger = logger;
    }

    public Task<string> UploadAsync(Stream content, string objectPath, string contentType, CancellationToken ct = default)
    {
        _logger.LogWarning("NoOpFileStorageService: Upload called but SupabaseStorage is not configured. ObjectPath={Path}", objectPath);
        return Task.FromResult(objectPath);
    }

    public Task<string> UploadAsync(Stream content, string objectPath, string contentType, string bucketName, CancellationToken ct = default)
    {
        _logger.LogWarning("NoOpFileStorageService: Upload called but SupabaseStorage is not configured. ObjectPath={Path}, Bucket={Bucket}", objectPath, bucketName);
        return Task.FromResult(objectPath);
    }

    public Task<string?> CreateSignedUrlAsync(string objectPath, CancellationToken ct = default)
    {
        _logger.LogDebug("NoOpFileStorageService: CreateSignedUrl called but SupabaseStorage is not configured. ObjectPath={Path}", objectPath);
        return Task.FromResult<string?>(null);
    }

    public Task<string?> CreateSignedUrlAsync(string objectPath, string bucketName, CancellationToken ct = default)
    {
        _logger.LogDebug("NoOpFileStorageService: CreateSignedUrl called but SupabaseStorage is not configured. ObjectPath={Path}, Bucket={Bucket}", objectPath, bucketName);
        return Task.FromResult<string?>(null);
    }

    public Task DeleteAsync(string objectPath, CancellationToken ct = default)
    {
        _logger.LogDebug("NoOpFileStorageService: Delete called but SupabaseStorage is not configured. ObjectPath={Path}", objectPath);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string objectPath, string bucketName, CancellationToken ct = default)
    {
        _logger.LogDebug("NoOpFileStorageService: Delete called but SupabaseStorage is not configured. ObjectPath={Path}, Bucket={Bucket}", objectPath, bucketName);
        return Task.CompletedTask;
    }
}
