using ADSUS_BE.BLL.CaseClinicServices;
using ADSUS_BE.BLL.ClinicServiceManagement;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.Services;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.ExternalServices;
using ADSUS_BE.DAL.Repositories.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ADSUS_BE.UnitTests;

/// <summary>
/// ClinicServiceManagementService/CaseClinicServiceService trước đây nhận thẳng AppDbContext; nay
/// nhận repository + service của module khác (P11 review 24/09/2026). Helper này dựng chúng trên
/// repository THẬT chạy cùng DB InMemory mà test đã nạp dữ liệu; CaseService và InvoiceService phía
/// sau cũng là bản thật (mock phần ngoài như lưu file, thông báo, kho).
/// </summary>
internal static class ClinicServiceTestServices
{
    public static ClinicServiceManagementService Management(AppDbContext db) =>
        new(new ClinicServiceRepository(db), NullLogger<ClinicServiceManagementService>.Instance);

    public static CaseClinicServiceService CaseClinicService(AppDbContext db)
    {
        var cases = new Lazy<ICaseService>(() => new CaseService(
            new CaseRepository(db),
            new UltrasoundImageRepository(db),
            new PatientProfileRepository(db),
            new UserRepository(db),
            new PatientRelationshipRepository(db),
            new Lazy<IFileStorageService>(() => Mock.Of<IFileStorageService>()),
            Mock.Of<INotificationService>(),
            NullLogger<CaseService>.Instance));

        var invoices = new Lazy<IInvoiceService>(() => PrescriptionAdherenceTestServices.Invoice(
            db,
            Mock.Of<IInventoryService>(),
            null!,
            Mock.Of<IMedicationIntakeScheduleGenerator>(),
            Mock.Of<INotificationService>()));

        return new CaseClinicServiceService(
            new CaseClinicServiceRepository(db),
            Management(db),
            cases,
            invoices,
            new UnitOfWork(db),
            NullLogger<CaseClinicServiceService>.Instance);
    }
}
