import 'medical_record_image.dart';
import 'medical_record_prescription.dart';

/// Trạng thái của một ca khám (Case) — theo đúng state machine backend (Module 04).
///
/// Đính chính 14/08/2026 (sau khi trao đổi lại): Patient (Mobile) CHỈ BAO GIỜ thấy
/// `end` — GET /cases/me ép cứng phía server chỉ trả ca `end`, GET /cases/{id} trả 404
/// nếu Case của chính Patient đó chưa `end` (kể cả đã `confirmed` nhưng chưa kê đơn).
/// `end` = đã Confirmed VÀ đã được kê đơn thuốc — coi là "đã hoàn tất lượt khám".
enum CaseStatus { created, confirmed, end }

/// Chi tiết đầy đủ 1 lượt khám mà Patient được xem — UC-08, SCR-14.
///
/// Đính chính 15/08/2026: trước đây chỉ có `conclusion` + đơn thuốc tối thiểu, dựa theo UCS
/// văn bản (hẹp). Quyết định 01/08/2026 thật sự (comment CasesController.cs's ExportReport,
/// xem design spec 2026-08-15) là Patient xem được CÙNG NỘI DUNG như PDF export — thêm
/// `doctorName`, `finalDiagnosis`, `images` (ảnh siêu âm GỐC, không phải ảnh khoanh vùng AI —
/// tính năng đó chưa tồn tại). Vẫn KHÔNG có `clinicalInfo`, không có AI Result thô (GB-05).
class MedicalRecordCase {
  const MedicalRecordCase({
    required this.caseId,
    required this.visitDate,
    required this.status,
    required this.doctorId,
    required this.doctorName,
    this.finalDiagnosis,
    this.doctorConclusion,
    this.prescription,
    this.images = const [],
  });

  final String caseId;
  final DateTime visitDate;
  final CaseStatus status;
  final String doctorId;
  final String doctorName;

  /// Chẩn đoán của bác sĩ — "CHẨN ĐOÁN" trong PDF export (`CaseReportService.cs`).
  final String? finalDiagnosis;

  /// Hướng xử trí/kết luận của bác sĩ — "HƯỚNG XỬ TRÍ" trong PDF export. Đổi tên từ
  /// `conclusion` (15/08/2026) — khớp đúng tên field backend thật (`DoctorConclusion`),
  /// tránh lặp lại bug lệch tên field từng khiến trường này không bao giờ đọc được.
  final String? doctorConclusion;

  /// Đơn thuốc kèm theo lượt khám — đính chính 15/08/2026: trước đây chỉ có
  /// `prescriptionId`/`prescriptionStatus` (badge tĩnh), giờ có đủ chi tiết vì Module 7
  /// xác nhận chưa có màn riêng để điều hướng tới.
  final MedicalRecordPrescription? prescription;
  final List<MedicalRecordImage> images;
}
