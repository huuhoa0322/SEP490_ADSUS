import '../entities/symptom.dart';

/// Repository interface cho Symptoms
abstract interface class SymptomRepository {
  /// Lấy danh sách categories và symptoms
  Future<List<SymptomCategory>> getCategories();
}
