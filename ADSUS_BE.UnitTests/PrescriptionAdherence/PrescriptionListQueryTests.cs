using ADSUS_BE.BLL.PrescriptionAdherence.DTOs;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.UnitTests.PrescriptionAdherence;

/// <summary>
/// Module 7 — test mapping StatusFilter string → enum list cho repository filter.
/// UC-11: ALL → empty list, ACTIVE → [Active], COMPLETED → [Completed].
/// </summary>
public class PrescriptionListQueryTests
{
    private static readonly Guid PatientId = Guid.NewGuid();

    [Fact]
    public void NullStatusFilter_Resolves_ToEmpty()
    {
        var q = new PrescriptionListQuery(PatientId, null, null, null, 1, 20);
        Assert.Empty(q.ResolvedStatuses);
    }

    [Theory]
    [InlineData("ALL")]
    [InlineData("all")]
    [InlineData("")]
    [InlineData("invalid")]
    public void NonMatchingStatus_Resolves_ToEmpty(string filter)
    {
        var q = new PrescriptionListQuery(PatientId, filter, null, null, 1, 20);
        Assert.Empty(q.ResolvedStatuses);
    }

    [Fact]
    public void ActiveString_Resolves_ToActiveEnum()
    {
        var q = new PrescriptionListQuery(PatientId, "ACTIVE", null, null, 1, 20);
        Assert.Single(q.ResolvedStatuses);
        Assert.Contains(PrescriptionStatus.Active, q.ResolvedStatuses);
    }

    [Fact]
    public void CompletedString_Resolves_ToCompletedEnum()
    {
        var q = new PrescriptionListQuery(PatientId, "COMPLETED", null, null, 1, 20);
        Assert.Single(q.ResolvedStatuses);
        Assert.Contains(PrescriptionStatus.Completed, q.ResolvedStatuses);
    }

    [Fact]
    public void ActiveLowercase_AlsoResolves()
    {
        var q = new PrescriptionListQuery(PatientId, "active", null, null, 1, 20);
        Assert.Contains(PrescriptionStatus.Active, q.ResolvedStatuses);
    }
}
