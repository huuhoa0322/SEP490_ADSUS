using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Auth.Services;
using ADSUS_BE.BLL.Auth.Validators;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.DashboardReporting.Interfaces;
using ADSUS_BE.BLL.DashboardReporting.Services;
using ADSUS_BE.BLL.Engagement.Interfaces;
using ADSUS_BE.BLL.Engagement.Services;
using ADSUS_BE.BLL.AIModelManagement.Interfaces;
using ADSUS_BE.BLL.AIModelManagement.Services;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Services;
using ADSUS_BE.Jobs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.Services;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.AppointmentScheduling.Services;
using ADSUS_BE.BLL.MedicalRecord.Validators;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ADSUS_BE.Middlewares;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Quartz;
using Serilog;

namespace ADSUS_BE
{
    public class Program
    {
        /// <summary>Tên chính sách CORS cho phép frontend gọi API.</summary>
        private const string CorsPolicy = "AdsusCors";

        /// <summary>
        /// Origin mặc định lúc phát triển, dùng khi cấu hình không khai gì.
        ///
        /// Next.js mặc định chạy ở cổng 3000, NHƯNG nếu cổng đó đang bận thì nó tự nhảy sang
        /// 3001, 3002... mà chỉ báo một dòng nhỏ trong terminal. Thiếu các cổng dự phòng này
        /// thì trình duyệt chặn sạch mọi lời gọi, triệu chứng nhìn y hệt "backend chưa chạy".
        /// Hay gặp nhất là khi lỡ mở hai cửa sổ `npm run dev`.
        /// </summary>
        private static readonly string[] DefaultDevCorsOrigins =
        {
            "http://localhost:3000",
            "https://localhost:3000",
            "http://localhost:3001",
            "https://localhost:3001",
            "http://localhost:3002",
            "https://localhost:3002",
        };

