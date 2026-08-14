using System.Globalization;
using System.Reflection;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.Mappers;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using ADSUS_BE.DAL.ExternalServices;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using QuestPDF.Drawing;
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
    private const string VietnameseFontFamily = "Noto Sans";

    private readonly ICaseRepository _cases;
    private readonly IFileStorageService _storage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CaseReportService> _logger;

    /// <summary>
    /// Nhúng Noto Sans (embedded resource, có dấu tiếng Việt) qua QuestPDF.Drawing.FontManager
    /// thay vì phụ thuộc "Arial" cài sẵn trên OS host. Trước đây "Arial" chỉ đúng trên máy
    /// dev Windows — server thật deploy trên Render (Linux) không có Arial, QuestPDF sẽ âm
    /// thầm fallback về Lato (không dấu tiếng Việt) mà không hề báo lỗi. Chạy 1 lần cho cả
    /// process nhờ static constructor.
    /// </summary>
    static CaseReportService()
    {
        using var fontStream = typeof(CaseReportService).Assembly.GetManifestResourceStream(
            "ADSUS_BE.BLL.Resources.Fonts.NotoSans-VariableFont_wdth_wght.ttf")
            ?? throw new InvalidOperationException(
                "Embedded Vietnamese font resource not found — check Resources/Fonts/ and the .csproj EmbeddedResource entry.");
        FontManager.RegisterFont(fontStream);
    }

    public CaseReportService(ICaseRepository cases, IFileStorageService storage, IHttpClientFactory httpClientFactory, ILogger<CaseReportService> logger)
    {
        _cases = cases;
        _storage = storage;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<byte[]> GenerateReportAsync(Guid caseId, CancellationToken ct = default)
    {
        var medicalCase = await _cases.GetDetailAsync(caseId, ct)
            ?? throw new ResourceNotFoundException("Case not found.");

        // BR-01 / AF-01: chỉ xuất được báo cáo của ca đã kết thúc.
        if (medicalCase.Status != CaseStatus.End)
        {
            throw new BusinessException("Ca bệnh chưa kết luận, không thể xuất báo cáo PDF.");
        }

        var imageBytesList = new List<(byte[] Bytes, string? Note)>();
        using var client = _httpClientFactory.CreateClient();

        foreach (var image in medicalCase.UltrasoundImages)
        {
            var signedUrl = await _storage.CreateSignedUrlAsync(image.FileRef, ct);
            if (signedUrl != null)
            {
                try
                {
                    var imgBytes = await client.GetByteArrayAsync(signedUrl, ct);
                    imageBytesList.Add((imgBytes, image.Note));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download image {FileRef} for PDF export.", image.FileRef);
                }
            }
        }

        var bytes = BuildPdf(medicalCase, imageBytesList);

        _logger.LogInformation("Generated PDF report for case {CaseId}", caseId);

        return bytes;
    }

    private static byte[] BuildPdf(Case medicalCase, List<(byte[] Bytes, string? Note)> images)
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
                // Đã fix finding #5 (review trước): trước đây dùng "Arial" — chỉ đúng trên máy
                // dev Windows, vỡ dấu âm thầm nếu deploy Linux (server thật dùng Render, xem
                // GenerateReportAsync_ConfirmedCase_EmbedsVietnameseCapableFontNotArial). Giờ
                // dùng Noto Sans tự nhúng qua FontManager.RegisterFont (static constructor phía
                // trên) — không phụ thuộc font cài sẵn trên OS host nữa.
                page.DefaultTextStyle(style => style.FontSize(11).FontFamily(VietnameseFontFamily));

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
                                header.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.Grey.Lighten3).Padding(5).Text("Thuốc").SemiBold();
                                header.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.Grey.Lighten3).Padding(5).Text("Liều dùng").SemiBold();
                                header.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.Grey.Lighten3).Padding(5).Text("Số ngày").SemiBold();
                                header.Cell().Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.Grey.Lighten3).Padding(5).Text("Hướng dẫn").SemiBold();
                            });

                            foreach (var item in prescription.PrescriptionItems)
                            {
                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(item.Medicine?.Name ?? "—");
                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(item.Dosage);
                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(item.DurationDays.ToString(CultureInfo.InvariantCulture));
                                table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(item.Instructions ?? "—");
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

                    if (images.Count > 0)
                    {
                        column.Item().PaddingTop(15).Text("ẢNH SIÊU ÂM").SemiBold().FontSize(13);
                        
                        foreach (var img in images)
                        {
                            column.Item().PaddingBottom(15).Row(row =>
                            {
                                // Chiếm 6 phần chiều rộng cho ảnh
                                row.RelativeItem(6).Image(img.Bytes);
                                
                                // Chiếm 4 phần chiều rộng cho ghi chú nằm bên phải ảnh
                                row.RelativeItem(4).PaddingLeft(15).Column(noteCol =>
                                {
                                    if (!string.IsNullOrWhiteSpace(img.Note))
                                    {
                                        noteCol.Item().Text(text =>
                                        {
                                            text.Span("Ghi chú ảnh:\n").SemiBold().FontSize(11).Italic();
                                            text.Span(img.Note).FontSize(11).Italic();
                                        });
                                    }
                                });
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
