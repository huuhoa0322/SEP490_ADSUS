/// DTOs cho Module 10 Chat (FT-39).
///
/// API endpoints:
///   POST /api/v1/me/chat/messages
///   GET  /api/v1/me/chat/messages?from=&to=&limit=
library;

class SendChatMessageRequest {
  const SendChatMessageRequest({required this.content});
  final String content;

  Map<String, dynamic> toJson() => {'content': content};
}

class ChatMessageDto {
  const ChatMessageDto({
    required this.messageId,
    required this.role,
    required this.content,
    required this.createdAt,
    required this.isSafetyResponse,
    this.detectedIntent,
  });

  final String messageId;
  final String role; // "User" | "Assistant"
  final String content;
  final DateTime createdAt;
  final bool isSafetyResponse;
  final String? detectedIntent; // ChatIntent string from BE

  factory ChatMessageDto.fromJson(Map<String, dynamic> json) {
    return ChatMessageDto(
      messageId: json['messageId'] as String,
      role: json['role'] as String,
      content: json['content'] as String,
      createdAt: DateTime.parse(json['createdAt'] as String),
      isSafetyResponse: json['isSafetyResponse'] as bool? ?? false,
      detectedIntent: json['detectedIntent'] as String?,
    );
  }
}

class ChatHistoryDto {
  const ChatHistoryDto({required this.messages});
  final List<ChatMessageDto> messages;

  factory ChatHistoryDto.fromJson(Map<String, dynamic> json) {
    final raw = json['messages'] as List<dynamic>? ?? [];
    return ChatHistoryDto(
      messages: raw
          .map((e) => ChatMessageDto.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}
