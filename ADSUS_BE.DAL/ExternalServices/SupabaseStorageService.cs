using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADSUS_BE.DAL.ExternalServices;

/// <summary>
/// Gọi thẳng Supabase Storage REST API bằng HttpClient. Không dùng SDK Supabase: ở đây chỉ
/// cần đúng ba thao tác, thêm cả một SDK để đổi lấy ba lời gọi HTTP là không đáng.
/// </summary>
public sealed class SupabaseStorageService : IFileStorageService
{
    private readonly HttpClient _http;
    private readonly SupabaseStorageSettings _settings;
    private readonly ILogger<SupabaseStorageService> _logger;

    public SupabaseStorageService(
        IHttpClientFactory httpClientFactory,
        IOptions<SupabaseStorageSettings> settings,
        ILogger<SupabaseStorageService> logger)
    {
        _http = httpClientFactory.CreateClient("SupabaseStorage");
        _settings = settings.Value;
        _logger = logger;
    }

    private string BaseUrl => _settings.Url.TrimEnd('/');

    private void AddAuth(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ServiceKey);
        request.Headers.Add("apikey", _settings.ServiceKey);
    }

    // objectPath/bucketName cuối cùng đều nằm trong path của URL gọi Supabase Storage —
    // encode từng segment (và chặn "." / ".." literal) để tránh path traversal hoặc
    // chèn thêm segment/query/fragment ngoài ý muốn nếu caller truyền dữ liệu không tin cậy.
    private static string BuildEncodedObjectPath(string bucketName, string objectPath)
    {
        var segments = new[] { bucketName }
            .Concat(objectPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Invalid storage path segment.", nameof(objectPath));
        }

        return string.Join('/', segments.Select(Uri.EscapeDataString));
    }

    public Task<string> UploadAsync(
        Stream content,
        string objectPath,
        string contentType,
        CancellationToken ct = default)
    {
        return UploadAsync(content, objectPath, contentType, _settings.Bucket, ct);
    }

    public async Task<string> UploadAsync(
        Stream content,
        string objectPath,
        string contentType,
        string bucketName,
        CancellationToken ct = default)
    {
        var encodedPath = BuildEncodedObjectPath(bucketName, objectPath);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{BaseUrl}/storage/v1/object/{encodedPath}");
        AddAuth(request);

        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Upload to storage failed for {ObjectPath} with status {StatusCode}: {Body}",
                objectPath, (int)response.StatusCode, body);

            // Message chung chung vì nó đi thẳng ra client — không lộ chi tiết hạ tầng.
            throw new InvalidOperationException("Could not store the uploaded file.");
        }

        _logger.LogInformation("Uploaded {ObjectPath} to storage bucket {Bucket}", objectPath, bucketName);
        return objectPath;
    }

    public Task<string?> CreateSignedUrlAsync(string objectPath, CancellationToken ct = default)
    {
        return CreateSignedUrlAsync(objectPath, _settings.Bucket, ct);
    }

    public async Task<string?> CreateSignedUrlAsync(string objectPath, string bucketName, CancellationToken ct = default)
    {
        try
        {
            var encodedPath = BuildEncodedObjectPath(bucketName, objectPath);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/storage/v1/object/sign/{encodedPath}")
            {
                Content = JsonContent.Create(new { expiresIn = _settings.SignedUrlTtlSeconds }),
            };
            AddAuth(request);

            using var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                // Hay gặp nhất: dữ liệu seed có file_ref trỏ vào object chưa từng tồn tại.
                _logger.LogWarning(
                    "Could not sign {ObjectPath}, status {StatusCode}", objectPath, (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<SignedUrlPayload>(cancellationToken: ct);

            if (string.IsNullOrWhiteSpace(payload?.SignedUrl))
            {
                _logger.LogWarning("Storage returned an empty signed URL for {ObjectPath}", objectPath);
                return null;
            }

            // Supabase trả đường dẫn tương đối ("/object/sign/..."), phải ghép tiền tố.
            return $"{BaseUrl}/storage/v1{payload.SignedUrl}";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(exception, "Storage was unreachable while signing {ObjectPath}", objectPath);
            return null;
        }
    }

    public Task DeleteAsync(string objectPath, CancellationToken ct = default)
    {
        return DeleteAsync(objectPath, _settings.Bucket, ct);
    }

    public async Task DeleteAsync(string objectPath, string bucketName, CancellationToken ct = default)
    {
        var encodedPath = BuildEncodedObjectPath(bucketName, objectPath);
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{BaseUrl}/storage/v1/object/{encodedPath}");
        AddAuth(request);

        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            // Chỉ log. Hàm này luôn được gọi trên đường dọn dẹp sau một lỗi khác — ném tiếp
            // ở đây sẽ nuốt mất nguyên nhân gốc mà người đọc log đang cần.
            _logger.LogWarning(
                "Could not delete orphaned object {ObjectPath}, status {StatusCode}",
                objectPath, (int)response.StatusCode);
        }
    }

    private sealed class SignedUrlPayload
    {
        [JsonPropertyName("signedURL")]
        public string? SignedUrl { get; set; }
    }
}
