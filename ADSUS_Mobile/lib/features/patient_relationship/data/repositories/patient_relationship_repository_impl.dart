import 'package:dio/dio.dart';

import '../../../../core/constants/api_constants.dart';
import '../../../../core/network/api_exception.dart';
import '../../domain/entities/patient_relationship.dart';
import '../../domain/repositories/patient_relationship_repository.dart';
import '../dtos/patient_relationship_dtos.dart';

/// Implementation của [PatientRelationshipRepository].
///
/// Gọi trực tiếp API qua Dio. Chỉ có chỗ này được import 'package:dio/dio.dart'.
class PatientRelationshipRepositoryImpl implements PatientRelationshipRepository {
  PatientRelationshipRepositoryImpl(this._dio);

  final Dio _dio;

  @override
  Future<List<PatientRelationship>> getRelatives() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiConstants.patientRelationships,
      );

      if (res.data == null) {
        throw const ApiException('Không tải được danh sách người thân.');
      }

      final envelope = _parseEnvelope(res.data!);
      if (envelope.code != 200) {
        throw ApiException(envelope.message);
      }

      if (envelope.data == null) {
        return [];
      }

      List<dynamic> dataList;
      if (envelope.data is List) {
        dataList = envelope.data as List;
      } else {
        throw const ApiException('Dữ liệu không đúng định dạng.');
      }

      return dataList
          .whereType<Map<String, dynamic>>()
          .map((e) => PatientRelationshipDTO.fromJson(e).toEntity())
          .toList();
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e,
          fallback: 'Không tải được danh sách người thân.');
    }
  }

  @override
  Future<PatientRelationship> addRelative({
    required String fullName,
    required String phone,
    DateTime? dateOfBirth,
    String? relationshipName,
  }) async {
    try {
      final body = AddRelativeRequest(
        fullName: fullName,
        phone: phone,
        dateOfBirth: dateOfBirth != null ? _formatDateTime(dateOfBirth) : null,
        relationshipName: relationshipName,
      );

      final res = await _dio.post<Map<String, dynamic>>(
        ApiConstants.patientRelationships,
        data: body.toJson(),
      );

      if (res.data == null) {
        throw const ApiException('Thêm người thân thất bại.');
      }

      final envelope = _parseEnvelope(res.data!);
      if (envelope.data == null) {
        throw const ApiException('Thêm người thân thất bại.');
      }

      return PatientRelationshipDTO.fromJson(
        envelope.data as Map<String, dynamic>,
      ).toEntity();
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Thêm người thân thất bại.');
    }
  }

  @override
  Future<PatientRelationship> updateRelative(
    String id, {
    String? fullName,
    String? phone,
    DateTime? dateOfBirth,
    String? relationshipName,
  }) async {
    try {
      final body = UpdateRelativeRequest(
        fullName: fullName,
        phone: phone,
        dateOfBirth: dateOfBirth != null ? _formatDateTime(dateOfBirth) : null,
        relationshipName: relationshipName,
      );

      final res = await _dio.put<Map<String, dynamic>>(
        '${ApiConstants.patientRelationships}/$id',
        data: body.toJson(),
      );

      if (res.data == null) {
        throw const ApiException('Cập nhật người thân thất bại.');
      }

      final envelope = _parseEnvelope(res.data!);
      if (envelope.data == null) {
        throw const ApiException('Cập nhật người thân thất bại.');
      }

      return PatientRelationshipDTO.fromJson(
        envelope.data as Map<String, dynamic>,
      ).toEntity();
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Cập nhật người thân thất bại.');
    }
  }

  @override
  Future<void> deleteRelative(String id) async {
    try {
      final res = await _dio.delete<Map<String, dynamic>>(
        '${ApiConstants.patientRelationships}/$id',
      );

      if (res.data == null) {
        throw const ApiException('Xóa người thân thất bại.');
      }

      final envelope = _parseEnvelope(res.data!);
      if (envelope.code != 200) {
        throw ApiException(envelope.message);
      }
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e, fallback: 'Xóa người thân thất bại.');
    }
  }

  @override
  Future<bool> checkPhoneRegistered(String phone) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiConstants.checkPhoneRegistered,
        queryParameters: {'phone': phone},
      );

      if (res.data == null) {
        return false;
      }

      final envelope = _parseEnvelope(res.data!);
      // Backend trả {code, message, data: true/false}
      return envelope.data == true;
    } on DioException catch (e) {
      // 404 = chưa có tài khoản → trả về false
      if (e.response?.statusCode == 404) {
        return false;
      }
      throw ApiErrorMapper.general(e,
          fallback: 'Không kiểm tra được số điện thoại.');
    }
  }

  _ApiEnvelope _parseEnvelope(Map<String, dynamic> json) {
    return _ApiEnvelope(
      code: json['code'] as int? ?? 0,
      message: json['message'] as String? ?? '',
      data: json['data'],
    );
  }

  String _formatDateTime(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-'
      '${d.month.toString().padLeft(2, '0')}-'
      '${d.day.toString().padLeft(2, '0')}';
}

class _ApiEnvelope {
  const _ApiEnvelope({
    required this.code,
    required this.message,
    this.data,
  });

  final int code;
  final String message;
  final dynamic data;
}
