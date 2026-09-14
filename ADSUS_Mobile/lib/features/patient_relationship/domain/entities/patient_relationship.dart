/// Entity đại diện cho một người thân đã được lưu trong danh bạ.
///
/// Dùng trong màn hình:
///   - My Relatives: xem danh sách người thân
///   - Book Appointment: chọn người thân để đặt lịch hộ
class PatientRelationship {
  const PatientRelationship({
    required this.relationshipId,
    required this.patientProfileId,
    required this.fullName,
    this.phone,
    this.dateOfBirth,
    this.relationshipName,
    required this.createdAt,
  });

  final String relationshipId;
  final String patientProfileId;
  final String fullName;
  final String? phone;
  final DateTime? dateOfBirth;
  final String? relationshipName;
  final DateTime createdAt;

  /// Tên hiển thị với nhãn quan hệ (VD: "Mẹ - Nguyễn Thị A")
  String get displayName {
    if (relationshipName != null && relationshipName!.isNotEmpty) {
      return '$relationshipName - $fullName';
    }
    return fullName;
  }

  /// Tính tuổi từ ngày sinh (nếu có)
  int? get age {
    if (dateOfBirth == null) return null;
    final now = DateTime.now();
    int age = now.year - dateOfBirth!.year;
    if (now.month < dateOfBirth!.month ||
        (now.month == dateOfBirth!.month && now.day < dateOfBirth!.day)) {
      age--;
    }
    return age;
  }
}
