import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../data/repositories/ai_chat_repository.dart';
import '../../domain/entities/chat_message.dart' as entity;
import '../../domain/entities/chat_message.dart' show sanitizeAssistantContent;

/// Trạng thái màn hình chatbot.
class AiChatState {
  const AiChatState({
    this.messages = const [],
    this.isSending = false,
    this.errorMessage,
    this.lastDetectedIntent,
  });

  final List<entity.ChatMessage> messages;
  final bool isSending;
  final String? errorMessage;

  /// Intent của assistant message vừa nhất (dùng cho suggestion chips).
  final entity.ChatIntent? lastDetectedIntent;

  AiChatState copyWith({
    List<entity.ChatMessage>? messages,
    bool? isSending,
    String? errorMessage,
    entity.ChatIntent? lastDetectedIntent,
    bool clearError = false,
    bool clearLastIntent = false,
  }) =>
      AiChatState(
        messages: messages ?? this.messages,
        isSending: isSending ?? this.isSending,
        errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
        lastDetectedIntent:
            clearLastIntent ? null : (lastDetectedIntent ?? this.lastDetectedIntent),
      );
}

/// ViewModel cho màn hình Chatbot (FT-39).
///
/// Quản lý:
/// - Danh sách tin nhắn (USER + ASSISTANT)
/// - Trạng thái gửi (disable nút khi đang chờ)
/// - Lỗi hiển thị
class AiChatViewModel extends StateNotifier<AiChatState> {
  AiChatViewModel(this._ref) : super(const AiChatState());

  final Ref _ref;

  AiChatRepository get _repo => _ref.read(aiChatRepositoryProvider);

  /// Tải lịch sử hội thoại (toàn bộ tính từ epoch).
  Future<void> loadHistory() async {
    try {
      final messages = await _repo.getHistory(
        from: DateTime.utc(2024, 1, 1),
        to: DateTime.now().toUtc(),
        limit: 200,
      );
      // Messages đã sorted DESC từ API → đảo lại để hiển thị đúng thứ tự (cũ → mới)
      state = state.copyWith(messages: messages.reversed.toList());
    } on ApiException catch (e) {
      state = state.copyWith(errorMessage: e.message);
    }
  }

  /// Gửi tin nhắn → nhận phản hồi.
  Future<void> sendMessage(String content) async {
    if (content.trim().isEmpty) return;

    state = state.copyWith(isSending: true, clearError: true);

    try {
      // Thêm USER message vào list ngay (optimistic)
      final userMsg = entity.ChatMessage(
        messageId: 'local-${DateTime.now().millisecondsSinceEpoch}',
        role: entity.ChatRole.user,
        content: content.trim(),
        createdAt: DateTime.now(),
        isSafety: false,
      );
      state = state.copyWith(messages: [...state.messages, userMsg]);

      // Gọi API
      final response = await _repo.sendMessage(content.trim());
      if (response != null) {
        // Thêm ASSISTANT response. Response đã được sanitize từ DTO.toEntity(),
        // nhưng sendMessage trả về object thô → sanitize lại để khử disclaimer
        // mà LLM (Gemini) tự ghép vào.
        final assistantMsg = entity.ChatMessage(
          messageId: response.messageId,
          role: entity.ChatRole.assistant,
          content: sanitizeAssistantContent(response.content),
          createdAt: response.createdAt,
          isSafety: response.isSafety,
          detectedIntent: response.detectedIntent,
        );
        state = state.copyWith(
          messages: [...state.messages, assistantMsg],
          isSending: false,
          lastDetectedIntent: assistantMsg.detectedIntent,
        );
      } else {
        state = state.copyWith(
          isSending: false,
          errorMessage: 'Không nhận được phản hồi từ AI. Vui lòng thử lại.',
        );
      }
    } on ApiException catch (e) {
      state = state.copyWith(
        isSending: false,
        errorMessage: e.message,
      );
    }
  }

  void clearError() => state = state.copyWith(clearError: true);
}

/// Provider cho AiChatViewModel.
final aiChatViewModelProvider =
    StateNotifierProvider<AiChatViewModel, AiChatState>((ref) {
  return AiChatViewModel(ref);
});
