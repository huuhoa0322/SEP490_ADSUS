using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ADSUS_BE.IntegrationTests.UserRoleManagement;

/// <summary>
/// UC-04 — các luồng chính (tạo, sửa, vô hiệu hoá, khôi phục) chạy qua pipeline HTTP thật.
///
/// Khác <see cref="AdminOnlyAccessTests"/> (chỉ kiểm RBAC 401/403): ở đây repository là một
/// bản giả CÓ TRẠNG THÁI (in-memory), nên gọi Create rồi GetById/Deactivate/Reactivate ngay
/// sau đó phản ánh đúng thay đổi — kiểm được cả routing, validator, service, và JSON
/// serialize/deserialize cùng lúc, thứ không unit test nào một mình làm được.
/// </summary>
public class UserAccountFlowTests
{
    private readonly FakeUserRepository _users = new();

    [Fact]
    public async Task CreateAccount_PatientWithDateOfBirth_HidesItOnGetByIdEndToEnd()
    {
        // BR-01 — kiểm ở tầng HTTP thật, không phải gọi thẳng service: JSON trả về cho Admin
        // không được có ngày sinh, dù request lúc tạo có gửi lên.
        using var app = CreateApp();
        var client = CreateAdminClient(app);

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            phoneNumber = "0911111111",
            fullName = "Nguyễn Thị Bệnh Nhân",
            role = "PATIENT",
            dateOfBirth = "1990-05-20",
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content
            .ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>();
        Assert.NotNull(created!.Data);
        Assert.Null(created.Data!.Account.DateOfBirth);
        Assert.False(string.IsNullOrEmpty(created.Data.TemporaryPassword));

        var getResponse = await client.GetAsync($"/api/v1/admin/users/{created.Data.Account.UserId}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<UserAccountResponse>>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Null(fetched!.Data!.DateOfBirth);
    }

    [Fact]
    public async Task CreateAccount_DuplicatePhoneNumber_Returns400()
    {
        using var app = CreateApp();
        var client = CreateAdminClient(app);
        var request = new
        {
            phoneNumber = "0922222222",
            fullName = "BS. Trần Văn B",
            role = "DOCTOR",
        };

        var firstResponse = await client.PostAsJsonAsync("/api/v1/admin/users", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync("/api/v1/admin/users", request);

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        var body = await secondResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Contains("already used", body!.Message);
    }

    [Fact]
    public async Task CreateThenDeactivateThenReactivate_StatusTransitionsThroughRealPipeline()
    {
        using var app = CreateApp();
        var client = CreateAdminClient(app);

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            phoneNumber = "0933333333",
            fullName = "BS. Lê Văn C",
            role = "DOCTOR",
        });
        var created = await createResponse.Content
            .ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>();
        var userId = created!.Data!.Account.UserId;

        var deactivateResponse = await client.PutAsync($"/api/v1/admin/users/{userId}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var afterDeactivate = await client.GetAsync($"/api/v1/admin/users/{userId}");
        var afterDeactivateBody = await afterDeactivate.Content
            .ReadFromJsonAsync<ApiResponse<UserAccountResponse>>();
        Assert.Equal("DEACTIVATED", afterDeactivateBody!.Data!.Status);

        var reactivateResponse = await client.PutAsync($"/api/v1/admin/users/{userId}/reactivate", null);
        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);

        var afterReactivate = await client.GetAsync($"/api/v1/admin/users/{userId}");
        var afterReactivateBody = await afterReactivate.Content
            .ReadFromJsonAsync<ApiResponse<UserAccountResponse>>();
        Assert.Equal("ACTIVE", afterReactivateBody!.Data!.Status);
    }

