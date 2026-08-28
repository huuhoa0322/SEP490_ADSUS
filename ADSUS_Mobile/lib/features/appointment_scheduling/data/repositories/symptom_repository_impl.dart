import 'package:dio/dio.dart';

import '../../../../core/constants/api_constants.dart';
import '../../domain/entities/symptom.dart';
import '../../domain/repositories/symptom_repository.dart';
import '../dtos/symptom_dtos.dart';
import '../mappers/symptom_mapper.dart';

/// Implementation của SymptomRepository - gọi GET /api/v1/symptoms/categories
class SymptomRepositoryImpl implements SymptomRepository {
  final Dio _dio;

  SymptomRepositoryImpl(this._dio);

  @override
  Future<List<SymptomCategory>> getCategories() async {
    try {
      final response = await _dio.get(
        ApiConstants.symptomCategories,
      );

      final data = response.data;
      if (data is Map<String, dynamic> && data['code'] == 200) {
        final List<dynamic> categoriesList = data['data'] ?? [];
        final dtos = categoriesList
            .map((json) =>
                SymptomCategoryDto.fromJson(json as Map<String, dynamic>))
            .toList();
        return SymptomMapper.categoryListFromDto(dtos);
      }
      return [];
    } on DioException catch (e) {
      // ignore: avoid_print
      print('[SymptomRepo] Error fetching categories: $e');
      return [];
    }
  }
}
