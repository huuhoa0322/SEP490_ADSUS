/// Một ảnh siêu âm gốc thuộc 1 lượt khám — mirror `UltrasoundImageResponse` (API Spec module 04).
///
/// `imageUrl` nullable: Storage ký URL có thể fail (file không còn tồn tại) — phải hiện ô
/// hỏng tử tế thay vì `Image.network(null)` vỡ hình (xem `MedicalRecordDetailScreen`).
class MedicalRecordImage {
  const MedicalRecordImage({
    required this.imageId,
    required this.uploadedAt,
    this.imageUrl,
    this.note,
  });

  final String imageId;
  final DateTime uploadedAt;
  final String? imageUrl;
  final String? note;
}
