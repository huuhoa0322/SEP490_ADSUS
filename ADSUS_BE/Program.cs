using System.Text;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Auth.Services;
using ADSUS_BE.BLL.Auth.Validators;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

namespace ADSUS_BE
{
    public class Program
    {
        /// <summary>Tên chính sách CORS cho phép frontend gọi API trong lúc phát triển.</summary>
        private const string DevCorsPolicy = "DevCors";

        /// <summary>
        /// Origin của frontend được phép gọi API. Next.js mặc định chạy ở cổng 3000.
        /// Ai chạy frontend ở cổng khác thì phải thêm vào đây, không thì trình duyệt chặn.
        /// </summary>
        private static readonly string[] AllowedCorsOrigins =
        {
            "http://localhost:3000",
            "https://localhost:3000",
        };

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
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
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(
                builder.Configuration.GetConnectionString("DefaultConnection"));
            dataSourceBuilder.MapEnum<UserRole>("user_role");
            dataSourceBuilder.MapEnum<UserStatus>("user_status");
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

            builder.Services.AddAuthorization();

            // ---------- CORS ----------
            // Không có phần này thì trình duyệt chặn sạch mọi lời gọi từ Next.js.
            // Đây là origin cho môi trường phát triển — lúc deploy thật phải thay bằng
            // tên miền thật.
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(DevCorsPolicy, policy => policy
                    .WithOrigins(AllowedCorsOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
            });

            // ---------- Per-module service registration ----------
            // DAL
            builder.Services.AddScoped<IUserRepository, UserRepository>();

            // External services — push notification. Hiện tại dùng FakePush (in-memory stub)
            // cho dev/test/CI. Production sẽ đổi sang FirebasePushNotificationClient (sprint sau,
            // cần FCM service account JSON qua User Secrets). Singleton vì stateless.
            builder.Services.AddSingleton<IPushNotificationClient, FakePushNotificationClient>();

            // BLL — Module 1: Authentication & Account
            builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
            builder.Services.AddScoped<IAuthService, AuthService>();

            // Scans the whole BLL assembly, so validators added by other modules are picked
            // up automatically.
            builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

            var app = builder.Build();

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
            app.UseCors(DevCorsPolicy);

            app.UseHttpsRedirection();

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
                    string.Join(", ", AllowedCorsOrigins),
                    app.Environment.EnvironmentName);
            });

            app.Run();
        }
    }
}

public partial class Program { }
