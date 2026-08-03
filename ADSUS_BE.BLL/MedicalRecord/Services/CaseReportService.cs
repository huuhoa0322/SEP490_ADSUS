using System.Globalization;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.Mappers;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ADSUS_BE.BLL.MedicalRecord.Services;

/// <summary>
/// UC-12 — báo cáo PDF của một lần khám.
///
/// Nội dung đúng bằng những gì SCR-12 hiển thị và không hơn (BR-01): kết luận bác sĩ đã
/// duyệt và đơn thuốc. KHÔNG có độ tin cậy của AI, KHÔNG có dữ liệu AI chưa duyệt, và cũng
/// KHÔNG có logo phòng khám hay ảnh thu nhỏ — hai thứ sau không có căn cứ trong PRD.
///
/// File dựng tại chỗ mỗi lần gọi, không lưu vào Storage: một bản PDF cũ nằm đâu đó sẽ mâu
/// thuẫn với kết luận hoặc đơn thuốc nếu chúng được sửa sau này.
/// </summary>
public sealed class CaseReportService : ICaseReportService
{
    private readonly ICaseRepository _cases;
    private readonly ILogger<CaseReportService> _logger;

    public CaseReportService(ICaseRepository cases, ILogger<CaseReportService> logger)
    {
        _cases = cases;
        _logger = logger;
    }

    public async Task<byte[]> GenerateReportAsync(Guid caseId, CancellationToken ct = default)
    {
        var medicalCase = await _cases.GetDetailAsync(caseId, ct)
            ?? throw new ResourceNotFoundException("Case not found.");

        // BR-01 / AF-01: chỉ xuất được báo cáo của ca đã duyệt.
        if (medicalCase.Status != CaseStatus.Confirmed)
        {
            throw new BusinessException("Cannot export a report for an incomplete case.");
        }

        var bytes = BuildPdf(medicalCase);

        _logger.LogInformation("Generated PDF report for case {CaseId}", caseId);

        return bytes;
    }

    private static byte[] BuildPdf(Case medicalCase)
    {
        var patient = medicalCase.PatientProfile?.User;
        // Cùng logic chọn "đơn thuốc hiện hành" với #23 (GET /cases/{id}) — xem
        // CaseMapper.SelectLatestPrescription — để PDF không bao giờ mâu thuẫn với API.
        var prescription = CaseMapper.SelectLatestPrescription(medicalCase);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);

                // Font phải có dấu tiếng Việt. Lato (mặc định của QuestPDF) không có, để
                // nguyên là toàn bộ dấu biến thành ô vuông.
                //
                // RỦI RO CHƯA XỬ LÝ (final review finding #5, ghi nhận nhưng chưa fix):
                // "Arial" giả định host giống Windows có sẵn font Arial. Trên container Linux
                // (nơi rất có thể sẽ deploy) không có Arial — QuestPDF âm thầm fallback về Lato
                // (bundled sẵn), font KHÔNG có dấu tiếng Việt. Hậu quả: response vẫn 200, vẫn
                // đúng Content-Type application/pdf, PDF vẫn "sinh ra" bình thường — không có
                // exception, không có log lỗi — nhưng toàn bộ chữ có dấu trong đó bị mất dấu
                // hoặc hiển thị thành ô vuông. Đây là lỗi âm thầm, không phải lỗi ồn ào.
                // Cách sửa đúng và lâu dài: nhúng một font TTF hỗ trợ tiếng Việt (vd. Noto Sans,
                // Roboto) qua QuestPDF.Drawing.FontManager.RegisterFont(...) khi ứng dụng khởi
                // động, rồi trỏ FontFamily về tên font đó — không phụ thuộc font có sẵn trên OS.
                // Việc này cần một file .ttf thật (và xác nhận license) nên KHÔNG làm trong đợt
                // sửa lỗi review này — để lại làm follow-up, cần người quyết định có chặn release
                // vì rủi ro này hay chấp nhận nếu môi trường deploy thực tế chỉ là Windows.
                page.DefaultTextStyle(style => style.FontSize(11).FontFamily("Arial"));

                page.Header()
                    .Text("BÁO CÁO KẾT QUẢ KHÁM")
                    .SemiBold().FontSize(18).AlignCenter();

                page.Content().PaddingVertical(20).Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Text(text =>
                    {
                        text.Span("Họ và tên: ").SemiBold();
                        text.Span(patient?.FullName ?? "—");
                    });

                    column.Item().Text(text =>
                    {
                        text.Span("Ngày sinh: ").SemiBold();
                        text.Span(patient?.DateOfBirth?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "—");
                    });

                    column.Item().Text(text =>
                    {
                        text.Span("Số điện thoại: ").SemiBold();
                        text.Span(patient?.Phone ?? "—");
                    });

                    column.Item().Text(text =>
                    {
                        text.Span("Ngày khám: ").SemiBold();
                        text.Span(medicalCase.VisitDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                    });

                    column.Item().Text(text =>
                    {
                        text.Span("Bác sĩ phụ trách: ").SemiBold();
                        text.Span(medicalCase.Doctor?.FullName ?? "—");
                    });

                    column.Item().PaddingTop(10).Text("CHẨN ĐOÁN").SemiBold().FontSize(13);
                    column.Item().Text(medicalCase.FinalDiagnosis ?? "—");

                    column.Item().PaddingTop(10).Text("HƯỚNG XỬ TRÍ").SemiBold().FontSize(13);
                    column.Item().Text(medicalCase.DoctorConclusion ?? "—");

                    column.Item().PaddingTop(10).Text("ĐƠN THUỐC").SemiBold().FontSize(13);

                    if (prescription is null || prescription.PrescriptionItems.Count == 0)
                    {
                        column.Item().Text("Không có đơn thuốc cho lần khám này.");
                    }
                    else
                    {
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(3);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Thuốc").SemiBold();
                                header.Cell().Text("Liều dùng").SemiBold();
                                header.Cell().Text("Số ngày").SemiBold();
                                header.Cell().Text("Hướng dẫn").SemiBold();
                            });

                            foreach (var item in prescription.PrescriptionItems)
                            {
                                table.Cell().Text(item.Medicine?.Name ?? "—");
                                table.Cell().Text(item.Dosage);
                                table.Cell().Text(item.DurationDays.ToString(CultureInfo.InvariantCulture));
                                table.Cell().Text(item.Instructions ?? "—");
                            }
                        });

                        if (!string.IsNullOrWhiteSpace(prescription.GeneralNote))
                        {
                            column.Item().PaddingTop(8).Text(text =>
                            {
                                text.Span("Ghi chú: ").SemiBold();
                                text.Span(prescription.GeneralNote);
                            });
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Trang ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}
