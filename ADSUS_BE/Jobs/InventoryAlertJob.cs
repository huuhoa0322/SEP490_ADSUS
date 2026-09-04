using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.Jobs
{
    [DisallowConcurrentExecution]
    public class InventoryAlertJob : IJob
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<InventoryAlertJob> _logger;
        
        public InventoryAlertJob(IServiceProvider serviceProvider, ILogger<InventoryAlertJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                _logger.LogInformation("InventoryAlertJob is starting at {Time}", DateTime.UtcNow);
                await ProcessInventoryAlertsAsync(context.CancellationToken);
                _logger.LogInformation("InventoryAlertJob completed successfully at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[InventoryAlertJob] Failed to process alerts");
            }
        }

        public async Task ProcessInventoryAlertsAsync(System.Threading.CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var summary = await inventoryService.GetAlertSummaryAsync();
            
            if (summary.LowStockCount == 0 && summary.ExpiringSoonCount == 0 && summary.ExpiredCount == 0)
            {
                _logger.LogInformation("[InventoryAlertJob] No alerts to send.");
                return;
            }
            
            // Lấy tất cả user ADMIN + NURSE
            var adminNurseIds = await dbContext.Users
                .Where(u => (u.Role == UserRole.Admin || u.Role == UserRole.Nurse) 
                            && u.Status == UserStatus.Active)
                .Select(u => u.UserId)
                .ToListAsync(stoppingToken);
            
            if (!adminNurseIds.Any())
            {
                _logger.LogWarning("[InventoryAlertJob] No active admin/nurse found to send alerts to.");
                return;
            }

            // Tạo nội dung notification
            var body = BuildAlertBody(summary);
            
            var sendRequest = new SendNotificationRequest
            {
                UserId = adminNurseIds.First(), // placeholder, SendBulkAsync sẽ đổi
                Type = "inventory_alert",
                Title = "⚠️ Cảnh báo kho thuốc",
                Body = body,
                DeepLink = "/admin/medicines/inventory-alerts",
                Metadata = new Dictionary<string, object>
                {
                    ["lowStockCount"] = summary.LowStockCount,
                    ["expiryCount"] = summary.ExpiringSoonCount + summary.ExpiredCount
                }
            };

            await notificationService.SendBulkAsync(adminNurseIds, sendRequest, stoppingToken);
            _logger.LogInformation("[InventoryAlertJob] Successfully sent alerts to {Count} staff members.", adminNurseIds.Count);
        }
        
        private static string BuildAlertBody(InventoryAlertSummary summary)
        {
            var parts = new List<string>();
            if (summary.LowStockCount > 0)
                parts.Add($"{summary.LowStockCount} thuốc sắp hết hàng");
            if (summary.ExpiringSoonCount > 0)
                parts.Add($"{summary.ExpiringSoonCount} lô sắp hết hạn");
            if (summary.ExpiredCount > 0)
                parts.Add($"{summary.ExpiredCount} lô đã hết hạn");
            return string.Join(", ", parts) + ". Vui lòng kiểm tra kho.";
        }
    }
}