        /// <summary>
        /// Đọc danh sách origin được phép từ cấu hình, khoá <c>Cors:AllowedOrigins</c>.
        ///
        /// Trước đây danh sách này nằm cứng trong code, nên deploy lên tên miền thật là phải
        /// sửa code rồi build lại — mà quên thì trình duyệt chặn sạch, triệu chứng lại giống
        /// hệt "backend chưa chạy". Giờ chỉ cần thêm vào appsettings của môi trường đó:
        /// <code>"Cors": { "AllowedOrigins": [ "https://adsus.example.com" ] }</code>
        ///
        /// Ngoài Development mà không khai gì thì dừng luôn: chạy tiếp với danh sách
        /// localhost là cầm chắc frontend không gọi được mà chẳng ai hiểu vì sao.
        /// </summary>
        private static string[] ResolveCorsOrigins(WebApplicationBuilder builder)
        {
            var configured = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>();

            if (configured is { Length: > 0 }) return configured;

            if (builder.Environment.IsDevelopment()) return DefaultDevCorsOrigins;

            throw new InvalidOperationException(
                "Chua khai 'Cors:AllowedOrigins'. Moi truong " +
                $"'{builder.Environment.EnvironmentName}' bat buoc phai liet ke ten mien that " +
                "cua frontend, vi danh sach localhost mac dinh se chan sach moi loi goi.");
        }

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // QuestPDF Community: miễn phí cho tổ chức có doanh thu dưới 1 triệu USD/năm.
            // Thiếu dòng này thì thư viện ném exception ngay lần dựng PDF đầu tiên.
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            // ---------- Serilog ----------
            // Replaces the default Microsoft.Extensions.Logging console provider. Sinks and
            // levels are read from the "Serilog" section in appsettings.json — GlobalExceptionHandler
            // and every Service still just inject ILogger<T> as usual, Serilog is only the provider.
            builder.Host.UseSerilog((context, configuration) =>
                configuration.ReadFrom.Configuration(context.Configuration));

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy =
                        System.Text.Json.JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DictionaryKeyPolicy =
                        System.Text.Json.JsonNamingPolicy.CamelCase;
                    // Cho phép map camelCase JSON sang PascalCase property khi deserialize.
                    // Không có cờ này thì request gửi "doctorId" không khớp property "DoctorId".
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    // Cho phép enum parse từ cả string ("Morning") lẫn integer (0)
                    options.JsonSerializerOptions.Converters.Add(
                        new System.Text.Json.Serialization.JsonStringEnumConverter());
                });
            builder.Services.AddEndpointsApiExplorer();

            // ---------- Swagger: token input so protected endpoints can be tried out ----------
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "ADSUS API", Version = "v1" });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Paste the access token here (no need to type 'Bearer').",
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer",
                            },
                        },
                        Array.Empty<string>()
                    },
                });
            });

            // ---------- Database ----------
            // Npgsql does not discover PostgreSQL enums on its own — they must be registered
            // on the data source. Without this every query touching role or status fails at
            // runtime, even though the build succeeds.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Không có dòng này thì Npgsql ném ra "Host can't be null" — đọc xong không
                // ai đoán được phải sửa ở đâu.
                //
                // Nguyên nhân hay gặp nhất KHÔNG phải là quên nhập User Secrets, mà là chạy
                // sai profile: trên thanh Run của Visual Studio phải chọn "http" hoặc
                // "https". Chọn mục mang tên project ("ADSUS_BE") là chạy không qua
                // launchSettings.json, ASPNETCORE_ENVIRONMENT không được đặt, môi trường rơi
                // về Production, mà User Secrets thì chỉ nạp ở Development.
                throw new InvalidOperationException(
                    "Khong doc duoc chuoi ket noi 'DefaultConnection'. " +
                    $"Moi truong hien tai: {builder.Environment.EnvironmentName}. " +
                    "Neu khong phai 'Development' thi tren thanh Run cua Visual Studio hay chon " +
                    "profile 'http' (dung chon muc ten project) roi chay lai. " +
                    "Neu dung 'Development' roi ma van bao loi thi chuot phai project ADSUS_BE > " +
                    "Manage User Secrets va dan khoi ConnectionStrings + JwtSettings — xin file " +
                    "chung cua nhom.");
            }

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.MapEnum<UserRole>("user_role");
            dataSourceBuilder.MapEnum<UserStatus>("user_status");
            dataSourceBuilder.MapEnum<GenderType>("gender_type");
            dataSourceBuilder.MapEnum<BlogPostStatus>("blog_status");
            dataSourceBuilder.MapEnum<AiResultStatus>("ai_result_status");
            dataSourceBuilder.MapEnum<AppointmentStatus>("appointment_status");
            dataSourceBuilder.MapEnum<CaseStatus>("case_status");
            dataSourceBuilder.MapEnum<ModelVersionStatus>("model_version_status");
            dataSourceBuilder.MapEnum<PrescriptionStatus>("prescription_status");
            dataSourceBuilder.MapEnum<IntakeStatus>("intake_status");
            dataSourceBuilder.MapEnum<SlotStatus>("slot_status");
            dataSourceBuilder.MapEnum<ReminderSlot>("reminder_slot");
            var dataSource = dataSourceBuilder.Build();

            builder.Services.AddSingleton(dataSource);
            builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dataSource));

            // ---------- Configuration read from User Secrets ----------
            builder.Services.Configure<JwtSettings>(
                builder.Configuration.GetSection(JwtSettings.SectionName));

            var jwtSettings = builder.Configuration
                .GetSection(JwtSettings.SectionName)
                .Get<JwtSettings>();

            if (jwtSettings is null || string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
            {
                // Fail at startup rather than letting a confusing error surface on the first
                // request that needs a token.
                throw new InvalidOperationException(
                    "JwtSettings is not configured. Right-click the ADSUS_BE project > Manage User Secrets " +
                    "and add the JwtSettings block (SecretKey, Issuer, Audience, ExpiryMinutes). " +
                    "The key must be identical across the team — ask for the shared one.");
            }

            // ---------- JWT authentication ----------
            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    // Sau khi chữ ký hợp lệ, còn phải hỏi thêm DB xem tài khoản có bị khoá
                    // hay vô hiệu hoá không. Xem AccountStatusJwtEvents để biết lý do.
                    options.EventsType = typeof(AccountStatusJwtEvents);

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtSettings.Issuer,
                        ValidAudience = jwtSettings.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),

                        // .NET allows five minutes of clock drift by default. For a medical
                        // system, expired should mean expired.
                        ClockSkew = TimeSpan.Zero,
                    };
                });

            // EventsType yêu cầu lớp xử lý sự kiện phải nằm trong DI. Scoped vì nó dùng
            // repository, mà repository sống theo vòng đời một request.
            builder.Services.AddScoped<AccountStatusJwtEvents>();

            builder.Services.AddAuthorization();

            // ---------- CORS ----------
            // Không có phần này thì trình duyệt chặn sạch mọi lời gọi từ Next.js.
            var corsOrigins = ResolveCorsOrigins(builder);

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(CorsPolicy, policy => policy
                    .WithOrigins(corsOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
            });

            // ---------- Chặn gọi dồn dập vào các endpoint xác thực ----------
            // UC-01 BR-04 (tự khoá tài khoản sau N lần sai) còn chờ nhóm chốt — xem chú thích
            // trong AuthService.LoginAsync. Nhưng dù chốt thế nào thì vẫn cần lớp này, vì đây
            // là hai luật khác nhau: BR-04 bảo vệ MỘT tài khoản, còn giới hạn theo địa chỉ IP
            // chặn kẻ dò lần lượt hàng nghìn số điện thoại khác nhau — bên kia không đỡ được.
            //
            // Riêng forgot-password còn nguy hơn: mỗi lời gọi trúng là đổi mật khẩu của người
            // ta rồi gửi một lá thư. Gọi liên tục là quấy rối được chủ tài khoản và đốt sạch
            // hạn mức gửi mail, dù kẻ tấn công không hề đăng nhập được.
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = async (context, cancellationToken) =>
                {
                    var response = context.HttpContext.Response;
                    response.StatusCode = StatusCodes.Status429TooManyRequests;

                    if (context.Lease.TryGetMetadata(
                            MetadataName.RetryAfter,
                            out var retryAfter))
                    {
                        response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                            .ToString(CultureInfo.InvariantCulture);
                    }

                    await response.WriteAsJsonAsync(
                        ApiResponse<object>.Fail(
                            StatusCodes.Status429TooManyRequests,
                            "Too many requests. Please wait before trying again."),
                        cancellationToken);
                };

                options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        // Phân vùng theo IP. Không dùng số điện thoại làm khoá: như vậy là
                        // kẻ tấn công tự chọn được vùng của mình, đổi số một cái là hết bị chặn.
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        }));
            });

            // ---------- Per-module service registration ----------
            // DAL
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
            builder.Services.AddScoped<IAiModelVersionRepository, AiModelVersionRepository>();
            builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

            // DAL — Module 4: Medical Record
            builder.Services.AddScoped<IPatientProfileRepository, PatientProfileRepository>();
            builder.Services.AddScoped<ICaseRepository, CaseRepository>();
            builder.Services.AddScoped<IUltrasoundImageRepository, UltrasoundImageRepository>();

            // External services — push notification. Hiện tại dùng FakePush (in-memory stub)
            // cho dev/test/CI. Production sẽ đổi sang FirebasePushNotificationClient (sprint sau,
            // cần FCM service account JSON qua User Secrets). Singleton vì stateless.
            builder.Services.AddSingleton<IPushNotificationClient, FakePushNotificationClient>();

            // BLL — safety filter cho Module 10 Chat (trước khi gọi LLM). Singleton vì stateless,
            // chỉ đọc mảng keyword tĩnh.
            builder.Services.AddSingleton<IPsychologyTopicFilter, PsychologyTopicFilter>();

            // BLL — Module 1: Authentication & Account
            builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IProfileService, ProfileService>();

            // BLL — Module 2: User & Role Management
            builder.Services.AddScoped<IUserAccountService, UserAccountService>();
            builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();

            // BLL — Module 3: Dashboard & Reporting
            builder.Services.AddScoped<IDashboardService, DashboardService>();

            // BLL — Module 4: Medical Record
            builder.Services.AddScoped<IPatientProfileService, PatientProfileService>();
            builder.Services.AddScoped<ICaseService, CaseService>();
            builder.Services.AddScoped<ICaseReportService, CaseReportService>();
            builder.Services.AddScoped<IDoctorDirectoryService, DoctorDirectoryService>();
            builder.Services.AddScoped<IPatientAccountService, PatientAccountService>();
            builder.Services.AddScoped<IValidator<CreatePatientAccountRequest>, CreatePatientAccountRequestValidator>();
            builder.Services.AddScoped<IValidator<UpdatePatientAccountRequest>, UpdatePatientAccountRequestValidator>();

            // BLL — Module 6: AI Model Management
            builder.Services.AddScoped<IAiModelService, AiModelService>();

            // BLL — Module 8: Appointment Scheduling (UC-15)
            builder.Services.AddScoped<IScheduleSlotRepository, ScheduleSlotRepository>();
            builder.Services.AddScoped<IScheduleSlotService, ScheduleSlotService>();

            // ---------- Cấu hình AI Backend ----------
            builder.Services.Configure<AiBackendSettings>(
                builder.Configuration.GetSection(AiBackendSettings.SectionName));
            builder.Services.AddHttpClient("AiBackend");

            // ---------- Supabase Storage (ảnh siêu âm, Module 4) ----------
            builder.Services.Configure<SupabaseStorageSettings>(
                builder.Configuration.GetSection(SupabaseStorageSettings.SectionName));

            var storageSettings = builder.Configuration
                .GetSection(SupabaseStorageSettings.SectionName)
                .Get<SupabaseStorageSettings>();

            if (storageSettings?.IsConfigured != true)
            {
                if (builder.Environment.IsDevelopment())
                {
                    // Chưa khai Supabase thì vẫn phải chạy được ở Development — nếu không, ai
                    // không đụng tới upload ảnh siêu âm cũng bị chặn khởi động chỉ vì thiếu một
                    // secret không liên quan tới việc họ đang làm. Chỉ request nào thực sự cần
                    // IFileStorageService mới vỡ, kèm thông báo rõ phải sửa gì.
                    builder.Services.AddScoped<IFileStorageService>(_ =>
                        throw new InvalidOperationException(
                            "Chua cau hinh SupabaseStorage. Chuot phai project ADSUS_BE > Manage "
                            + "User Secrets va them ca SupabaseStorage:Url va "
                            + "SupabaseStorage:ServiceKey — ca hai gia tri deu nam trong User "
                            + "Secrets, khong nam trong appsettings.json."));
                }
                else
                {
                    // Dừng ngay tại đây thay vì để vỡ lúc bác sĩ bấm tải ảnh lên. Lỗi lúc đó chỉ
                    // hiện ra là 500 giữa ca khám, còn ở đây thì biết ngay phải sửa gì.
                    throw new InvalidOperationException(
                        "Chua cau hinh SupabaseStorage. Moi truong " +
                        $"'{builder.Environment.EnvironmentName}' bat buoc phai cau hinh " +
                        "SupabaseStorage:Url va SupabaseStorage:ServiceKey.");
                }
            }
            else
            {
                builder.Services.AddHttpClient("SupabaseStorage");
                builder.Services.AddScoped<IFileStorageService, SupabaseStorageService>();
            }

            // ---------- Gửi email (API-04) ----------
            builder.Services.Configure<EmailSettings>(
                builder.Configuration.GetSection(EmailSettings.SectionName));

            var emailSettings = builder.Configuration
                .GetSection(EmailSettings.SectionName)
                .Get<EmailSettings>();

            if (emailSettings?.IsConfigured == true)
            {
                builder.Services.AddScoped<IEmailService, SmtpEmailService>();
            }
            else if (builder.Environment.IsDevelopment())
            {
                // Chưa khai SMTP thì vẫn phải chạy được, nếu không cả nhóm bị chặn chỉ vì
                // thiếu một tài khoản gửi mail. Bản này in mật khẩu tạm ra console.
                builder.Services.AddScoped<IEmailService, DevConsoleEmailService>();
            }
            else
            {
                // Dừng ngay tại đây, KHÔNG để chạy tiếp.
                //
                // Trước đây chỗ này chỉ bỏ qua không đăng ký gì, tưởng là "thiếu thì chết
                // lúc khởi động". Không phải: controller không lấy từ DI nên thiếu phụ thuộc
                // chỉ vỡ lúc có request, mà AuthController lại giữ IPasswordResetService —
                // nên NGAY CẢ ĐĂNG NHẬP cũng trả 500 ở môi trường khác Development, trong
                // khi log không nói gì về email.
                throw new InvalidOperationException(
                    "Chua cau hinh EmailSettings. Moi truong " +
                    $"'{builder.Environment.EnvironmentName}' bat buoc phai co may chu SMTP that " +
                    "— xem ADSUS_BE.BLL/Common/EmailSettings.cs de biet cac khoa can khai.");
            }

            // BLL — Module 10: Engagement (Blog PUBLIC endpoints)
            builder.Services.AddScoped<IBlogPostService, BlogPostService>();

            // DAL — Repositories
            builder.Services.AddScoped<IBlogPostRepository, BlogPostRepository>();

            // BLL — Module 7: Prescription & Medication Adherence
            builder.Services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
            builder.Services.AddScoped<IPrescriptionItemRepository, PrescriptionItemRepository>();
            builder.Services.AddScoped<IMedicationIntakeLogRepository, MedicationIntakeLogRepository>();
            builder.Services.AddScoped<IMedicineRepository, MedicineRepository>();
            // AdherenceCalculator is static — used directly, not injected.
            builder.Services.AddSingleton<IMedicationIntakeScheduleGenerator, MedicationIntakeScheduleGenerator>();
            builder.Services.AddScoped<IPrescriptionService, PrescriptionService>();
            builder.Services.AddScoped<IMedicationIntakeService, MedicationIntakeService>();

            // ---------- Quartz JOB-01: Medication Reminder ----------
            builder.Services.AddQuartz(q =>
            {
                // Dev: every 30 seconds. Prod: every 1 minute (override via appsettings).
                var devCron = "0/30 * * * * ?";       // every 30 sec
                var prodCron = "0 0/1 * * * ?";       // every 1 min

                var cronExpression = builder.Environment.IsDevelopment()
                    ? devCron
                    : prodCron;

                var jobKey = new Quartz.JobKey("MedicationReminderJob", "medication");

                q.AddJob<MedicationReminderJob>(opts => opts
                    .WithIdentity(jobKey)
                    .StoreDurably());

                q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity("MedicationReminderTrigger", "medication")
                    .WithCronSchedule(cronExpression));
            });

            builder.Services.AddQuartzHostedService(q => q
                .WaitForJobsToComplete = true);

            // Scans the whole BLL assembly, so validators added by other modules are picked
            // up automatically.
            builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

            var app = builder.Build();

            // Registered first so it wraps every middleware after it (CORS, auth, controllers)
            // and can translate any exception thrown downstream into an ApiResponse<T>.Fail(...).
            app.UseMiddleware<GlobalExceptionHandler>();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // CORS phải nằm NGOÀI khối IsDevelopment.
            // Trước đây đặt bên trong, nên ai chạy backend ở môi trường khác Development là
            // trình duyệt chặn sạch mọi lời gọi từ Next.js — mà triệu chứng nhìn y hệt
            // "backend chưa chạy", rất khó đoán ra nguyên nhân.
            // Bản thân chính sách đã giới hạn origin nên để ngoài vẫn an toàn.
            app.UseCors(CorsPolicy);

            // Đặt TRƯỚC xác thực: request bị chặn vì gọi quá dày thì không cần tốn công
            // kiểm tra token hay dò database làm gì.
            app.UseRateLimiter();

            // Chỉ ép HTTPS khi chạy thật.
            //
            // Lúc phát triển mà bật, ai chọn profile "https" trong Visual Studio là API sẽ
            // đá mọi request http sang https. Máy ảo Android không tin chứng chỉ tự ký của
            // .NET nên ứng dụng di động đứt kết nối, mà báo lỗi lại giống hệt "backend chưa
            // chạy" — rất mất thời gian mới lần ra.
            //
            // Bỏ ở môi trường Development không mất mát gì: máy ảo, trình duyệt và backend
            // đều nằm trên cùng một máy, không có đường truyền nào để nghe lén.
            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            // Order matters: Authentication (who are you) must run BEFORE Authorization
            // (are you allowed). Swap them and every [Authorize] returns 401.
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            // In ra ngay lúc khởi động để người chạy biết backend đang lắng nghe ở đâu và
            // cho phép origin nào. Không có dòng này thì lúc frontend báo "không kết nối
            // được" sẽ phải mò rất lâu mới biết là do sai cổng hay do CORS.
            app.Lifetime.ApplicationStarted.Register(() =>
            {
                var addresses = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : "(theo launchSettings)";
                app.Logger.LogInformation(
                    "ADSUS API san sang | Dia chi: {Addresses} | CORS cho phep: {Origins} | Moi truong: {Env}",
                    addresses,
                    string.Join(", ", corsOrigins),
                    app.Environment.EnvironmentName);
            });

            app.Run();
        }
    }
}

public partial class Program { }
