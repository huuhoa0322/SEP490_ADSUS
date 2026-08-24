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
      content: sanitizeAssistantContent(content),
      createdAt: createdAt,
      isSafety: isSafetyResponse,
    );
  }
}

/// Strip các đoạn mà Gemini (LLM) tự thêm vào response:
/// - Lời chào "Chào bạn, tôi là trợ lý sức khỏe của ADSUS." (đã có greeting ở empty state)
/// - Disclaimer "Thông tin trên do AI sinh ra…" / "Thông tin do AI sinh ra…" (đã có sticky banner + badge)
///
/// Disclaimer đã hiển thị ở Flutter UI (banner cố định + badge đầu bubble), nên BE
/// không ghép nữa; tuy nhiên LLM vẫn tự trả về các câu đó → strip ở client để khử lặp.
String sanitizeAssistantContent(String raw) {
  var text = raw;

  // Bỏ các câu disclaimer ở ĐẦU hoặc CUỐI response (LLM hay ghép thêm).
  // Dùng regex với multiline + dotall để bắt cả khi nó xuống dòng.
  final patterns = <RegExp>[
    // "** Thông tin trên do AI sinh ra — chỉ mang tính tham khảo... **"
    RegExp(
      r'\*{0,2}\s*\*?\*?\s*Thông tin (?:trên )?do AI sinh ra[^.*]*?(?:\.{2,}|\*\*|\.\s*\*|\n|$)',
      caseSensitive: false,
      multiLine: true,
    ),
    // "Chào bạn, tôi là trợ lý sức khỏe của ADSUS."
    RegExp(
      r'^\s*Chào bạn,?\s*tôi là trợ lý sức khỏe của ADSUS\.?\s*',
      caseSensitive: false,
      multiLine: true,
    ),
    // "**Lưu ý: Thông tin do AI ... chỉ mang tính tham khảo.**"
    RegExp(
      r'\*{0,2}\s*Lưu ý:?\s*Thông tin (?:trên )?do AI[^.*]*?(?:\.{2,}|\*\*|\.\s*\*|\n|$)',
      caseSensitive: false,
      multiLine: true,
    ),
    // "Luôn hỏi bác sĩ phụ trách trước khi áp dụng..." (LLM hay ghép cuối)
    RegExp(
      r'\n\s*Luôn hỏi bác sĩ phụ trách[^.\n]*\.?\s*$',
      caseSensitive: false,
      multiLine: true,
    ),
  ];

  for (final p in patterns) {
    text = text.replaceAll(p, '');
  }

  // Collapse nhiều blank line liên tiếp thành 1
  text = text.replaceAll(RegExp(r'\n{3,}'), '\n\n').trim();
  return text;
}
