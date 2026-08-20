import '../../data/dtos/chat_dto.dart';

/// Role trong hoi thoai chatbot.
enum ChatRole {
  user('USER'),
  assistant('ASSISTANT');

  const ChatRole(this.value);
  final String value;

  static ChatRole fromString(String s) {
    final upper = s.toUpperCase();
    for (final e in ChatRole.values) {
      if (e.value == upper) return e;
    }
    return ChatRole.user; // fallback
  }
}

/// Mot tin nhan trong hoi thoai chatbot (FT-39).
///
/// isSafety = true → render safety card thay vi assistant bubble thong thuong.
class ChatMessage {
  const ChatMessage({
    required this.messageId,
    required this.role,
    required this.content,
    required this.createdAt,
    required this.isSafety,
  });

  final String messageId;
  final ChatRole role;
  final String content;
  final DateTime createdAt;
  final bool isSafety;
}

extension ChatMessageDtoX on ChatMessageDto {
  ChatMessage toEntity() {
    return ChatMessage(
      messageId: messageId,
      role: ChatRole.fromString(role),
      content: content,
      createdAt: createdAt,
      isSafety: isSafetyResponse,
    );
  }
}