    [Fact]
    public async Task UpdateAccount_AttemptToPromoteToAdmin_Returns400AndRoleUnchanged()
    {
        using var app = CreateApp();
        var client = CreateAdminClient(app);

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            phoneNumber = "0944444444",
            fullName = "BS. Phạm Văn D",
            role = "DOCTOR",
        });
        var created = await createResponse.Content
            .ReadFromJsonAsync<ApiResponse<CreatedUserAccountResponse>>();
        var userId = created!.Data!.Account.UserId;

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/admin/users/{userId}", new
        {
            fullName = "BS. Phạm Văn D",
            role = "ADMIN",
        });

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/v1/admin/users/{userId}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<ApiResponse<UserAccountResponse>>();
        Assert.Equal("DOCTOR", fetched!.Data!.Role);
    }

    // ---- helpers ----

    private WebApplicationFactory<Program> CreateApp()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository>(_users);
                services.RemoveAll<IAuditLogRepository>();
                services.AddSingleton<IAuditLogRepository>(new FakeAuditLogRepository());
            });
        });
    }

    /// <summary>Seed một Admin đang đăng nhập, phát token cho họ.</summary>
    private HttpClient CreateAdminClient(WebApplicationFactory<Program> app)
    {
        var admin = new User
        {
            UserId = Guid.NewGuid(),
            Phone = "0900000000",
            FullName = "Quản trị viên",
            PasswordHash = "khong-dung-toi-trong-bai-test-nay",
            Role = UserRole.Admin,
            Status = UserStatus.Active,
        };
        _users.Seed(admin);

        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(admin);

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Bản giả CÓ TRẠNG THÁI của <see cref="IUserRepository"/> — một Dictionary trong bộ nhớ
    /// đóng vai trò database, để test được cả một luồng nhiều bước (tạo rồi đọc lại, tạo rồi
    /// vô hiệu hoá rồi đọc lại...), không chỉ một lời gọi rời rạc như Mock thông thường.
    /// </summary>
    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, User> _byId = new();

        public void Seed(User user) => _byId[user.UserId] = user;

        public Task<User?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId.Values.FirstOrDefault(u => u.Phone == phone));

        public Task<User?> GetByPhoneReadOnlyAsync(string phone, CancellationToken cancellationToken = default) =>
            GetByPhoneAsync(phone, cancellationToken);

        public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId.GetValueOrDefault(userId));

        public Task<User?> GetByIdReadOnlyAsync(Guid userId, CancellationToken cancellationToken = default) =>
            GetByIdAsync(userId, cancellationToken);

        public Task<User?> GetForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
            GetByIdAsync(userId, cancellationToken);

        public Task<bool> IsEmailUsedByAnotherUserAsync(
            Guid userId, string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId.Values.Any(u =>
                u.UserId != userId
                && u.Email != null
                && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId.Values.Any(u => u.Phone == phone));

        public Task<bool> IsEmailUsedAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId.Values.Any(u =>
                u.Email != null && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            _byId[user.UserId] = user;
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<User> Items, int TotalCount)> SearchAsync(
            string? keyword, UserRole? role, UserStatus? status, int page, int pageSize,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<User> query = _byId.Values;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(u =>
                    u.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || u.Phone.Contains(keyword));
            }
            if (role is not null) query = query.Where(u => u.Role == role);
            if (status is not null) query = query.Where(u => u.Status == status);

            var items = query.ToList();
            return Task.FromResult<(IReadOnlyList<User>, int)>((items, items.Count));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<User>> ListActiveDoctorsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<User>>(
                _byId.Values.Where(u => u.Role == UserRole.Doctor && u.Status == UserStatus.Active).ToList());

        public Task<IReadOnlyList<User>> GetAllPatientsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<User>>(
                _byId.Values.Where(u => u.Role == UserRole.Patient && u.Status == UserStatus.Active).ToList());
    }

    /// <summary>Bản giả chỉ để thoả DI — nội dung nhật ký không phải trọng tâm của các test này.</summary>
    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(
            int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditLogEntry>>(Array.Empty<AuditLogEntry>());
    }
}
