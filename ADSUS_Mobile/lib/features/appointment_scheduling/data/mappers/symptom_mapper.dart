import '../dtos/symptom_dtos.dart';
import '../../domain/entities/symptom.dart';

/// Mapper chuyển đổi Symptom DTOs ↔ Entities
class SymptomMapper {
  /// Chuyển SymptomDto → Symptom Entity
  static Symptom symptomFromDto(SymptomDto dto) {
    return Symptom(
      id: dto.symptomId ?? '',
      name: dto.name ?? '',
      isOther: dto.isOther ?? false,
    );
  }

  /// Chuyển SymptomCategoryDto → SymptomCategory Entity
  static SymptomCategory categoryFromDto(SymptomCategoryDto dto) {
    return SymptomCategory(
      id: dto.categoryId ?? '',
      name: dto.name ?? '',
      isOther: dto.isOther ?? false,
      symptoms: dto.symptoms
              ?.map((s) => symptomFromDto(s))
              .toList() ??
          [],
    );
  }

  /// Chuyển list SymptomCategoryDto → list SymptomCategory Entity
  static List<SymptomCategory> categoryListFromDto(
      List<SymptomCategoryDto> dtos) {
    return dtos.map((dto) => categoryFromDto(dto)).toList();
  }
}
