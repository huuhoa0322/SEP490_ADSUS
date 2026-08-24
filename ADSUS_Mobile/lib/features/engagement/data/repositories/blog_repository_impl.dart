import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/network/api_exception.dart';
import '../../domain/entities/blog_post.dart';
import '../../domain/repositories/blog_repository.dart';
import '../dtos/blog_post_dto.dart';

/// Implementation [BlogRepository] gọi thẳng backend.
///
/// Token tự động được đính kèm bởi [dio_client] interceptor (Patient JWT).
/// Backend đã filter `Status == Published` ở service layer (GB-05), repository
/// không cần check lại.
class BlogRepositoryImpl implements BlogRepository {
  const BlogRepositoryImpl(this._dio);

  final Dio _dio;

  @override
  Future<List<BlogPost>> listPublished({int page = 1, int pageSize = 20}) async {
    try {
      debugPrint('[BlogRepo] GET /api/v1/blog-posts page=$page pageSize=$pageSize');

      final res = await _dio.get<Map<String, dynamic>>(
        '/api/v1/blog-posts',
        queryParameters: {
          'page': page,
          'pageSize': pageSize,
        },
      );

      final envelope = res.data;
      if (envelope == null) return const [];

      final paged = envelope['data'];
      if (paged is! Map<String, dynamic>) return const [];

      final items = paged['items'];
      if (items is! List) return const [];

      return items
          .whereType<Map<String, dynamic>>()
          .map(BlogPostListItemDto.fromJson)
          .map((d) => d.toEntity())
          .toList();
    } on DioException catch (e) {
      debugPrint('[BlogRepo] listPublished DioException: ${e.response?.statusCode}');
      throw ApiErrorMapper.general(e,
          fallback: 'Không tải được danh sách bài viết.');
    }
  }

  @override
  Future<BlogPostDetail> getById(String id) async {
    try {
      debugPrint('[BlogRepo] GET /api/v1/blog-posts/$id');

      final res = await _dio.get<Map<String, dynamic>>('/api/v1/blog-posts/$id');

      final envelope = res.data;
      if (envelope == null) {
        throw const ApiException('Bài viết không tồn tại hoặc chưa được xuất bản.',
            statusCode: 404);
      }

      final data = envelope['data'];
      if (data is! Map<String, dynamic>) {
        throw const ApiException('Bài viết không tồn tại hoặc chưa được xuất bản.',
            statusCode: 404);
      }

      return BlogPostDetailDto.fromJson(data).toEntity();
    } on DioException catch (e) {
      // GB-05: backend trả 404 nếu post là Draft hoặc không tồn tại. Map sang cùng message
      // để bệnh nhân không phân biệt được hai trường hợp (không leak status).
      if (e.response?.statusCode == 404) {
        throw const ApiException(
            'Bài viết không tồn tại hoặc chưa được xuất bản.',
            statusCode: 404);
      }
      debugPrint('[BlogRepo] getById DioException: ${e.response?.statusCode}');
      throw ApiErrorMapper.general(e,
          fallback: 'Không tải được bài viết.');
    }
  }
}