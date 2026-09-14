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

      final body = res.data!;
      final envelope = _parseEnvelope(body);
      if (body.containsKey('code') && envelope.code != 200 && envelope.code != 0) {
        throw ApiException(envelope.message);
      }

      dynamic rawList;
      if (body['relatives'] is List) {
        rawList = body['relatives'];
      } else if (body['data'] is Map<String, dynamic> &&
          (body['data'] as Map<String, dynamic>)['relatives'] is List) {
        rawList = (body['data'] as Map<String, dynamic>)['relatives'];
      } else if (body['data'] is List) {
        rawList = body['data'];
      } else if (envelope.data is List) {
        rawList = envelope.data;
      } else if (envelope.data is Map<String, dynamic> &&
          (envelope.data as Map<String, dynamic>)['relatives'] is List) {
        rawList = (envelope.data as Map<String, dynamic>)['relatives'];
      } else {
        throw const ApiException('Dữ liệu không đúng định dạng.');
      }

      final dataList = (rawList as List?) ?? [];
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
      final trimmedPhone = phone.trim();
      final body = AddRelativeRequest(
        fullName: fullName.trim(),
        phone: trimmedPhone.isNotEmpty ? trimmedPhone : null,
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

      final resMap = res.data!;
      Map<String, dynamic>? dataMap;
      if (resMap['relationshipId'] != null) {
        dataMap = resMap;
      } else if (resMap['data'] is Map<String, dynamic>) {
        dataMap = resMap['data'] as Map<String, dynamic>;
      } else {
        final envelope = _parseEnvelope(resMap);
        if (envelope.data is Map<String, dynamic>) {
          dataMap = envelope.data as Map<String, dynamic>;
        }
      }

      if (dataMap == null) {
        throw const ApiException('Thêm người thân thất bại.');
      }

      return PatientRelationshipDTO.fromJson(dataMap).toEntity();
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
      final trimmedPhone = phone?.trim();
      final body = UpdateRelativeRequest(
        fullName: fullName?.trim(),
        phone: (trimmedPhone != null && trimmedPhone.isNotEmpty) ? trimmedPhone : null,
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

      final resMap = res.data!;
      Map<String, dynamic>? dataMap;
      if (resMap['relationshipId'] != null) {
        dataMap = resMap;
      } else if (resMap['data'] is Map<String, dynamic>) {
        dataMap = resMap['data'] as Map<String, dynamic>;
      } else {
        final envelope = _parseEnvelope(resMap);
        if (envelope.data is Map<String, dynamic>) {
          dataMap = envelope.data as Map<String, dynamic>;
        }
      }

      if (dataMap == null) {
        throw const ApiException('Cập nhật người thân thất bại.');
      }

      return PatientRelationshipDTO.fromJson(dataMap).toEntity();
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

      if (res.data != null) {
        final envelope = _parseEnvelope(res.data!);
        if (envelope.code != 200 && envelope.code != 0 && envelope.code != 204) {
          throw ApiException(envelope.message);
        }
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

      final body = res.data!;
      if (body['isRegistered'] is bool) {
        return body['isRegistered'] as bool;
      }

      if (body['data'] is bool) {
        return body['data'] as bool;
      }

      if (body['data'] is Map<String, dynamic> &&
          (body['data'] as Map<String, dynamic>)['isRegistered'] is bool) {
        return (body['data'] as Map<String, dynamic>)['isRegistered'] as bool;
      }

      final envelope = _parseEnvelope(body);
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
