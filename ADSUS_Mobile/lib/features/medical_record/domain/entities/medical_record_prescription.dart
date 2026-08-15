/// 1 loại thuốc trong đơn — mirror `PrescriptionItemSummary` (API Spec module 04).
class MedicalRecordPrescriptionItem {
  const MedicalRecordPrescriptionItem({
    required this.medicineName,
    required this.dosage,
    required this.durationDays,
    required this.startDate,
    this.instructions,
  });

  final String medicineName;
  final String dosage;
  final int durationDays;
  final DateTime startDate;
  final String? instructions;
}

/// Đơn thuốc đầy đủ kèm 1 lượt khám — mirror `PrescriptionSummary` (API Spec module 04).
/// Đính chính 15/08/2026: Module 7 chưa có màn xem chi tiết đơn thuốc riêng, nên SCR-14 hiện
/// thẳng đủ thông tin thay vì chỉ 1 badge trạng thái.
class MedicalRecordPrescription {
  const MedicalRecordPrescription({
    required this.prescriptionId,
    required this.status,
    required this.prescribedDate,
    this.generalNote,
    this.items = const [],
  });

  final String prescriptionId;
  final String status;
  final DateTime prescribedDate;
  final String? generalNote;
  final List<MedicalRecordPrescriptionItem> items;
}
