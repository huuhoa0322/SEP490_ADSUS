using System;

namespace ADSUS_BE.BLL.Common;

public static class IoUCalculator
{
    public static decimal Calculate(
        decimal xmin1, decimal ymin1, decimal xmax1, decimal ymax1,
        decimal xmin2, decimal ymin2, decimal xmax2, decimal ymax2)
    {
        var xA = Math.Max(xmin1, xmin2);
        var yA = Math.Max(ymin1, ymin2);
        var xB = Math.Min(xmax1, xmax2);
        var yB = Math.Min(ymax1, ymax2);

        var interArea = Math.Max(0, xB - xA) * Math.Max(0, yB - yA);

        var box1Area = (xmax1 - xmin1) * (ymax1 - ymin1);
        var box2Area = (xmax2 - xmin2) * (ymax2 - ymin2);

        var unionArea = box1Area + box2Area - interArea;
        
        if (unionArea == 0) return 0;
        
        return interArea / unionArea;
    }
}
