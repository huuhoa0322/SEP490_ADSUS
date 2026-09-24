import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/dtos/chat_stream_event.dart';
import '../../data/models/chat_message_model.dart';
import '../../data/repositories/chat_local_repository.dart';
import '../../data/repositories/chat_api_repository.dart';
import '../../data/repositories/chat_repository.dart';
import '../../domain/entities/chat_message.dart' as entity;
import '../../domain/entities/chat_message.dart' show sanitizeAssistantContent;
import '../../../../shared/providers/app_providers.dart';

/// Trạng thái màn hình chatbot.
class AiChatState {
  const AiChatState({
    this.messages = const [],
    this.isLoading = false,
    this.isSending = false,
    this.isThinking = false,
    this.isMerging = false,
    this.errorMessage,
    this.isOffline = false,
    this.lastDetectedIntent,
    this.hasLoadedHistory = false,
  });

  final List<entity.ChatMessage> messages;
  final bool isLoading;
  final bool isSending;
  final bool isThinking;
  final bool isMerging;
  final String? errorMessage;
  final bool isOffline;
  final entity.ChatIntent? lastDetectedIntent;
  final bool hasLoadedHistory;

  AiChatState copyWith({
    List<entity.ChatMessage>? messages,
    bool? isLoading,
    bool? isSending,
    bool? isThinking,
    bool? isMerging,
    String? errorMessage,
    bool? isOffline,
    entity.ChatIntent? lastDetectedIntent,
    bool? hasLoadedHistory,
    bool clearError = false,
    bool clearLastIntent = false,
  }) =>
      AiChatState(
        messages: messages ?? this.messages,
        isLoading: isLoading ?? this.isLoading,
        isSending: isSending ?? this.isSending,
        isThinking: isThinking ?? this.isThinking,
        isMerging: isMerging ?? this.isMerging,
        errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
        isOffline: isOffline ?? this.isOffline,
        lastDetectedIntent:
            clearLastIntent ? null : (lastDetectedIntent ?? this.lastDetectedIntent),
        hasLoadedHistory: hasLoadedHistory ?? this.hasLoadedHistory,
      );
}

class AiChatViewModel extends StateNotifier<AiChatState> {
  AiChatViewModel(this._ref) : super(const AiChatState()) {
    _connectivitySubscription = _listenConnectivity();
  }

  final Ref _ref;
  ChatRepository? _chatRepo;
  StreamSubscription<List<ConnectivityResult>>? _connectivitySubscription;
  CancelToken? _currentCancelToken;

  /// Khởi tạo repository sau khi user đăng nhập.
  ///
  /// PHẢI await initializeForUser() trước khi coi _chatRepo là sẵn sàng dùng — bản cũ gọi
  /// mà không await, nên _chatRepo đã khác null (qua được guard "if (_chatRepo == null)
  /// return;" ở mọi hàm khác) trong khi Hive box bên trong VẪN CHƯA MỞ XONG. Nếu đúng lúc đó
  /// có sự kiện đổi kết nối mạng tới (rất dễ xảy ra, xem _listenConnectivity — sự kiện đầu
  /// tiên có thể tới ngay khi khởi động), syncPendingMessages() sẽ đọc thẳng vào box chưa mở,
  /// ném StateError thật (bắt được qua integration_test BF-03, 22/09/2026).
  Future<void> initialize(String userId) async {
    final local = _ref.read(chatLocalRepositoryProvider);
    final api = _ref.read(chatApiRepositoryProvider);

    final repo = ChatRepository(local: local, api: api);
    repo.onNewMessage = _onNewMessage;
    await repo.initializeForUser(userId);
    _chatRepo = repo;

    await loadHistory();
  }

  /// Dọn dẹp khi đăng xuất.
  Future<void> disposeForUser() async {
    _currentCancelToken?.cancel();
    _currentCancelToken = null;
    await _chatRepo?.dispose();
    _chatRepo = null;
    state = const AiChatState();
  }

