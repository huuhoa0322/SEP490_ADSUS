/// Trạng thái của một ca khám (Case) — theo đúng state machine backend (Module 04).
///
/// Patient (Mobile) chỉ bao giờ thấy `confirmed` — GET /cases/me ép cứng phía server,
/// GET /cases/{id} trả 404 nếu Case của chính Patient đó chưa Confirmed (UC-08 AF-01).
enum CaseStatus { created, analyzed, confirmed }

/// Chi tiết đầy đủ 1 lượt khám mà Patient được xem — UC-08, SCR-14.
///
/// Chỉ chứa đúng những field backend trả cho Patient (GET /cases/{id}, API Spec #23):
/// không có ảnh siêu âm, không có AI Result thô (GB-05), không có tên bác sĩ (backend
/// hiện chưa trả trường này — chỉ có doctorId).
class MedicalRecordCase {
  const MedicalRecordCase({
    required this.caseId,
    required this.visitDate,
    required this.status,
    required this.doctorId,
    this.conclusion,
    this.prescriptionId,
    this.prescriptionStatus,
  });

  final String caseId;
  final DateTime visitDate;
  final CaseStatus status;
  final String doctorId;

  /// Kết luận của bác sĩ — nội dung DUY NHẤT về chẩn đoán mà Patient được xem (GB-05).
  final String? conclusion;

  /// Tóm tắt đơn thuốc tối thiểu — chi tiết đầy đủ thuộc Module 07 (chưa có màn để
  /// điều hướng tới), nên chỉ hiển thị dạng badge tĩnh, không bấm được.
  final String? prescriptionId;
  final String? prescriptionStatus;
}
