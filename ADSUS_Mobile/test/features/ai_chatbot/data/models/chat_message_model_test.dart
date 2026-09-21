import 'package:flutter_test/flutter_test.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/dtos/chat_dto.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/models/chat_message_model.dart';
import 'package:adsus_mobile/features/ai_chatbot/domain/entities/chat_message.dart';

void main() {
  group('ChatMessage Entity & Enums', () {
    test('ChatRole.fromString parses valid strings and falls back to user', () {
      expect(ChatRole.fromString('USER'), ChatRole.user);
      expect(ChatRole.fromString('user'), ChatRole.user);
      expect(ChatRole.fromString('ASSISTANT'), ChatRole.assistant);
      expect(ChatRole.fromString('assistant'), ChatRole.assistant);
      expect(ChatRole.fromString('UNKNOWN_ROLE'), ChatRole.user);
    });

    test('ChatIntent.fromString parses all supported intents and handles null/unknown', () {
      expect(ChatIntent.fromString('greeting'), ChatIntent.greeting);
      expect(ChatIntent.fromString('prescription'), ChatIntent.prescription);
      expect(ChatIntent.fromString('appointment'), ChatIntent.appointment);
      expect(ChatIntent.fromString('casehistory'), ChatIntent.caseHistory);
      expect(ChatIntent.fromString('allergy'), ChatIntent.allergy);
      expect(ChatIntent.fromString('disease'), ChatIntent.disease);
      expect(ChatIntent.fromString('healthlog'), ChatIntent.healthLog);
      expect(ChatIntent.fromString('blog'), ChatIntent.blog);
      expect(ChatIntent.fromString('general'), ChatIntent.general);
      expect(ChatIntent.fromString('invalid_intent'), ChatIntent.unknown);
      expect(ChatIntent.fromString(null), ChatIntent.unknown);
    });
  });

  group('sanitizeAssistantContent', () {
    test('removes AI greeting prefix if present', () {
      const input = 'Chào bạn, tôi là trợ lý sức khỏe của ADSUS. Bạn cần tư vấn dinh dưỡng?';
      final sanitized = sanitizeAssistantContent(input);
      expect(sanitized, 'Bạn cần tư vấn dinh dưỡng?');
    });

    test('removes trailing and leading disclaimers', () {
      const input = '''
** Thông tin trên do AI sinh ra — chỉ mang tính tham khảo... **
Bà bầu nên bổ sung acid folic 400mcg mỗi ngày.
Luôn hỏi bác sĩ phụ trách trước khi dùng thuốc.
''';
      final sanitized = sanitizeAssistantContent(input);
      expect(sanitized, contains('Bà bầu nên bổ sung acid folic 400mcg mỗi ngày.'));
      expect(sanitized, isNot(contains('Thông tin trên do AI')));
      expect(sanitized, isNot(contains('Luôn hỏi bác sĩ phụ trách')));
    });

    test('collapses excessive empty newlines', () {
      const input = 'Đoạn 1\n\n\n\n\nĐoạn 2';
      final sanitized = sanitizeAssistantContent(input);
      expect(sanitized, 'Đoạn 1\n\nĐoạn 2');
    });
  });

  group('Chat DTOs & Models', () {
    test('ChatMessageDto.fromJson parses JSON payload correctly', () {
      final json = {
        'messageId': 'msg-123',
        'role': 'ASSISTANT',
        'content': 'Xin chào mẹ bầu',
        'createdAt': '2026-09-18T10:00:00.000Z',
        'isSafetyResponse': true,
        'detectedIntent': 'greeting',
        'isRateLimitExceeded': false,
      };

      final dto = ChatMessageDto.fromJson(json);
      expect(dto.messageId, 'msg-123');
      expect(dto.role, 'ASSISTANT');
      expect(dto.content, 'Xin chào mẹ bầu');
      expect(dto.isSafetyResponse, isTrue);
      expect(dto.detectedIntent, 'greeting');
      expect(dto.isRateLimitExceeded, isFalse);

      final entity = dto.toEntity();
      expect(entity.messageId, 'msg-123');
      expect(entity.role, ChatRole.assistant);
      expect(entity.isSafety, isTrue);
      expect(entity.detectedIntent, ChatIntent.greeting);
    });

    test('ChatHistoryDto.fromJson handles list of messages', () {
      final json = {
        'messages': [
          {
            'messageId': 'msg-1',
            'role': 'USER',
            'content': 'Tôi muốn hỏi lịch khám',
            'createdAt': '2026-09-18T09:00:00.000Z',
            'isSafetyResponse': false,
          },
          {
            'messageId': 'msg-2',
            'role': 'ASSISTANT',
            'content': 'Lịch khám của bạn vào ngày mai',
            'createdAt': '2026-09-18T09:01:00.000Z',
            'isSafetyResponse': false,
          }
        ]
      };

      final history = ChatHistoryDto.fromJson(json);
      expect(history.messages.length, 2);
      expect(history.messages.first.content, 'Tôi muốn hỏi lịch khám');
      expect(history.messages.last.content, 'Lịch khám của bạn vào ngày mai');
    });

    test('SendChatMessageRequest.toJson formats payload', () {
      const req = SendChatMessageRequest(content: 'Test query');
      expect(req.toJson(), {'content': 'Test query'});
    });

    test('ChatMessageModel copyWith preserves and overrides fields', () {
      final model = ChatMessageModel(
        id: 'id-1',
        content: 'hello',
        role: HiveChatRole.user,
        createdAt: DateTime(2026, 9, 18),
        isSafetyResponse: false,
        isSynced: false,
      );

      final updated = model.copyWith(isSynced: true, content: 'updated hello');
      expect(updated.id, 'id-1');
      expect(updated.content, 'updated hello');
      expect(updated.isSynced, isTrue);
      expect(updated.role, HiveChatRole.user);
    });
  });
}
