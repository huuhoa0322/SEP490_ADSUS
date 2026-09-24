using System.Net;
using ADSUS_BE.DAL.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ADSUS_BE.UnitTests.ExternalServices;

public class SupabaseStorageServiceTests
{
    /// <summary>Ghi lại request gửi đi thay vì gọi Supabase thật.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static (SupabaseStorageService Sut, CapturingHandler Handler) CreateSut()
    {
        var handler = new CapturingHandler();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("SupabaseStorage")).Returns(new HttpClient(handler));

        var settings = Options.Create(new SupabaseStorageSettings
        {
            Url = "https://example.supabase.co",
            ServiceKey = "service-role-key",
            Bucket = "ultrasound-images",
        });

        return (new SupabaseStorageService(factory.Object, settings, NullLogger<SupabaseStorageService>.Instance), handler);
    }

    [Fact]
    public async Task UploadAsync_SendsUpsertHeader_SoReConfirmingSameImageOverwritesInsteadOf409()
    {
        // Xác nhận lại kết quả AI trên cùng ảnh dùng lại đúng tên file — thiếu x-upsert thì
        // Supabase trả 409 KeyAlreadyExists (lỗi thật gặp 24/09/2026).
        var (sut, handler) = CreateSut();

        await sut.UploadAsync(new MemoryStream(new byte[] { 1 }), "case_1_img_2.jpg", "image/jpeg", "datasets", TestContext.Current.CancellationToken);

        var request = Assert.IsType<HttpRequestMessage>(handler.LastRequest);
        Assert.True(request.Headers.TryGetValues("x-upsert", out var values));
        Assert.Equal("true", Assert.Single(values));
        Assert.Equal("https://example.supabase.co/storage/v1/object/datasets/case_1_img_2.jpg", request.RequestUri!.ToString());
    }
}
