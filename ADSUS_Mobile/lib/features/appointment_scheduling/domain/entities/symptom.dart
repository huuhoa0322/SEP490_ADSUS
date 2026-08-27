/// Triệu chứng chi tiết
class Symptom {
  final String id;
  final String name;
  final bool isOther;

  const Symptom({
    required this.id,
    required this.name,
    this.isOther = false,
  });
}

/// Nhóm triệu chứng (category)
class SymptomCategory {
  final String id;
  final String name;
  final bool isOther;
  final List<Symptom> symptoms;

  const SymptomCategory({
    required this.id,
    required this.name,
    this.isOther = false,
    this.symptoms = const [],
  });

  /// Filter bỏ category đã chọn ở block khác
  bool get canSelectOtherSymptoms =>
      !isOther || symptoms.any((s) => !s.isOther);
}
