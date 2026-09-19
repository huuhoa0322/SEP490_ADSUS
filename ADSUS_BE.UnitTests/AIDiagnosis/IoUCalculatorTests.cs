using ADSUS_BE.BLL.Common;
using Xunit;

namespace ADSUS_BE.UnitTests.AIDiagnosis;

public class IoUCalculatorTests
{
    [Fact]
    public void MatchThreshold_IsHalf_StandardMap50()
    {
        Assert.Equal(0.5m, IoUCalculator.MatchThreshold);
    }

    [Fact]
    public void Calculate_IdenticalBoxes_ReturnsOne()
    {
        // Box [10, 10, 50, 50]
        var iou = IoUCalculator.Calculate(10, 10, 50, 50, 10, 10, 50, 50);
        Assert.Equal(1.0m, iou);
    }

    [Fact]
    public void Calculate_DisjointBoxes_ReturnsZero()
    {
        // Box 1: [0, 0, 10, 10], Box 2: [20, 20, 30, 30]
        var iou = IoUCalculator.Calculate(0, 0, 10, 10, 20, 20, 30, 30);
        Assert.Equal(0m, iou);
    }

    [Fact]
    public void Calculate_BoxesTouchingAtEdge_ReturnsZero()
    {
        // Box 1: [0, 0, 10, 10], Box 2: [10, 0, 20, 10] (touch at x=10)
        var iou = IoUCalculator.Calculate(0, 0, 10, 10, 10, 0, 20, 10);
        Assert.Equal(0m, iou);
    }

    [Fact]
    public void Calculate_PartialOverlap_ReturnsAccurateRatio()
    {
        // Box 1: [0, 0, 2, 2] -> Area = 4
        // Box 2: [1, 0, 3, 2] -> Area = 4
        // Intersection: [1, 0, 2, 2] -> Area = 1 * 2 = 2
        // Union: 4 + 4 - 2 = 6
        // IoU: 2 / 6 = 1 / 3
        var iou = IoUCalculator.Calculate(0, 0, 2, 2, 1, 0, 3, 2);
        Assert.Equal(2m / 6m, iou);
    }

    [Fact]
    public void Calculate_NestedBox_ReturnsInnerAreaDividedByOuter()
    {
        // Box 1 (Outer): [0, 0, 10, 10] -> Area = 100
        // Box 2 (Inner): [2, 2, 6, 6] -> Area = 4 * 4 = 16
        // Intersection: 16
        // Union: 100 + 16 - 16 = 100
        // IoU: 16 / 100 = 0.16
        var iou = IoUCalculator.Calculate(0, 0, 10, 10, 2, 2, 6, 6);
        Assert.Equal(0.16m, iou);
    }

    [Fact]
    public void Calculate_IsSymmetric()
    {
        var iou1 = IoUCalculator.Calculate(0, 0, 20, 30, 10, 10, 40, 50);
        var iou2 = IoUCalculator.Calculate(10, 10, 40, 50, 0, 0, 20, 30);
        Assert.Equal(iou1, iou2);
    }

    [Fact]
    public void Calculate_ZeroAreaBox_ReturnsZeroWithoutDivisionByZero()
    {
        // Box 1: [5, 5, 5, 5] (point: width=0, height=0)
        var iou = IoUCalculator.Calculate(5, 5, 5, 5, 0, 0, 10, 10);
        Assert.Equal(0m, iou);
    }
}
