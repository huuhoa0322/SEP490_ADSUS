import 'package:dio/dio.dart';

import '../../../../core/constants/api_constants.dart';
import '../../../../core/network/api_exception.dart';
import '../dtos/chat_dto.dart';

/// Repository gọi API chat — dùng chung cho ChatRepository offline-first.
///
/// Endpoint: /api/v1/me/chat/messages
class ChatApiRepository {
  ChatApiRepository({required this._dio});

  final Dio _dio;

  /// POST /api/v1/me/chat/messages — gửi tin nhắn, nhận phản hồi assistant.
  Future<ChatMessageDto> sendMessage(SendChatMessageRequest request) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        ApiConstants.chatMessages,
        data: request.toJson(),
        options: Options(receiveTimeout: ApiConstants.chatTimeout),
      );

      final data = res.data?['data'] as Map<String, dynamic>?;
      if (data == null) {
        throw ApiException('Không nhận được phản hồi từ AI.');
      }
      return ChatMessageDto.fromJson(data);
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e,
          fallback: 'Không gửi được tin nhắn. Vui lòng thử lại.');
    }
  }

  /// GET /api/v1/me/chat/messages — lấy lịch sử từ server.
  Future<ChatHistoryDto> getHistory(ChatHistoryRequest request) async {
    try {
      final queryParams = <String, dynamic>{};
      if (request.from != null) queryParams['from'] = request.from!.toUtc().toIso8601String();
      if (request.to != null) queryParams['to'] = request.to!.toUtc().toIso8601String();
      if (request.limit != 100) queryParams['limit'] = request.limit;
      if (request.beforeId != null) queryParams['beforeId'] = request.beforeId;

      final res = await _dio.get<Map<String, dynamic>>(
        ApiConstants.chatMessages,
        queryParameters: queryParams,
      );

      final envelope = res.data;
      if (envelope == null) return const ChatHistoryDto(messages: []);
      return ChatHistoryDto.fromJson(envelope);
    } on DioException catch (e) {
      throw ApiErrorMapper.general(e,
          fallback: 'Không tải được lịch sử hội thoại.');
    }
  }
}

/// Request lấy lịch sử chat.
class ChatHistoryRequest {
  const ChatHistoryRequest({
    this.from,
    this.to,
    this.limit = 100,
    this.beforeId,
  });
  final DateTime? from;
  final DateTime? to;
  final int limit;
  final String? beforeId;
}
