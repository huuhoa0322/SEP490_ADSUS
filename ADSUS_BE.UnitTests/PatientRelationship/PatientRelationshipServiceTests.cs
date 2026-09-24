using ADSUS_BE.BLL.PatientRelationship.DTOs;
using ADSUS_BE.BLL.PatientRelationship.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ADSUS_BE.UnitTests.Relatives;

/// <summary>
/// PatientRelationshipService chạy trên repository + PatientProfileService THẬT (DB InMemory):
/// hồ sơ người thân (guest profile) được tạo/nhận lại/sửa qua module MedicalRecord, kiểm tra người
/// giám hộ nằm ở service thay vì controller đọc thẳng DbContext (P11 review 24/09/2026).
/// </summary>
public class PatientRelationshipServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PatientRelationshipService _sut;

    public PatientRelationshipServiceTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _sut = PatientAccountTestServices.Relationship(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private User SeedUser(UserRole role, string phone)
    {
        var user = new User
        {
            UserId = Guid.NewGuid(),
            FullName = role.ToString(),
            Phone = phone,
            PasswordHash = "hash",
            Role = role,
            Status = UserStatus.Active,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task AddRelativeForGuardianAsync_UnknownUser_ReturnsNullAndCreatesNothing()
    {
        var result = await _sut.AddRelativeForGuardianAsync(
            new AddRelativeRequest("Bà Ngoại", null, null, "Bà"), Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Empty(await _db.PatientRelationships.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddRelativeForGuardianAsync_GuardianIsNotPatient_ReturnsNull()
    {
        var nurse = SeedUser(UserRole.Staff, "0901000001");

        var result = await _sut.AddRelativeForGuardianAsync(
            new AddRelativeRequest("Bà Ngoại", null, null, "Bà"), nurse.UserId, TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Empty(await _db.PatientRelationships.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddRelativeForGuardianAsync_Patient_CreatesGuestProfileAndRelationship()
    {
        var mother = SeedUser(UserRole.Patient, "0901000002");

        var result = await _sut.AddRelativeForGuardianAsync(
            new AddRelativeRequest("Bé Na", null, new DateOnly(2024, 1, 2), "Con"), mother.UserId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Bé Na", result!.PatientName);
        Assert.False(result.IsRegisteredAccount);

        var relationship = await _db.PatientRelationships.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(mother.UserId, relationship.UserId);
        var profile = await _db.PatientProfiles.SingleAsync(p => p.PatientProfileId == relationship.PatientProfileId, TestContext.Current.CancellationToken);
        Assert.Null(profile.UserId);
        Assert.Equal(mother.UserId, profile.CreatedBy);
    }

    [Fact]
    public async Task AddRelativeAsync_SamePhoneAddedByTwoUsers_ReusesOneGuestProfile()
    {
        var sister = SeedUser(UserRole.Patient, "0901000003");
        var brother = SeedUser(UserRole.Patient, "0901000004");
        var request = new AddRelativeRequest("Mẹ", "0909999999", null, "Mẹ");

        var first = await _sut.AddRelativeAsync(request, sister.UserId, TestContext.Current.CancellationToken);
        var second = await _sut.AddRelativeAsync(request, brother.UserId, TestContext.Current.CancellationToken);

        Assert.Equal(first.PatientProfileId, second.PatientProfileId);
        Assert.Single(await _db.PatientProfiles.Where(p => p.UserId == null).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await _db.PatientRelationships.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddRelativeAsync_SameRelativeTwice_Throws()
    {
        var user = SeedUser(UserRole.Patient, "0901000005");
        var request = new AddRelativeRequest("Mẹ", "0909999998", null, "Mẹ");
        await _sut.AddRelativeAsync(request, user.UserId, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddRelativeAsync(request, user.UserId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateRelativeAsync_GuestProfile_UpdatesProfileAndRelationshipName()
    {
        var user = SeedUser(UserRole.Patient, "0901000006");
        var added = await _sut.AddRelativeAsync(
            new AddRelativeRequest("Ông", "0909999997", null, "Ông nội"), user.UserId, TestContext.Current.CancellationToken);

        var updated = await _sut.UpdateRelativeAsync(
            added.RelationshipId,
            new UpdateRelativeRequest("Ông Nội Minh", "", new DateOnly(1950, 5, 6), "Ông"),
            user.UserId,
            TestContext.Current.CancellationToken);

        Assert.Equal("Ông Nội Minh", updated.PatientName);
        Assert.Null(updated.PatientPhone); // chuỗi rỗng = xoá số
        Assert.Equal(new DateOnly(1950, 5, 6), updated.DateOfBirth);
        Assert.Equal("Ông", updated.RelationshipName);
    }

    [Fact]
    public async Task UpdateRelativeAsync_NewPhoneBelongsToAccount_ThrowsAndKeepsOldData()
    {
        var user = SeedUser(UserRole.Patient, "0901000007");
        var registered = SeedUser(UserRole.Patient, "0901000008");
        var added = await _sut.AddRelativeAsync(
            new AddRelativeRequest("Cô", "0909999996", null, "Cô"), user.UserId, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateRelativeAsync(
            added.RelationshipId,
            new UpdateRelativeRequest(null, registered.Phone, null, null),
            user.UserId,
            TestContext.Current.CancellationToken));
    }
}
