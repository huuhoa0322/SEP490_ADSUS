/// DTO cho SymptomCategory từ backend GET /api/v1/symptoms/categories
class SymptomCategoryDto {
  final String? categoryId;
  final String? name;
  final bool? isOther;
  final List<SymptomDto>? symptoms;

  SymptomCategoryDto({
    this.categoryId,
    this.name,
    this.isOther,
    this.symptoms,
  });

  factory SymptomCategoryDto.fromJson(Map<String, dynamic> json) {
    return SymptomCategoryDto(
      categoryId: json['categoryId'] as String?,
      name: json['name'] as String?,
      isOther: json['isOther'] as bool? ?? false,
      symptoms: (json['symptoms'] as List?)
          ?.map((s) => SymptomDto.fromJson(s as Map<String, dynamic>))
          .toList(),
    );
  }
}

/// DTO cho Symptom từ backend
class SymptomDto {
  final String? symptomId;
  final String? name;
  final bool? isOther;

  SymptomDto({
    this.symptomId,
    this.name,
    this.isOther,
  });

  factory SymptomDto.fromJson(Map<String, dynamic> json) {
    return SymptomDto(
      symptomId: json['symptomId'] as String?,
      name: json['name'] as String?,
      isOther: json['isOther'] as bool? ?? false,
    );
  }
}

/// Input triệu chứng khi đặt lịch (gửi lên backend)
class SymptomInput {
  final String categoryId;
  final String? symptomId;
  final String? otherNote;

  SymptomInput({
    required this.categoryId,
    this.symptomId,
    this.otherNote,
  });

  Map<String, dynamic> toJson() => {
        'categoryId': categoryId,
        if (symptomId != null) 'symptomId': symptomId,
        if (otherNote != null && otherNote!.isNotEmpty)
          'otherNote': otherNote,
      };
}
