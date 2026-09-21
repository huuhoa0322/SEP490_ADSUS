import 'package:flutter_test/flutter_test.dart';
import 'package:adsus_mobile/features/ai_chatbot/presentation/viewmodels/ai_chat_view_model.dart';
import 'package:adsus_mobile/features/ai_chatbot/domain/entities/chat_message.dart';

void main() {
  group('AiChatState Unit Tests', () {
    test('default state has correct initial values', () {
      const state = AiChatState();
      expect(state.messages, isEmpty);
      expect(state.isLoading, isFalse);
      expect(state.isSending, isFalse);
      expect(state.isMerging, isFalse);
      expect(state.errorMessage, isNull);
      expect(state.isOffline, isFalse);
      expect(state.lastDetectedIntent, isNull);
      expect(state.hasLoadedHistory, isFalse);
    });

    test('copyWith updates fields correctly', () {
      const state = AiChatState();
      final msg = ChatMessage(
        messageId: 'm1',
        role: ChatRole.user,
        content: 'Bụng đau',
        createdAt: DateTime(2026, 9, 18),
        isSafety: false,
      );

      final updated = state.copyWith(
        messages: [msg],
        isLoading: true,
        isSending: true,
        isOffline: true,
        errorMessage: 'Network error',
        lastDetectedIntent: ChatIntent.healthLog,
        hasLoadedHistory: true,
      );

      expect(updated.messages.length, 1);
      expect(updated.messages.first.content, 'Bụng đau');
      expect(updated.isLoading, isTrue);
      expect(updated.isSending, isTrue);
      expect(updated.isOffline, isTrue);
      expect(updated.errorMessage, 'Network error');
      expect(updated.lastDetectedIntent, ChatIntent.healthLog);
      expect(updated.hasLoadedHistory, isTrue);
    });

    test('copyWith with clearError removes errorMessage', () {
      const state = AiChatState(errorMessage: 'Có lỗi xảy ra');
      expect(state.errorMessage, 'Có lỗi xảy ra');

      final cleared = state.copyWith(clearError: true);
      expect(cleared.errorMessage, isNull);
    });

    test('copyWith with clearLastIntent resets detected intent', () {
      const state = AiChatState(lastDetectedIntent: ChatIntent.prescription);
      expect(state.lastDetectedIntent, ChatIntent.prescription);

      final cleared = state.copyWith(clearLastIntent: true);
      expect(cleared.lastDetectedIntent, isNull);
    });

    test('sorting invariant: messages can be sorted chronologically by createdAt', () {
      final m1 = ChatMessage(
        messageId: '1',
        role: ChatRole.user,
        content: 'Trước',
        createdAt: DateTime(2026, 9, 18, 10, 0),
        isSafety: false,
      );
      final m2 = ChatMessage(
        messageId: '2',
        role: ChatRole.assistant,
        content: 'Sau',
        createdAt: DateTime(2026, 9, 18, 10, 1),
        isSafety: false,
      );

      final list = [m2, m1];
      list.sort((a, b) => a.createdAt.compareTo(b.createdAt));

      expect(list.first.messageId, '1');
      expect(list.last.messageId, '2');
    });
  });
}
