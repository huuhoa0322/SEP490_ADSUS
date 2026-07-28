using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ADSUS_BE
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Npgsql không tự nhận enum PostgreSQL — phải khai báo tường minh qua DataSource.
            // Thiếu phần này thì mọi truy vấn chạm cột role/status đều lỗi lúc chạy,
            // dù build vẫn qua bình thường.
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(
                builder.Configuration.GetConnectionString("DefaultConnection"));
            dataSourceBuilder.MapEnum<UserRole>("user_role");
            dataSourceBuilder.MapEnum<UserStatus>("user_status");
            var dataSource = dataSourceBuilder.Build();

            builder.Services.AddSingleton(dataSource);
            builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dataSource));

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}

public partial class Program { }
