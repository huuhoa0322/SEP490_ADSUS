using System.Text;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Auth.Services;
using ADSUS_BE.BLL.Auth.Validators;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
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
        /// <summary>CORS policy that lets the frontend call the API during development.</summary>
        private const string DevCorsPolicy = "DevCors";

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
            // Without this the browser blocks every call coming from Next.js.
            // Development only — replace with the real domain before deploying.
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(DevCorsPolicy, policy => policy
                    .WithOrigins("http://localhost:3000", "https://localhost:3000")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
            });

            // ---------- Per-module service registration ----------
            // DAL
            builder.Services.AddScoped<IUserRepository, UserRepository>();

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
                app.UseCors(DevCorsPolicy);
            }

            app.UseHttpsRedirection();

            // Order matters: Authentication (who are you) must run BEFORE Authorization
            // (are you allowed). Swap them and every [Authorize] returns 401.
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}

public partial class Program { }
