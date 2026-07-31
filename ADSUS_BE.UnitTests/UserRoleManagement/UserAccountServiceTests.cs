using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

/// <summary>
/// UC-04 — Admin quản lý tài khoản (FT-07 tạo, FT-08 khoá/vô hiệu hoá, FT-09 phân quyền).
///
/// Bám theo phần Verification Criteria của UC-04 trong UCS.
/// </summary>
public class UserAccountServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly UserAccountService _sut;

    private readonly List<User> _saved = new();

    public UserAccountServiceTests()
    {
        _users.Setup(r => r.PhoneExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _users.Setup(r => r.IsEmailUsedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _users.Setup(r => r.IsEmailUsedByAnotherUserAsync(
                  It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => _saved.Add(u))
              .Returns(Task.CompletedTask);

        _email.Setup(e => e.SendTemporaryPasswordAsync(
                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        _sut = new UserAccountService(_users.Object, _email.Object);
    }

    // ---------- FT-07: tạo tài khoản ----------

    [Fact]
    public async Task Tao_BacSi_ThanhCong_TaiKhoanActive_VaBiEpDoiMatKhau()
    {
        var (result, account) = await _sut.CreateAsync(YeuCauTao("DOCTOR"));

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal("ACTIVE", account!.Status);
        Assert.Equal("DOCTOR", account.Role);

        // BR-03 — tài khoản mới luôn bị buộc đổi mật khẩu ở lần đăng nhập đầu.
        var user = Assert.Single(_saved);
        Assert.True(user.MustChangePassword);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public async Task Tao_MatKhauTam_DuocBAM_KhongLuuThoVaKhongTraVe()
    {
        var (_, account) = await _sut.CreateAsync(YeuCauTao("DOCTOR"));

        var user = Assert.Single(_saved);

        // PRD §6.2 — không ai được thấy mật khẩu dạng đọc được. Chỉ có bản băm trong DB.
        Assert.StartsWith("$2", user.PasswordHash);

        // Và phản hồi trả cho Admin cũng không được kèm mật khẩu — DTO không có trường nào
        // chứa nó, nên chỉ cần khẳng định các trường công khai là đủ.
        Assert.NotNull(account);
        Assert.Equal("0988776655", account!.PhoneNumber);
    }

    [Fact]
    public async Task Tao_MatKhauTam_ThoaChinhSachTDS()
    {
        // TDS §4.3: 8–72 ký tự, ít nhất 1 chữ hoa, ít nhất 1 chữ số.
        // Sinh nhiều lần vì đây là hàm ngẫu nhiên — chạy một lần không chứng minh được gì.
        for (var i = 0; i < 200; i++)
        {
            var matKhau = TemporaryPasswordGenerator.Generate();

            Assert.InRange(matKhau.Length, 8, 72);
            Assert.Contains(matKhau, char.IsUpper);
            Assert.Contains(matKhau, char.IsDigit);
        }
    }

    [Fact]
    public async Task Tao_GuiMatKhauTamQuaEmail_KhiCoEmail()
    {
        await _sut.CreateAsync(YeuCauTao("DOCTOR"));

        _email.Verify(e => e.SendTemporaryPasswordAsync(
            "bs.b@example.com", "BS. Trần Văn B", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Tao_KhongCoEmail_VanTaoDuoc_NhungPhaiBaoLaCHUA_GUI_DUOC()
    {
        // UCS ghi Email là Optional nên vẫn phải cho tạo. Nhưng mật khẩu tạm chỉ đi qua
        // email, nên tài khoản này chưa ai đăng nhập được — báo Success là Admin tưởng xong
        // việc rồi vài ngày sau mới có người kêu không vào được.
        var request = YeuCauTao("DOCTOR");
        request.Email = null;

        var (result, account) = await _sut.CreateAsync(request);

        Assert.Equal(AccountOperationResult.CreatedWithoutEmail, result);

        // Vẫn phải tạo thật, không được nuốt mất.
        Assert.NotNull(account);
        Assert.Single(_saved);

        _email.Verify(e => e.SendTemporaryPasswordAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Tao_GuiMailThatBai_TAI_KHOAN_VAN_TON_TAI_VaBaoDungSuThat()
    {
        // Máy chủ mail hỏng thì KHÔNG được huỷ tài khoản: số điện thoại đã bị chiếm, Admin
        // bấm tạo lại chỉ nhận được "số điện thoại đã tồn tại" rồi không hiểu chuyện gì.
        _email.Setup(e => e.SendTemporaryPasswordAsync(
                  It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);

        var (result, account) = await _sut.CreateAsync(YeuCauTao("DOCTOR"));

        Assert.Equal(AccountOperationResult.CreatedButEmailNotSent, result);
        Assert.NotNull(account);
        Assert.Single(_saved);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Tao_SoDienThoaiDaTonTai_BiTuChoi()
    {
        // AF-03 / BR-02 — số điện thoại là định danh đăng nhập duy nhất.
        _users.Setup(r => r.PhoneExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        var (result, account) = await _sut.CreateAsync(YeuCauTao("DOCTOR"));

        Assert.Equal(AccountOperationResult.PhoneAlreadyUsed, result);
        Assert.Null(account);
        Assert.Empty(_saved);
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("")]
    [InlineData("SUPERUSER")]
    public async Task Tao_VaiTroKhongHopLe_BiTuChoi(string vaiTro)
    {
        // UC-04: tài khoản ADMIN được cấp lúc dựng hệ thống, KHÔNG tạo qua màn này.
        var (result, _) = await _sut.CreateAsync(YeuCauTao(vaiTro));

        Assert.Equal(AccountOperationResult.InvalidRole, result);
        Assert.Empty(_saved);
    }

    // ---------- BR-01: ngày sinh của bệnh nhân ----------

    [Fact]
    public async Task Tao_BenhNhan_NgaySinh_BI_BO_QUA_DuClientCoGuiLen()
    {
        // BR-01 — ngày sinh là dữ liệu y tế, Admin không được chạm. Ẩn ở giao diện là chưa
        // đủ vì gọi thẳng API vẫn gửi lên được, nên tầng nghiệp vụ phải tự loại bỏ.
        var request = YeuCauTao("PATIENT");
        request.DateOfBirth = "1990-05-20";

        var (result, account) = await _sut.CreateAsync(request);

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Null(Assert.Single(_saved).DateOfBirth);
        Assert.Null(account!.DateOfBirth);
    }

    [Fact]
    public async Task Tao_BacSi_NgaySinh_DUOC_LUU()
    {
        // BR-01 chỉ áp cho PATIENT. Bác sĩ và điều dưỡng vẫn khai ngày sinh bình thường.
        var request = YeuCauTao("DOCTOR");
        request.DateOfBirth = "1985-03-10";

        var (_, account) = await _sut.CreateAsync(request);

        Assert.Equal(new DateOnly(1985, 3, 10), Assert.Single(_saved).DateOfBirth);
        Assert.Equal("1985-03-10", account!.DateOfBirth);
    }

    [Fact]
    public async Task LayTheoId_BenhNhan_KHONG_TraVeNgaySinh()
    {
        // Kể cả khi trong DB có sẵn ngày sinh (do dữ liệu cũ, hoặc do vai trò vừa bị đổi),
        // giao diện quản trị vẫn không được thấy.
        var user = TaoUserTrongDb(UserRole.Patient);
        user.DateOfBirth = new DateOnly(1990, 5, 20);
        SetupGetById(user);

        var account = await _sut.GetByIdAsync(user.UserId, Guid.NewGuid());

        Assert.Null(account!.DateOfBirth);
    }

    [Fact]
    public async Task LayTheoId_DanhDau_Dung_Dong_Cua_Chinh_Admin()
    {
        // Để giao diện ẩn nút khoá và vô hiệu hoá trên dòng của chính người đang đăng nhập —
        // backend vốn đã chặn (AF-04), bày ra nút chắc chắn báo lỗi chỉ làm người dùng bối rối.
        var user = TaoUserTrongDb(UserRole.Admin);
        SetupGetById(user);

        var chinhMinh = await _sut.GetByIdAsync(user.UserId, user.UserId);
        var nguoiKhac = await _sut.GetByIdAsync(user.UserId, Guid.NewGuid());

        Assert.True(chinhMinh!.IsCurrentUser);
        Assert.False(nguoiKhac!.IsCurrentUser);
    }

    // ---------- FT-08: khoá / mở khoá / vô hiệu hoá ----------

    [Fact]
    public async Task Khoa_TaiKhoanDangHoatDong_ChuyenSangLocked()
    {
        var user = TaoUserTrongDb(UserRole.Doctor);
        SetupGetById(user);

        var result = await _sut.SetLockedAsync(user.UserId, locked: true, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal(UserStatus.Locked, user.Status);
    }

    [Fact]
    public async Task MoKhoa_TaiKhoanBiKhoa_QuayLaiActive()
    {
        // BR-04 — đây là đường DUY NHẤT từ Locked về Active, và phải do Admin bấm tay.
        var user = TaoUserTrongDb(UserRole.Doctor);
        user.Status = UserStatus.Locked;
        SetupGetById(user);

        var result = await _sut.SetLockedAsync(user.UserId, locked: false, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public async Task VoHieuHoa_ChuyenSangDeactivated_VaKHONG_XOA_BanGhi()
    {
        // BR-05 — không bao giờ xoá cứng; dữ liệu liên quan phải còn truy cập được.
        var user = TaoUserTrongDb(UserRole.Patient);
        SetupGetById(user);

        var result = await _sut.DeactivateAsync(user.UserId, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal(UserStatus.Deactivated, user.Status);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VoHieuHoaRoi_KhongMoKhoaLaiDuoc()
    {
        // BR-05 — Deactivated là trạng thái cuối, PRD không định nghĩa đường kích hoạt lại.
        var user = TaoUserTrongDb(UserRole.Doctor);
        user.Status = UserStatus.Deactivated;
        SetupGetById(user);

        var result = await _sut.SetLockedAsync(user.UserId, locked: false, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.AccountIsDeactivated, result);
        Assert.Equal(UserStatus.Deactivated, user.Status);
    }

    [Fact]
    public async Task Admin_KhongTuKhoaChinhMinh()
    {
        // UC-04 AF-04 để ngỏ trường hợp này. Cho phép thì Admin tự nhốt mình ra ngoài hệ
        // thống và không còn ai mở ra được.
        var adminId = Guid.NewGuid();

        var result = await _sut.SetLockedAsync(adminId, locked: true, adminId);

        Assert.Equal(AccountOperationResult.CannotTargetSelf, result);
    }

    [Fact]
    public async Task Admin_VO_HIEU_HOA_DUOC_Admin_KHAC()
    {
        // UC-04 AF-04 — nhóm chốt ngày 31/07/2026: Admin được vô hiệu hoá Admin khác.
        // Chỉ cấm thao tác lên chính mình.
        var adminKhac = TaoUserTrongDb(UserRole.Admin);
        SetupGetById(adminKhac);

        var result = await _sut.DeactivateAsync(adminKhac.UserId, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal(UserStatus.Deactivated, adminKhac.Status);
    }

    [Fact]
    public async Task Admin_KHOA_DUOC_Admin_KHAC()
    {
        var adminKhac = TaoUserTrongDb(UserRole.Admin);
        SetupGetById(adminKhac);

        var result = await _sut.SetLockedAsync(adminKhac.UserId, locked: true, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal(UserStatus.Locked, adminKhac.Status);
    }

    [Fact]
    public async Task Admin_KhongTuVoHieuHoaChinhMinh()
    {
        var adminId = Guid.NewGuid();

        var result = await _sut.DeactivateAsync(adminId, adminId);

        Assert.Equal(AccountOperationResult.CannotTargetSelf, result);
    }

    [Fact]
    public async Task Khoa_TaiKhoanKhongTonTai_TraVeNotFound()
    {
        SetupGetById(null);

        var result = await _sut.SetLockedAsync(Guid.NewGuid(), locked: true, Guid.NewGuid());

        Assert.Equal(AccountOperationResult.NotFound, result);
    }

    // ---------- FT-09: phân quyền ----------

    [Fact]
    public async Task PhanQuyen_DoiVaiTroSangDieuDuong()
    {
        var user = TaoUserTrongDb(UserRole.Doctor);
        SetupGetById(user);

        var result = await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Vũ Thị Cẩm Tú",
            Role = "NURSE",
        });

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal(UserRole.Nurse, user.Role);
    }

    [Fact]
    public async Task PhanQuyen_KHONG_DOI_SoDienThoaiVaTrangThai()
    {
        // BR-02 — số điện thoại là định danh đăng nhập. Trạng thái đi qua endpoint riêng.
        var user = TaoUserTrongDb(UserRole.Doctor);
        user.Status = UserStatus.Locked;
        var soCu = user.Phone;
        SetupGetById(user);

        await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Tên Mới",
            Role = "DOCTOR",
        });

        Assert.Equal(soCu, user.Phone);
        Assert.Equal(UserStatus.Locked, user.Status);
    }

    [Fact]
    public async Task PhanQuyen_DoiSangBenhNhan_ThiXOA_NgaySinh()
    {
        // Đổi vai trò sang PATIENT thì ngày sinh đang có phải bị xoá, nếu không Admin vẫn
        // đọc được dữ liệu y tế của tài khoản đó (BR-01).
        var user = TaoUserTrongDb(UserRole.Doctor);
        user.DateOfBirth = new DateOnly(1985, 3, 10);
        SetupGetById(user);

        await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Nguyễn Văn A",
            Role = "PATIENT",
            DateOfBirth = "1985-03-10",
        });

        Assert.Null(user.DateOfBirth);
    }

    [Theory]
    [InlineData("DOCTOR")]
    [InlineData("NURSE")]
    [InlineData("PATIENT")]
    public async Task PhanQuyen_KHONG_DUOC_HA_QUYEN_ADMIN(string vaiTroMoi)
    {
        // Lỗ nguy hiểm nhất của màn này trước khi vá: ô vai trò trên form chỉ có Bác sĩ,
        // Điều dưỡng, Bệnh nhân. Mở một tài khoản Admin ra sửa thì ô đó rơi về giá trị đầu
        // danh sách, chỉ cần bấm Lưu để đổi cái tên là mất luôn quyền quản trị — không cảnh
        // báo, không hoàn tác được. Mất Admin cuối cùng là không còn ai tạo lại được nữa.
        var user = TaoUserTrongDb(UserRole.Admin);
        SetupGetById(user);

        var result = await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Quản trị viên",
            Role = vaiTroMoi,
        });

        Assert.Equal(AccountOperationResult.CannotChangeAdminRole, result);
        Assert.Equal(UserRole.Admin, user.Role);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PhanQuyen_KHONG_DUOC_PHONG_ADMIN_QuaManNay()
    {
        // Chiều ngược lại. UC-04 ghi "Admin accounts are not created on this screen" —
        // không cho tạo thì cũng không được đi cửa sau bằng cách sửa vai trò.
        var user = TaoUserTrongDb(UserRole.Doctor);
        SetupGetById(user);

        var result = await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Nguyễn Văn A",
            Role = "ADMIN",
        });

        Assert.Equal(AccountOperationResult.CannotChangeAdminRole, result);
        Assert.Equal(UserRole.Doctor, user.Role);
    }

    [Fact]
    public async Task PhanQuyen_ADMIN_VAN_SUA_DUOC_TEN_VA_EMAIL()
    {
        // Khoá vai trò nhưng không được khoá luôn cả form: sửa tên hay email của tài khoản
        // Admin vẫn phải chạy, miễn là vai trò giữ nguyên ADMIN.
        var user = TaoUserTrongDb(UserRole.Admin);
        SetupGetById(user);

        var result = await _sut.UpdateAsync(user.UserId, new UpdateUserAccountRequest
        {
            FullName = "Nguyễn Quý Hiếu",
            Role = "ADMIN",
            Email = "admin@example.com",
        });

        Assert.Equal(AccountOperationResult.Success, result);
        Assert.Equal("Nguyễn Quý Hiếu", user.FullName);
        Assert.Equal("admin@example.com", user.Email);
        Assert.Equal(UserRole.Admin, user.Role);
    }

    // ---------- helpers ----------

    private static CreateUserAccountRequest YeuCauTao(string vaiTro) => new()
    {
        PhoneNumber = "0988776655",
        FullName = "BS. Trần Văn B",
        Role = vaiTro,
        Email = "bs.b@example.com",
    };

    private static User TaoUserTrongDb(UserRole vaiTro) => new()
    {
        UserId = Guid.NewGuid(),
        Phone = "0912345678",
        FullName = "Nguyễn Văn A",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Aa123456@"),
        Role = vaiTro,
        Status = UserStatus.Active,
    };

    private void SetupGetById(User? user) =>
        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);
}
