using Xunit;

namespace ADSUS_BE.UnitTests.Common.TestData;

/// <summary>
/// Ma trận dữ liệu kiểm thử chuẩn hóa cho các bộ lọc XSS và ký hiệu lâm sàng bất đẳng thức y khoa.
/// </summary>
public static class HtmlValidationTestData
{
    public static TheoryData<string?, bool, string> CrossPlatformHtmlVectors => new()
    {
        // 1. Vector XSS / HTML Độc hại -> Bắt buộc CHẶN (expectedValid = false)
        { "<SCRIPT>alert(1)</SCRIPT>", false, "Thẻ SCRIPT viết hoa toàn bộ" },
        { "<div style=\"display:none\">", false, "Thẻ div chứa thuộc tính inline style" },
        { "<img/src=x onerror=alert(1)>", false, "Thẻ img kèm event handler onerror" },
        { "<svg onload=alert(1)>", false, "Thẻ svg kèm event handler onload" },
        { "<a href=\"javascript:...\">", false, "Thẻ liên kết chứa URI javascript" },
        { "<br/>", false, "Thẻ tự đóng br/" },
        { "<hr/>", false, "Thẻ tự đóng hr/" },
        { "<textarea></textarea>", false, "Cặp thẻ textarea" },
        { "</br>", false, "Thẻ đóng lỗi cú pháp </br>" },
        { "</p>", false, "Thẻ đóng </p>" },
        { "<IMG SRC=\"javascript:alert(1);\">", false, "Thẻ img viết hoa kèm script source" },
        { "<svg/onload=alert(1)>", false, "Thẻ svg tự đóng chèn onload" },
        { "<BODY ONLOAD=alert(1)>", false, "Thẻ body kèm onload" },
        { "<iframe src=\"http://evil.com\">", false, "Thẻ iframe nhúng trang ngoài" },
        { "<STYLE>.evil{display:none}</STYLE>", false, "Thẻ style CSS độc hại" },
        { "<details ontoggle=\"alert(1)\">", false, "Thẻ details kèm ontoggle" },
        { "<b>bold</b>", false, "Thẻ định dạng b thông thường" },
        { "<INPUT TYPE=\"IMAGE\" SRC=\"javascript:alert(1);\">", false, "Thẻ input image nguy hiểm" },

        // 2. Ký hiệu Lâm sàng Hợp lệ -> Bắt buộc CHO PHÉP (expectedValid = true)
        { "Đau bụng < 3 ngày", true, "So sánh < có khoảng trắng" },
        { "Nhiệt độ > 38.5°C", true, "Nhiệt độ sốt > có khoảng trắng" },
        { "Huyết áp < 120/80 mmHg", true, "Chỉ số huyết áp < chuẩn" },
        { "SpO2 > 95%", true, "Độ bão hòa oxy SpO2 > chuẩn" },
        { "Bạch cầu < 4.0 và SpO2 > 95%", true, "Biểu thức lâm sàng phức hợp" },
        { "Thân nhiệt <38°C", true, "So sánh < không có khoảng trắng" },
        { "HbA1c < 6.5%", true, "Ngưỡng chỉ số đường huyết HbA1c" },
        { "Bạch cầu <4.0", true, "Chỉ số bạch cầu không khoảng trắng" },
        { "Glucose < 70 mg/dL", true, "Ngưỡng hạ đường huyết" },
        { "Creatinine <1.2 mg/dL", true, "Chỉ số chức năng thận" },
        { "Kali < 3.5 mEq/L", true, "Nồng độ điện giải Kali" },
        { "Tiểu cầu <150 G/L", true, "Chỉ số số lượng tiểu cầu" },
        { "AST < 35 U/L & ALT < 35 U/L", true, "Chỉ số men gan kép" },
        { "PaO2 < 60 mmHg, PaCO2 > 45 mmHg", true, "Khí máu động mạch" },
        { "Sốt cao > 39°C kéo dài < 2 ngày", true, "Mô tả triệu chứng đa ngưỡng" },
        { "Cân nặng < 2500g", true, "Trọng lượng thai nhi / trẻ sơ sinh" },
        { "Chiều dài đầu mông < 10mm", true, "Chỉ số siêu âm thai CRL" },

        // 3. Chuỗi Rỗng / Null / Khoảng trắng -> Cho phép (expectedValid = true)
        { null, true, "Null payload" },
        { "", true, "Chuỗi rỗng" },
        { "   ", true, "Khoảng trắng" },
        { "`t`n`r", true, "Ký tự xuống dòng tab" },
        { "         ", true, "Nhiều khoảng trắng" }
    };
}
