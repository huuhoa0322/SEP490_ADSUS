import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/network/api_exception.dart';
import '../../domain/entities/chat_message.dart';
import '../dtos/chat_dto.dart';

/// Repository cho Module 10 Chat (FT-39).
///
/// Token được tự động gắn bởi dio_client.dart interceptor.
class AiChatRepository {
  const AiChatRepository(this._dio);

  final Dio _dio;

  /// POST /api/v1/me/chat/messages
  ///
  /// Gửi tin nhắn → nhận ASSISTANT response.
  /// Trả về null nếu lỗi.
  Future<ChatMessage?> sendMessage(String content) async {
    try {
      debugPrint('[AiChatRepo] POST /api/v1/me/chat/messages content: $content');

      final res = await _dio.post<Map<String, dynamic>>(
        '/api/v1/me/chat/messages',
        data: SendChatMessageRequest(content: content).toJson(),
      );

      final envelope = res.data;
      debugPrint('[AiChatRepo] Response envelope: $envelope');
      if (envelope == null) return null;

      final data = envelope['data'];
      debugPrint('[AiChatRepo] Response data: $data');
      if (data == null) return null;

      return ChatMessageDto.fromJson(data as Map<String, dynamic>).toEntity();
    } on DioException catch (e) {
      debugPrint('[AiChatRepo] DioException: ${e.response?.statusCode} - ${e.message}');
      throw ApiErrorMapper.general(e,
          fallback: 'Không gửi được tin nhắn. Vui lòng thử lại.');
    }
  }

  /// GET /api/v1/me/chat/messages?from=&to=&limit=
  ///
  /// Lấy lịch sử hội thoại (toàn bộ nếu from=epoch).
  Future<List<ChatMessage>> getHistory({
    required DateTime from,
    required DateTime to,
    int limit = 50,
  }) async {
    try {
      debugPrint('[AiChatRepo] GET /api/v1/me/chat/messages from=$from to=$to limit=$limit');

      final res = await _dio.get<Map<String, dynamic>>(
        '/api/v1/me/chat/messages',
        queryParameters: {
          'from': from.toIso8601String(),
          'to': to.toIso8601String(),
          'limit': limit,
        },
      );

      final envelope = res.data;
      if (envelope == null) return [];

      final dto = ChatHistoryDto.fromJson(envelope);
      return dto.messages.map((d) => d.toEntity()).toList();
    } on DioException catch (e) {
      debugPrint('[AiChatRepo] DioException: ${e.response?.statusCode} - ${e.message}');
      throw ApiErrorMapper.general(e,
          fallback: 'Không tải được lịch sử hội thoại.');
    }
  }
}
