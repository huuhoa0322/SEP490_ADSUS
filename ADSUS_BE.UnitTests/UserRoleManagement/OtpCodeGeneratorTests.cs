using System.Linq;
using ADSUS_BE.BLL.UserRoleManagement.Services;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

public class OtpCodeGeneratorTests
{
    [Fact]
    public void Generate_ReturnsExactlySixDigits()
    {
        var code = OtpCodeGenerator.Generate();

        Assert.Equal(6, code.Length);
        Assert.True(code.All(char.IsDigit));
    }

    [Fact]
    public void Generate_IsNotConstant()
    {
        // Không chứng minh được tính ngẫu nhiên bằng một test đơn vị, nhưng sinh 50 lần mà
        // ra đúng một giá trị lặp lại toàn bộ thì gần chắc là bug (ví dụ quên gọi RNG).
        var codes = Enumerable.Range(0, 50).Select(_ => OtpCodeGenerator.Generate()).ToHashSet();

        Assert.True(codes.Count > 1);
    }
}
