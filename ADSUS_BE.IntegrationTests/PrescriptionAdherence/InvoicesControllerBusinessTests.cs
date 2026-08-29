using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ADSUS_BE.IntegrationTests.AppointmentScheduling;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace ADSUS_BE.IntegrationTests.PrescriptionAdherence;

/// <summary>
/// Kiểm tra tất cả nhánh business-logic của InvoicesController thông qua HTTP.
/// Mỗi test verify: đúng HTTP status code VÀ response body có message tiếng Việt.
/// </summary>
public class InvoicesControllerBusinessTests
{
    private readonly Mock<IInvoiceService> _invoiceService = new();
    private readonly Mock<IUserRepository> _users = new();

    private WebApplicationFactory<Program> CreateApp()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInvoiceService>();
                services.AddScoped(_ => _invoiceService.Object);

                services.RemoveAll<IUserRepository>();
                services.AddScoped(_ => _users.Object);
            });
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET /api/v1/invoices/{id}  — không tìm thấy hóa đơn
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInvoiceDetail_NotFound_Returns422WithMessage()
    {
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Nurse);

        var nonExistentId = Guid.NewGuid();
        _invoiceService
            .Setup(s => s.GetInvoiceDetailAsync(nonExistentId))
            .ThrowsAsync(new BusinessException("Không tìm thấy hóa đơn."));

        var response = await client.GetAsync($"/api/v1/invoices/{nonExistentId}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Không tìm thấy hóa đơn", body);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PUT /api/v1/invoices/{id}/pay  — không tìm thấy hóa đơn
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PayAndDispense_InvoiceNotFound_Returns422WithMessage()
    {
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Nurse);

        var nonExistentId = Guid.NewGuid();
        _invoiceService
            .Setup(s => s.PayAndDispenseAsync(nonExistentId, It.IsAny<PaymentMethod>()))
            .ThrowsAsync(new BusinessException("Không tìm thấy hóa đơn."));

        var payload = new StringContent(
            JsonSerializer.Serialize(new { paymentMethod = "CASH" }),
            Encoding.UTF8, "application/json");

        var response = await client.PutAsync($"/api/v1/invoices/{nonExistentId}/pay", payload);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Không tìm thấy hóa đơn", body);
    }

    [Fact]
    public async Task PayAndDispense_AlreadyPaid_Returns422WithMessage()
    {
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Nurse);

        var invoiceId = Guid.NewGuid();
        _invoiceService
            .Setup(s => s.PayAndDispenseAsync(invoiceId, It.IsAny<PaymentMethod>()))
            .ThrowsAsync(new BusinessException("Hóa đơn này đã được thanh toán."));

        var payload = new StringContent(
            JsonSerializer.Serialize(new { paymentMethod = "CASH" }),
            Encoding.UTF8, "application/json");

        var response = await client.PutAsync($"/api/v1/invoices/{invoiceId}/pay", payload);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("đã được thanh toán", body);
    }

    [Fact]
    public async Task PayAndDispense_InvalidPaymentMethod_Returns400()
    {
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Nurse);

        var invoiceId = Guid.NewGuid();

        // Không cần setup service vì controller bắt lỗi trước khi gọi service
        var payload = new StringContent(
            JsonSerializer.Serialize(new { paymentMethod = "BITCOIN" }), // không hợp lệ
            Encoding.UTF8, "application/json");

        var response = await client.PutAsync($"/api/v1/invoices/{invoiceId}/pay", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Phương thức thanh toán không hợp lệ", body);
    }

    // ─────────────────────────────────────────────────────────────────────
    // POST /api/v1/invoices/generate/{caseId}  — không có đơn thuốc
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateInvoice_NoPrescription_Returns422WithMessage()
    {
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Nurse);

        var caseId = Guid.NewGuid();
        _invoiceService
            .Setup(s => s.GenerateInvoiceForCaseAsync(caseId))
            .ThrowsAsync(new BusinessException("Không tìm thấy đơn thuốc hoặc đơn thuốc trống cho ca khám này."));

        var response = await client.PostAsync($"/api/v1/invoices/generate/{caseId}", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Không tìm thấy đơn thuốc", body);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Authorization — chỉ NURSE được phép gọi endpoints thanh toán
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PayAndDispense_AsPatient_ReturnsForbidden()
    {
        using var app = CreateApp();
        var client = TestAuthHelper.CreateAuthenticatedClient(app, _users, UserRole.Patient);

        var payload = new StringContent(
            JsonSerializer.Serialize(new { paymentMethod = "CASH" }),
            Encoding.UTF8, "application/json");

        var response = await client.PutAsync($"/api/v1/invoices/{Guid.NewGuid()}/pay", payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PayAndDispense_AsAnonymous_ReturnsUnauthorized()
    {
        using var app = CreateApp();
        var client = app.CreateClient();

        var payload = new StringContent(
            JsonSerializer.Serialize(new { paymentMethod = "CASH" }),
            Encoding.UTF8, "application/json");

        var response = await client.PutAsync($"/api/v1/invoices/{Guid.NewGuid()}/pay", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GenerateInvoice_AsAnonymous_ReturnsUnauthorized()
    {
        using var app = CreateApp();
        var client = app.CreateClient();

        var response = await client.PostAsync($"/api/v1/invoices/generate/{Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
