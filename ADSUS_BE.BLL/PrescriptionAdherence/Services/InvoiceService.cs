using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;

namespace ADSUS_BE.BLL.PrescriptionAdherence.Services;

public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext _context;
    private readonly IInventoryService _inventoryService;

    public InvoiceService(AppDbContext context, IInventoryService inventoryService)
    {
        _context = context;
        _inventoryService = inventoryService;
    }

    public async Task<Guid> GenerateInvoiceForCaseAsync(Guid caseId)
    {
        // 1. Kiểm tra xem Case đã có hóa đơn nào PENDING/PAID chưa để tránh tạo trùng
        var existingInvoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.CaseId == caseId && (i.Status == InvoiceStatus.PENDING || i.Status == InvoiceStatus.PAID));
            
        if (existingInvoice != null)
        {
            return existingInvoice.Id;
        }

        // 2. Lấy đơn thuốc của Case này (chỉ lấy đơn đang Active)
        var prescription = await _context.Prescriptions
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .FirstOrDefaultAsync(p => p.CaseId == caseId && p.Status == PrescriptionStatus.Active);

        if (prescription == null || !prescription.PrescriptionItems.Any())
        {
            throw new BusinessException("Không tìm thấy đơn thuốc hoặc đơn thuốc trống cho ca khám này.");
        }

        // Tạo Hóa đơn mới
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            CreatedAt = DateTime.UtcNow,
            Status = InvoiceStatus.PENDING,
            TotalAmount = 0
        };
        _context.Invoices.Add(invoice);

        decimal grandTotal = 0;

        // 3. Xử lý từng món thuốc (Greedy Allocation)
        foreach (var pItem in prescription.PrescriptionItems)
        {
            var remainingQuantity = pItem.QuantityBase;
            if (remainingQuantity <= 0) continue;

            // Lấy tất cả các quy cách đóng gói được phép bán của loại thuốc này, xếp từ lớn xuống nhỏ
            var packagings = await _context.MedicinePackagings
                .Include(mp => mp.MedicineUnit)
                .Where(mp => mp.MedicineId == pItem.MedicineId && mp.IsSellable)
                .OrderByDescending(mp => mp.ConversionFactor)
                .ToListAsync();

            if (!packagings.Any())
            {
                throw new BusinessException($"Thuốc '{pItem.Medicine.Name}' chưa được cấu hình đơn vị bán (IsSellable = true).");
            }

            foreach (var pack in packagings)
            {
                if (remainingQuantity >= pack.ConversionFactor)
                {
                    int qtyToBill = remainingQuantity / pack.ConversionFactor;
                    remainingQuantity = remainingQuantity % pack.ConversionFactor;

                    var invoiceItem = new InvoiceItem
                    {
                        Id = Guid.NewGuid(),
                        InvoiceId = invoice.Id,
                        Description = $"{pItem.Medicine.Name} - {pack.MedicineUnit.Name}",
                        Quantity = qtyToBill,
                        UnitPrice = pack.SalePrice,
                        TotalPrice = qtyToBill * pack.SalePrice,
                        ReferenceId = pItem.PrescriptionItemId
                    };
                    
                    _context.InvoiceItems.Add(invoiceItem);
                    grandTotal += invoiceItem.TotalPrice;
                }
            }

            // Nếu vẫn còn lẻ (nhỏ hơn đơn vị bán lẻ nhỏ nhất), làm tròn lên (Ceil) 1 đơn vị nhỏ nhất đó
            if (remainingQuantity > 0)
            {
                var smallestPack = packagings.Last(); // Đã sort Descending, Last() là nhỏ nhất
                var invoiceItem = new InvoiceItem
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoice.Id,
                    Description = $"{pItem.Medicine.Name} - {smallestPack.MedicineUnit.Name} (Làm tròn lên)",
                    Quantity = 1,
                    UnitPrice = smallestPack.SalePrice,
                    TotalPrice = smallestPack.SalePrice,
                    ReferenceId = pItem.PrescriptionItemId
                };
                
                _context.InvoiceItems.Add(invoiceItem);
                grandTotal += invoiceItem.TotalPrice;
            }
        }
        
        invoice.TotalAmount = grandTotal;
        
        await _context.SaveChangesAsync();
        return invoice.Id;
    }

    public async Task<PagedResult<InvoiceResponse>> GetInvoicesAsync(InvoiceFilter filter)
    {
        var query = _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.PatientProfile)
                    .ThenInclude(p => p.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(i => i.Id.ToString().Contains(search) || i.Case.PatientProfile.User.FullName.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<InvoiceStatus>(filter.Status, true, out var statusEnum))
        {
            query = query.Where(i => i.Status == statusEnum);
        }

        bool desc = string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? false : true;
        
        query = filter.SortBy?.ToLower() switch
        {
            "totalamount" => desc ? query.OrderByDescending(i => i.TotalAmount) : query.OrderBy(i => i.TotalAmount),
            "createdat" => desc ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt),
            _ => desc ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt)
        };

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(i => new InvoiceResponse
            {
                Id = i.Id,
                CaseId = i.CaseId,
                CaseName = i.Case.PatientProfile.User.FullName,
                TotalAmount = i.TotalAmount,
                CreatedAt = i.CreatedAt,
                PaidAt = i.PaidAt,
                Status = i.Status.ToString(),
                PaymentMethod = i.PaymentMethod != null ? i.PaymentMethod.ToString() : null
            })
            .ToListAsync();

        return new PagedResult<InvoiceResponse>(
            items, filter.Page, filter.PageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)filter.PageSize));
    }

    public async Task<InvoiceDetailResponse> GetInvoiceDetailAsync(Guid id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Case)
                .ThenInclude(c => c.PatientProfile)
                    .ThenInclude(p => p.User)
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
            throw new BusinessException("Không tìm thấy hóa đơn.");

        return new InvoiceDetailResponse
        {
            Id = invoice.Id,
            CaseId = invoice.CaseId,
            CaseName = invoice.Case.PatientProfile.User.FullName,
            TotalAmount = invoice.TotalAmount,
            CreatedAt = invoice.CreatedAt,
            PaidAt = invoice.PaidAt,
            Status = invoice.Status.ToString(),
            PaymentMethod = invoice.PaymentMethod != null ? invoice.PaymentMethod.ToString() : null,
            Items = invoice.InvoiceItems.Select(item => new InvoiceItemResponse
            {
                Id = item.Id,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            }).ToList()
        };
    }

    public async Task PayAndDispenseAsync(Guid invoiceId, PaymentMethod method)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) throw new BusinessException("Không tìm thấy hóa đơn.");
        
        if (invoice.Status == InvoiceStatus.PAID)
            throw new BusinessException("Hóa đơn này đã được thanh toán.");

        // 1. Mark as PAID
        invoice.Status = InvoiceStatus.PAID;
        invoice.PaidAt = DateTime.UtcNow;
        invoice.PaymentMethod = method;

        // 2. Dispense items (FEFO, Inventory deduct)
        await _inventoryService.DispenseAsync(invoice.CaseId);

        // Lưu trạng thái hóa đơn (giao dịch Inventory đã được add bên trong DispenseAsync)
        await _context.SaveChangesAsync();
    }
}
