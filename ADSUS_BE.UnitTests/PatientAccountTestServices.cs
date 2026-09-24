using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.BLL.PatientRelationship.Services;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Implementations;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ADSUS_BE.UnitTests;

/// <summary>
/// PatientRelationshipService/AuthService/PatientSelfRegistrationService trước đây ghi thẳng
/// PatientProfiles/Users/PatientRelationships qua AppDbContext; nay đi qua PatientProfileService,
/// repository và IUnitOfWork (P11 review 24/09/2026). Helper dựng các phần đó trên repository THẬT
/// chạy cùng DB InMemory mà test đã nạp dữ liệu, và nối các method mới của mock sang repository thật
/// để test cũ (mock repository nhưng kiểm tra dữ liệu trong DB) giữ nguyên ý nghĩa.
/// </summary>
internal static class PatientAccountTestServices
{
    public static PatientProfileService PatientProfiles(AppDbContext db) =>
        new(new PatientProfileRepository(db), new UserRepository(db), NullLogger<PatientProfileService>.Instance);

    public static PatientRelationshipService Relationship(AppDbContext db) =>
        new(new PatientRelationshipRepository(db), PatientProfiles(db), new UserRepository(db), new UnitOfWork(db));

    /// <summary>Như <see cref="Relationship(AppDbContext)"/> nhưng giữ mock repository của test (các method mới nối sang repo thật).</summary>
    public static PatientRelationshipService Relationship(Mock<IPatientRelationshipRepository> repositoryMock, AppDbContext db) =>
        new(repositoryMock.BackedBy(db).Object, PatientProfiles(db), new UserRepository(db), new UnitOfWork(db));

    public static Mock<IPatientRelationshipRepository> BackedBy(this Mock<IPatientRelationshipRepository> mock, AppDbContext db)
    {
        var real = new PatientRelationshipRepository(db);

        mock.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid userId, Guid profileId, CancellationToken ct) => real.ExistsAsync(userId, profileId, ct));
        mock.Setup(r => r.GetByIdAndUserForUpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid relationshipId, Guid userId, CancellationToken ct) => real.GetByIdAndUserForUpdateAsync(relationshipId, userId, ct));
        mock.Setup(r => r.StageAddAsync(It.IsAny<PatientRelationship>(), It.IsAny<CancellationToken>()))
            .Returns((PatientRelationship relationship, CancellationToken ct) => real.StageAddAsync(relationship, ct));

        return mock;
    }

    /// <summary>Mock IUserRepository của test: AddAsync thêm thật vào DB (trước đây service tự _db.Users.Add).</summary>
    public static Mock<IUserRepository> AddsTo(this Mock<IUserRepository> mock, AppDbContext db)
    {
        mock.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns((User user, CancellationToken _) =>
            {
                db.Users.Add(user);
                return Task.CompletedTask;
            });
        return mock;
    }
}
