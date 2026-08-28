using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.DTOs.Invoice;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using Microsoft.AspNetCore.Authorization;

namespace ADSUS_BE.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize(Roles = "NURSE")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceResponse>>> GetInvoices([FromQuery] InvoiceFilter filter)
    {
        var result = await _invoiceService.GetInvoicesAsync(filter);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<InvoiceDetailResponse>> GetInvoiceDetail(Guid id)
    {
        var result = await _invoiceService.GetInvoiceDetailAsync(id);
        return Ok(result);
    }

    [HttpPost("generate/{caseId}")]
    public async Task<ActionResult<Guid>> GenerateInvoiceForCase(Guid caseId)
    {
        var invoiceId = await _invoiceService.GenerateInvoiceForCaseAsync(caseId);
        return Ok(invoiceId);
    }

    [HttpPut("{id}/pay")]
    public async Task<IActionResult> PayAndDispense(Guid id, [FromBody] PayInvoiceRequest request)
    {
        if (!Enum.TryParse<PaymentMethod>(request.PaymentMethod, true, out var method))
        {
            return BadRequest("Phương thức thanh toán không hợp lệ (CASH, CREDIT_CARD, BANK_TRANSFER, etc).");
        }
        
        await _invoiceService.PayAndDispenseAsync(id, method);
        return Ok(new { message = "Thanh toán và xuất kho thành công." });
    }
}

public class PayInvoiceRequest
{
    public string PaymentMethod { get; set; } = string.Empty;
}
