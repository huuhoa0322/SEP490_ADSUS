using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.CaseClinicServices;
using ADSUS_BE.BLL.ClinicServiceManagement;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ADSUS_BE.IntegrationTests;

/// <summary>
/// Dựng DI THẬT của app (không thay service nào bằng mock) và resolve các service nằm trong vòng
/// phụ thuộc giữa module: CaseService ↔ CaseClinicServiceService ↔ InvoiceService, cùng các
/// service phía trên dùng chúng. Vòng DI chỉ lộ ra lúc resolve — test controller thường mock
/// service nên không bắt được (P11 review 24/09/2026, Lazy trong CaseClinicServiceService).
/// </summary>
public class DependencyInjectionResolutionTests
{
    [Theory]
    [InlineData(typeof(ICaseClinicServiceService))]
    [InlineData(typeof(IClinicServiceManagementService))]
    [InlineData(typeof(ICaseService))]
    [InlineData(typeof(IInvoiceService))]
    [InlineData(typeof(IInventoryService))]
    [InlineData(typeof(IPrescriptionService))]
    [InlineData(typeof(ICaseDiagnosisService))]
    [InlineData(typeof(IAppointmentService))]
    [InlineData(typeof(ADSUS_BE.BLL.Auth.Interfaces.IAuthService))]
    [InlineData(typeof(ADSUS_BE.BLL.UserRoleManagement.Interfaces.IPatientSelfRegistrationService))]
    [InlineData(typeof(ADSUS_BE.BLL.PatientRelationship.Interfaces.IPatientRelationshipService))]
    public void Resolve_ServiceInCrossModuleCycle_Succeeds(Type serviceType)
    {
        using var app = new WebApplicationFactory<Program>();
        using var scope = app.Services.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService(serviceType);

        Assert.NotNull(service);
    }

    [Fact]
    public void Resolve_LazyDependenciesOfCaseClinicService_ResolveToSameScopedInstances()
    {
        using var app = new WebApplicationFactory<Program>();
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;

        // Dựng CaseClinicServiceService trước, rồi mới lấy giá trị Lazy — đúng thứ tự lúc chạy thật
        Assert.NotNull(sp.GetRequiredService<ICaseClinicServiceService>());

        var lazyCases = sp.GetRequiredService<Lazy<ICaseService>>();
        var lazyInvoices = sp.GetRequiredService<Lazy<IInvoiceService>>();

        Assert.Same(sp.GetRequiredService<ICaseService>(), lazyCases.Value);
        Assert.Same(sp.GetRequiredService<IInvoiceService>(), lazyInvoices.Value);
    }

    [Theory]
    [InlineData("_unitOfWork")]
    [InlineData("_patientProfiles")]
    [InlineData("_fcmTokenService")]
    public void Resolve_AuthService_DiPicksFullConstructor(string fieldName)
    {
        // AuthService có 3 constructor (bản ngắn cho test cũ truyền null!) — DI phải chọn bản đầy
        // đủ, nếu không đăng ký tài khoản (UC-02) lỗi NullReference lúc chạy.
        using var app = new WebApplicationFactory<Program>();
        using var scope = app.Services.CreateScope();

        var authService = scope.ServiceProvider.GetRequiredService<ADSUS_BE.BLL.Auth.Interfaces.IAuthService>();
        var field = authService.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.NotNull(field!.GetValue(authService));
    }

    [Theory]
    [InlineData("_unitOfWork")]
    [InlineData("_invoiceService")]
    [InlineData("_caseClinicServiceService")]
    public void Resolve_CaseService_OptionalDependenciesAreInjected(string fieldName)
    {
        // CaseService nhận các phụ thuộc này dạng tham số tuỳ chọn (= null) để test cũ dựng được;
        // nếu DI không tiêm thì "kết thúc ca → tự sinh hoá đơn" / "tự gắn Khám thường" bị bỏ qua
        // mà không báo lỗi gì — khoá lại ở đây.
        using var app = new WebApplicationFactory<Program>();
        using var scope = app.Services.CreateScope();

        var caseService = scope.ServiceProvider.GetRequiredService<ICaseService>();
        var field = caseService.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.NotNull(field!.GetValue(caseService));
    }
}