  /// Load lịch sử: local trước → merge server.
  Future<void> loadHistory() async {
    if (_chatRepo == null) return;

    state = state.copyWith(isLoading: true, clearError: true);

    try {
      final merged = await _chatRepo!.loadHistory(
        onLocalLoaded: (localMessages) {
          state = state.copyWith(
            messages: localMessages.map(_toEntity).toList(),
            isLoading: false,
            isMerging: true,
            hasLoadedHistory: false,
          );
        },
        onMergeComplete: (mergedMessages) {
          state = state.copyWith(
            messages: mergedMessages.map(_toEntity).toList(),
            isMerging: false,
            hasLoadedHistory: true,
          );
        },
      );

      state = state.copyWith(
        messages: merged.map(_toEntity).toList(),
        isLoading: false,
        isMerging: false,
        hasLoadedHistory: true,
      );
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        isMerging: false,
        hasLoadedHistory: true,
        errorMessage: 'Không tải được lịch sử chat.',
      );
    }
  }

  /// Gửi tin nhắn mới qua SSE streaming.
  ///
  ///  - isSending giữ true trong suốt thời gian streaming để vô hiệu hóa nút gửi, chống race condition.
  ///  - Sự kiện thinking: cập nhật isThinking = true để hiển thị typing/thinking indicator.
  ///  - Sự kiện delta: tạo hoặc nối text chunk vào tin nhắn assistant trên bộ nhớ (real-time).
  ///  - Sự kiện done: chuẩn hoá ID, detected intent, sanitize nội dung, gỡ bỏ isSending/isThinking.
  ///  - Sự kiện error hoặc ngoại lệ mạng: bắt lỗi an toàn, gỡ bỏ isSending/isThinking, báo lỗi thân thiện.
  Future<void> sendMessage(String content) async {
    if (_chatRepo == null || content.trim().isEmpty) return;
    if (state.isSending) return; // Khoá gửi song song

    final trimmed = content.trim();
    _currentCancelToken?.cancel();
    final cancelToken = CancelToken();
    _currentCancelToken = cancelToken;

    state = state.copyWith(
      isSending: true,
      isThinking: true,
      clearError: true,
    );

    String? currentAssistantId;
    final assistantTextBuffer = StringBuffer();
    bool receivedDone = false;

    try {
      final stream = _chatRepo!.streamMessage(trimmed, cancelToken: cancelToken);

      await for (final event in stream) {
        if (!mounted) break;

        switch (event) {
          case ChatStreamThinkingEvent():
            state = state.copyWith(isThinking: true);
            break;

          case ChatStreamDeltaEvent(:final chunk):
            if (chunk.isEmpty) break;
            assistantTextBuffer.write(chunk);

            if (currentAssistantId == null) {
              currentAssistantId = 'stream_${DateTime.now().millisecondsSinceEpoch}';
              final assistantMsg = entity.ChatMessage(
                messageId: currentAssistantId,
                role: entity.ChatRole.assistant,
                content: assistantTextBuffer.toString(),
                createdAt: DateTime.now(),
                isSafety: false,
              );
              state = state.copyWith(
                isThinking: false,
                messages: [...state.messages, assistantMsg],
              );
            } else {
              final updated = List<entity.ChatMessage>.from(state.messages);
              final idx = updated.indexWhere((m) => m.messageId == currentAssistantId);
              if (idx != -1) {
                updated[idx] = updated[idx].copyWith(
                  content: assistantTextBuffer.toString(),
                );
                state = state.copyWith(
                  isThinking: false,
                  messages: updated,
                );
              }
            }
            break;

          case ChatStreamDoneEvent(
            :final messageId,
            :final content,
            :final createdAt,
            :final detectedIntent,
            :final isSafetyResponse,
            :final isRateLimitExceeded,
          ):
            receivedDone = true;
            final finalRaw = content.isNotEmpty ? content : assistantTextBuffer.toString();
            final sanitized = sanitizeAssistantContent(finalRaw);
            final intent = entity.ChatIntent.fromString(detectedIntent);

            final updated = List<entity.ChatMessage>.from(state.messages);
            final idx = currentAssistantId != null
                ? updated.indexWhere((m) => m.messageId == currentAssistantId)
                : -1;

            final finalMsg = entity.ChatMessage(
              messageId: messageId.isNotEmpty
                  ? messageId
                  : (currentAssistantId ?? 'done_${DateTime.now().millisecondsSinceEpoch}'),
              role: entity.ChatRole.assistant,
              content: sanitized,
              createdAt: createdAt,
              isSafety: isSafetyResponse,
              detectedIntent: intent,
              isRateLimitExceeded: isRateLimitExceeded,
            );

            if (idx != -1) {
              updated[idx] = finalMsg;
            } else {
              updated.add(finalMsg);
            }

            state = state.copyWith(
              isSending: false,
              isThinking: false,
              messages: updated,
              lastDetectedIntent: intent,
            );
            break;

          case ChatStreamErrorEvent(:final message):
            state = state.copyWith(
              isSending: false,
              isThinking: false,
              errorMessage: message.isNotEmpty
                  ? message
                  : 'Gửi tin nhắn thất bại. Vui lòng thử lại.',
            );
            break;
        }
      }

      if (!receivedDone && state.isSending && mounted) {
        state = state.copyWith(
          isSending: false,
          isThinking: false,
          errorMessage: assistantTextBuffer.isNotEmpty
              ? null
              : 'Mất kết nối với máy chủ. Vui lòng thử lại.',
        );
      }
    } catch (e) {
      if (mounted) {
        state = state.copyWith(
          isSending: false,
          isThinking: false,
          errorMessage: 'Gửi tin nhắn thất bại. Vui lòng thử lại.',
        );
      }
    } finally {
      if (_currentCancelToken == cancelToken) {
        _currentCancelToken = null;
      }
    }
  }

  /// Thử sync lại message offline khi có mạng.
  Future<void> syncPendingMessages() async {
    if (_chatRepo == null) return;
    await _chatRepo!.syncPendingMessages();
  }

  /// Xoá toàn bộ lịch sử chat.
  Future<void> clearHistory() async {
    await _chatRepo?.clearHistory();
    state = state.copyWith(messages: []);
  }

  void clearError() => state = state.copyWith(clearError: true);

  void _onNewMessage(ChatMessageModel model) {
    if (!mounted) return;

    final entityMsg = _toEntity(model);
    final updated = [...state.messages, entityMsg];
    updated.sort((a, b) => a.createdAt.compareTo(b.createdAt));

    state = state.copyWith(
      messages: updated,
      lastDetectedIntent:
          entityMsg.role == entity.ChatRole.assistant ? entityMsg.detectedIntent : null,
    );
  }

  entity.ChatMessage _toEntity(ChatMessageModel model) {
    return entity.ChatMessage(
      messageId: model.id,
      role: model.role == HiveChatRole.user
          ? entity.ChatRole.user
          : entity.ChatRole.assistant,
      content: model.role == HiveChatRole.assistant
          ? sanitizeAssistantContent(model.content)
          : model.content,
      createdAt: model.createdAt,
      isSafety: model.isSafetyResponse,
      detectedIntent: entity.ChatIntent.fromString(model.detectedIntent),
    );
  }

  StreamSubscription<List<ConnectivityResult>> _listenConnectivity() {
    return Connectivity().onConnectivityChanged.listen((results) {
      // Subscription trước đây không bao giờ bị huỷ (không lưu lại để gọi cancel()) — provider
      // bị dispose (đăng xuất, đóng ProviderScope...) trong lúc 1 sự kiện đổi kết nối mạng
      // đang bay thì callback này vẫn chạy và ghi vào `state` đã dispose, ném
      // "Bad state: Tried to use AiChatViewModel after `dispose` was called" thật trên thiết
      // bị thật (bắt được qua integration_test BF-03, 22/09/2026).
      if (!mounted) return;
      final hasInternet = results.any((r) => r != ConnectivityResult.none);
      state = state.copyWith(isOffline: !hasInternet);

      if (hasInternet) {
        syncPendingMessages();
      }
    });
  }

  @override
  void dispose() {
    _currentCancelToken?.cancel();
    _currentCancelToken = null;
    _connectivitySubscription?.cancel();
    super.dispose();
  }
}

final aiChatViewModelProvider =
    StateNotifierProvider<AiChatViewModel, AiChatState>((ref) {
  return AiChatViewModel(ref);
});

/// Provider ChatLocalRepository (singleton).
final chatLocalRepositoryProvider = Provider<ChatLocalRepository>((ref) {
  return ChatLocalRepository();
});

/// Provider ChatApiRepository (singleton).
final chatApiRepositoryProvider = Provider<ChatApiRepository>((ref) {
  return ChatApiRepository(dio: ref.watch(dioProvider));
});
