import '../../domain/entities/patient_relationship.dart';

/// DTO nhận từ backend cho PatientRelationship.
class PatientRelationshipDTO {
  const PatientRelationshipDTO({
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
  final String? dateOfBirth;
  final String? relationshipName;
  final String createdAt;

  factory PatientRelationshipDTO.fromJson(Map<String, dynamic> json) {
    return PatientRelationshipDTO(
      relationshipId:
          (json['relationshipId'] ?? json['id'] ?? '').toString(),
      patientProfileId:
          (json['patientProfileId'] ?? json['patientId'] ?? '').toString(),
      fullName:
          (json['fullName'] ?? json['patientName'] ?? '') as String,
      phone: (json['phone'] ?? json['patientPhone']) as String?,
      dateOfBirth: json['dateOfBirth'] as String?,
      relationshipName: json['relationshipName'] as String?,
      createdAt: json['createdAt']?.toString() ??
          DateTime.now().toIso8601String(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'relationshipId': relationshipId,
      'patientProfileId': patientProfileId,
      'fullName': fullName,
      if (phone != null) 'phone': phone,
      if (dateOfBirth != null) 'dateOfBirth': dateOfBirth,
      if (relationshipName != null) 'relationshipName': relationshipName,
      'createdAt': createdAt,
    };
  }

  PatientRelationship toEntity() {
    return PatientRelationship(
      relationshipId: relationshipId,
      patientProfileId: patientProfileId,
      fullName: fullName,
      phone: phone,
      dateOfBirth:
          dateOfBirth != null ? DateTime.tryParse(dateOfBirth!) : null,
      relationshipName: relationshipName,
      createdAt: DateTime.tryParse(createdAt) ?? DateTime.now(),
    );
  }
}

/// Request body để thêm người thân mới.
class AddRelativeRequest {
  const AddRelativeRequest({
    required this.fullName,
    this.phone,
    this.dateOfBirth,
    this.relationshipName,
  });

  final String fullName;
  final String? phone;
  final String? dateOfBirth;
  final String? relationshipName;

  Map<String, dynamic> toJson() {
    return {
      'fullName': fullName,
      if (phone != null && phone!.trim().isNotEmpty) 'phone': phone!.trim(),
      if (dateOfBirth != null) 'dateOfBirth': dateOfBirth,
      if (relationshipName != null && relationshipName!.isNotEmpty)
        'relationshipName': relationshipName,
    };
  }
}

/// Request body để cập nhật người thân.
class UpdateRelativeRequest {
  const UpdateRelativeRequest({
    this.fullName,
    this.phone,
    this.dateOfBirth,
    this.relationshipName,
  });

  final String? fullName;
  final String? phone;
  final String? dateOfBirth;
  final String? relationshipName;

  Map<String, dynamic> toJson() {
    final map = <String, dynamic>{};
    if (fullName != null) map['fullName'] = fullName;
    if (phone != null) map['phone'] = phone;
    if (dateOfBirth != null) map['dateOfBirth'] = dateOfBirth;
    if (relationshipName != null) map['relationshipName'] = relationshipName;
    return map;
  }
}
