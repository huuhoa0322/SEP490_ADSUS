using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// 1 ca bệnh có NHIỀU ảnh (FT-13). File nhị phân nằm ngoài DB — file_ref là đường dẫn lưu trữ; ràng buộc dung lượng/định dạng kiểm ở tầng ứng dụng (TDS).
/// </summary>
public partial class UltrasoundImage
{
    public Guid ImageId { get; set; }

    public Guid CaseId { get; set; }

    public string FileRef { get; set; } = null!;

    public DateTime UploadedAt { get; set; }

    public string? Note { get; set; }

    public virtual ICollection<AiPrediction> AiPredictions { get; set; } = new List<AiPrediction>();

    public virtual Case Case { get; set; } = null!;

    public virtual ICollection<DoctorAnnotation> DoctorAnnotations { get; set; } = new List<DoctorAnnotation>();
}
