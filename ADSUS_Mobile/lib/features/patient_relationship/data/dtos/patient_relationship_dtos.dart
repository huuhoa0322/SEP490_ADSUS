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
      relationshipId: json['relationshipId'] as String,
      patientProfileId: json['patientProfileId'] as String,
      fullName: json['fullName'] as String,
      phone: json['phone'] as String?,
      dateOfBirth: json['dateOfBirth'] as String?,
      relationshipName: json['relationshipName'] as String?,
      createdAt: json['createdAt'] as String,
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
          dateOfBirth != null ? DateTime.parse(dateOfBirth!) : null,
      relationshipName: relationshipName,
      createdAt: DateTime.parse(createdAt),
    );
  }
}

/// Request body để thêm người thân mới.
class AddRelativeRequest {
  const AddRelativeRequest({
    required this.fullName,
    required this.phone,
    this.dateOfBirth,
    this.relationshipName,
  });

  final String fullName;
  final String phone;
  final String? dateOfBirth;
  final String? relationshipName;

  Map<String, dynamic> toJson() {
    return {
      'fullName': fullName,
      'phone': phone,
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
