using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ADSUS_BE.SystemTests.BF07_PharmacyInventory;

/// <summary>
/// Report 5.3 — BF-07 Pharmacy & Inventory Control (HTTP Flow).
///
/// KHÔNG tráo bất kỳ service nào — request đi xuyên suốt qua tầng DAL thật vào DB test thật.
/// Chỉ chọn các test case cốt lõi, mỗi case kiểm 1 quy tắc nghiệp vụ khác nhau. Toàn bộ vai trò
/// trong BF-07 là ADMIN/PHARMACIST — dùng thẳng tài khoản Admin, không cần dựng Case/Appointment
/// như các BF khác.
///
/// Cần sẵn 1 tài khoản Admin đã seed thủ công qua SQL (giống BF-01), và bảng medicine_unit đã
/// seed 1 dòng "Viên" (giống BF-06).
/// </summary>
public class PharmacyInventoryTests
{
    private const string SeedAdminPhone = "0900000001";
    private const string SeedAdminPassword = "Aa123456@";

    private static readonly Guid SeedMedicineUnitId = Guid.Parse("7b090445-b234-441b-a384-33b878cb831c");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task STC001_AdminCreatesMedicine_CatalogsItWithABaseUnitPackaging()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var medicineName = UniqueMedicineName("STC001");

        var response = await admin.PostAsJsonAsync("/api/v1/medicines", new
        {
            name = medicineName,
            usageUnit = (string?)null,
            volumePerBaseUnit = (decimal?)null,
            medicineUnitId = SeedMedicineUnitId,
            salePrice = 3000,
            lowStockThreshold = 10,
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var medicine = await response.Content.ReadFromJsonAsync<MedicineResponse>(JsonOptions, ct);
        Assert.Equal("ACTIVE", medicine!.Status);

        var packagings = await GetPackagingsAsync(admin, medicine.MedicineId, ct);
        Assert.Contains(packagings, p => p.IsBaseUnit && p.MedicineUnitId == SeedMedicineUnitId);
    }

    [Fact]
    public async Task STC002_CreatingMedicine_WithNameThatAlreadyExists_IsRejected()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (_, medicineName, _) = await CreateMedicineAsync(admin, "STC002", ct);

        var response = await admin.PostAsJsonAsync("/api/v1/medicines", new
        {
            name = medicineName,
            usageUnit = (string?)null,
            volumePerBaseUnit = (decimal?)null,
            medicineUnitId = SeedMedicineUnitId,
            salePrice = 3000,
            lowStockThreshold = 10,
        }, ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task STC003_AdminCreatesSupplier()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);

        var response = await admin.PostAsJsonAsync("/api/v1/suppliers", new
        {
            name = $"STC003 Supplier {Guid.NewGuid():N}"[..30],
            phoneNumber = UniquePhone(),
            email = $"{Guid.NewGuid():N}@test.adsus.local",
            address = "123 Test Street",
            taxCode = UniqueTaxCode(),
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var supplier = await response.Content.ReadFromJsonAsync<SupplierResponse>(JsonOptions, ct);
        Assert.True(supplier!.IsActive);
    }

    [Fact]
    public async Task STC004_CreatingSupplier_WithTaxCodeThatAlreadyExists_IsRejected()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var taxCode = UniqueTaxCode();
        await CreateSupplierAsync(admin, "STC004 First", taxCode, ct);

        var response = await admin.PostAsJsonAsync("/api/v1/suppliers", new
        {
            name = $"STC004 Second {Guid.NewGuid():N}"[..25],
            phoneNumber = UniquePhone(),
            email = $"{Guid.NewGuid():N}@test.adsus.local",
            address = "456 Test Avenue",
            taxCode,
        }, ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task STC005_ImportingTheSameLotTwice_MergesTheBatch_AndRecalculatesTheWeightedAverageImportPrice()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (medicineId, _, packagingId) = await CreateMedicineAsync(admin, "STC005", ct);
        var supplierId = await CreateSupplierAsync(admin, "STC005", UniqueTaxCode(), ct);
        var lotNumber = UniqueCode();
        var expiry = DateTime.UtcNow.AddYears(1);

        (await ImportBatchAsync(admin, medicineId, supplierId, packagingId, lotNumber, expiry, quantity: 10, importPricePerUnit: 1000, ct))
            .EnsureSuccessStatusCode();
        (await ImportBatchAsync(admin, medicineId, supplierId, packagingId, lotNumber, expiry, quantity: 10, importPricePerUnit: 2000, ct))
            .EnsureSuccessStatusCode();

        var batches = await GetBatchesAsync(admin, medicineId, ct);
        var batch = Assert.Single(batches, b => b.LotNumber == lotNumber);
        Assert.Equal(20, batch.QuantityBase);
        Assert.Equal(1500m, batch.BaseUnitAvgImportPrice);
    }

    [Fact]
    public async Task STC006_BulkImport_WithOneInvalidRow_RejectsTheWholeBatch()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (validMedicineId, _, validPackagingId) = await CreateMedicineAsync(admin, "STC006 Valid", ct);
        var (invalidMedicineId, _, invalidPackagingId) = await CreateMedicineAsync(admin, "STC006 Invalid", ct);
        var supplierId = await CreateSupplierAsync(admin, "STC006", UniqueTaxCode(), ct);
        (await admin.DeleteAsync($"/api/v1/medicines/{invalidMedicineId}", ct)).EnsureSuccessStatusCode();

        var response = await admin.PostAsJsonAsync("/api/v1/inventory/import/bulk", new[]
        {
            new
            {
                medicineId = validMedicineId,
                supplierId,
                medicinePackagingId = validPackagingId,
                lotNumber = UniqueCode(),
                expiryDate = DateTime.UtcNow.AddYears(1),
                quantity = 50,
                importPricePerUnit = 1000,
            },
            new
            {
                medicineId = invalidMedicineId,
                supplierId,
                medicinePackagingId = invalidPackagingId,
                lotNumber = UniqueCode(),
                expiryDate = DateTime.UtcNow.AddYears(1),
                quantity = 50,
                importPricePerUnit = 1000,
            },
        }, ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        // Toàn bộ batch phải rollback — dòng hợp lệ (medicine đầu) không được để lại tồn kho.
        var validMedicineBatches = await GetBatchesAsync(admin, validMedicineId, ct);
        Assert.Empty(validMedicineBatches);
    }

    [Fact]
    public async Task STC007_BatchesAreListedSoonestExpiryFirst_AndAdjustingWithAReasonRecordsTheChange()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (medicineId, _, packagingId) = await CreateMedicineAsync(admin, "STC007", ct);
        var supplierId = await CreateSupplierAsync(admin, "STC007", UniqueTaxCode(), ct);

        var farLot = UniqueCode();
        var soonLot = UniqueCode();
        // Nhập lô hạn XA trước, lô hạn GẦN sau — nếu list chỉ phản ánh thứ tự tạo (không sort theo
        // hạn) thì assertion bên dưới sẽ bắt được ngay.
        (await ImportBatchAsync(admin, medicineId, supplierId, packagingId, farLot, DateTime.UtcNow.AddYears(2), 20, 1000, ct))
            .EnsureSuccessStatusCode();
        (await ImportBatchAsync(admin, medicineId, supplierId, packagingId, soonLot, DateTime.UtcNow.AddMonths(6), 20, 1000, ct))
            .EnsureSuccessStatusCode();

        var batches = await GetBatchesAsync(admin, medicineId, ct);
        Assert.Equal(soonLot, batches[0].LotNumber);
        Assert.Equal(farLot, batches[1].LotNumber);

        var soonBatch = batches[0];
        var adjustResponse = await admin.PutAsJsonAsync("/api/v1/inventory/adjust", new
        {
            batchId = soonBatch.BatchId,
            newQuantityBase = soonBatch.QuantityBase - 3,
            reason = "STC007 physical count correction",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, adjustResponse.StatusCode);
        var adjusted = await adjustResponse.Content.ReadFromJsonAsync<AdjustInventoryResponse>(JsonOptions, ct);
        Assert.Equal(-3, adjusted!.Delta);
        Assert.Equal(soonBatch.QuantityBase - 3, adjusted.NewQuantity);

        // Không đổi số lượng — bị từ chối vì không có gì để điều chỉnh.
        var noChangeResponse = await admin.PutAsJsonAsync("/api/v1/inventory/adjust", new
        {
            batchId = soonBatch.BatchId,
            newQuantityBase = adjusted.NewQuantity,
            reason = "STC007 no actual change",
        }, ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noChangeResponse.StatusCode);

        // Thiếu lý do — bị từ chối ngay ở tầng validation (400, trước khi vào service).
        var noReasonResponse = await admin.PutAsJsonAsync("/api/v1/inventory/adjust", new
        {
            batchId = soonBatch.BatchId,
            newQuantityBase = adjusted.NewQuantity - 1,
            reason = "",
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, noReasonResponse.StatusCode);
    }

    [Fact]
    public async Task STC008_MedicineWhoseStockFallsToOrBelowItsThreshold_AppearsInTheLowStockAlertList()
    {
        await using var app = CreateApp();
        var ct = TestContext.Current.CancellationToken;
        var admin = await LoginAsAdminAsync(app);
        var (medicineId, _, packagingId) = await CreateMedicineAsync(admin, "STC008", ct, lowStockThreshold: 5);
        var supplierId = await CreateSupplierAsync(admin, "STC008", UniqueTaxCode(), ct);

        // Nhập đúng bằng ngưỡng (5) — "tồn <= ngưỡng" phải tính là low-stock.
        (await ImportBatchAsync(admin, medicineId, supplierId, packagingId, UniqueCode(), DateTime.UtcNow.AddYears(1), 5, 1000, ct))
            .EnsureSuccessStatusCode();

        var alertsResponse = await admin.GetAsync("/api/v1/inventory/alerts", ct);
        Assert.Equal(HttpStatusCode.OK, alertsResponse.StatusCode);
        var alerts = await alertsResponse.Content.ReadFromJsonAsync<InventoryAlertSummary>(JsonOptions, ct);
        var alert = Assert.Single(alerts!.LowStockAlerts, a => a.MedicineId == medicineId);
        Assert.Equal(5, alert.CurrentStock);
        Assert.Equal(5, alert.Threshold);
    }

    // ---- shared setup ----

    private static async Task<(Guid MedicineId, string MedicineName, Guid PackagingId)> CreateMedicineAsync(
        HttpClient admin, string label, CancellationToken ct, int lowStockThreshold = 10)
    {
        var medicineName = UniqueMedicineName(label);
        var response = await admin.PostAsJsonAsync("/api/v1/medicines", new
        {
            name = medicineName,
            usageUnit = (string?)null,
            volumePerBaseUnit = (decimal?)null,
            medicineUnitId = SeedMedicineUnitId,
            salePrice = 3000,
            lowStockThreshold,
        }, ct);
        response.EnsureSuccessStatusCode();
        var medicine = await response.Content.ReadFromJsonAsync<MedicineResponse>(JsonOptions, ct);

        var packagings = await GetPackagingsAsync(admin, medicine!.MedicineId, ct);
        var packagingId = packagings.Single(p => p.IsBaseUnit).Id;

        return (medicine.MedicineId, medicineName, packagingId);
    }

    private static async Task<Guid> CreateSupplierAsync(
        HttpClient admin, string label, string taxCode, CancellationToken ct)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/suppliers", new
        {
            name = $"{label} Supplier {Guid.NewGuid():N}"[..30],
            phoneNumber = UniquePhone(),
            email = $"{Guid.NewGuid():N}@test.adsus.local",
            address = "123 Test Street",
            taxCode,
        }, ct);
        response.EnsureSuccessStatusCode();
        var supplier = await response.Content.ReadFromJsonAsync<SupplierResponse>(JsonOptions, ct);
        return supplier!.SupplierId;
    }

    private static Task<HttpResponseMessage> ImportBatchAsync(
        HttpClient admin, Guid medicineId, Guid supplierId, Guid packagingId,
        string lotNumber, DateTime expiryDate, int quantity, decimal importPricePerUnit, CancellationToken ct) =>
        admin.PostAsJsonAsync("/api/v1/inventory/import", new
        {
            medicineId,
            supplierId,
            medicinePackagingId = packagingId,
            lotNumber,
            expiryDate,
            quantity,
            importPricePerUnit,
        }, ct);

    private static async Task<List<MedicinePackagingResponse>> GetPackagingsAsync(
        HttpClient admin, Guid medicineId, CancellationToken ct)
    {
        var response = await admin.GetAsync($"/api/v1/medicines/{medicineId}/packagings", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<MedicinePackagingResponse>>(JsonOptions, ct))!;
    }

    private static async Task<List<MedicineBatchResponse>> GetBatchesAsync(
        HttpClient admin, Guid medicineId, CancellationToken ct)
    {
        var response = await admin.GetAsync($"/api/v1/inventory/batches?medicineId={medicineId}", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<ADSUS_BE.BLL.Common.PagedResult<MedicineBatchResponse>>(JsonOptions, ct);
        return page!.Items.ToList();
    }

    // ---- helpers ----

    private static WebApplicationFactory<Program> CreateApp() => new();

    private static string UniquePhone() => "09" + Random.Shared.Next(10_000_000, 99_999_999);

    private static string UniqueCode() => "TEST-" + Guid.NewGuid().ToString("N")[..12];

    private static string UniqueMedicineName(string label) => $"{label} Med {Guid.NewGuid():N}";

    private static string UniqueTaxCode() => Random.Shared.Next(1_000_000_000, 2_000_000_000).ToString();

    private static async Task<HttpClient> LoginAsAdminAsync(WebApplicationFactory<Program> app)
    {
        var ct = TestContext.Current.CancellationToken;
        var loginResponse = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            phoneNumber = SeedAdminPhone,
            password = SeedAdminPassword,
        }, ct);
        loginResponse.EnsureSuccessStatusCode();
        var body = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions, ct);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }
}
