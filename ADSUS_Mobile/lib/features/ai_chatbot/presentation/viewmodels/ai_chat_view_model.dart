import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
    this.isMerging = false,
    this.errorMessage,
    this.isOffline = false,
    this.lastDetectedIntent,
    this.hasLoadedHistory = false,
  });

  final List<entity.ChatMessage> messages;
  final bool isLoading;
  final bool isSending;
  final bool isMerging;
  final String? errorMessage;
  final bool isOffline;
  final entity.ChatIntent? lastDetectedIntent;
  final bool hasLoadedHistory;

  AiChatState copyWith({
    List<entity.ChatMessage>? messages,
    bool? isLoading,
    bool? isSending,
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
    _listenConnectivity();
  }

  final Ref _ref;
  ChatRepository? _chatRepo;

  /// Khởi tạo repository sau khi user đăng nhập.
  void initialize(String userId) {
    final local = _ref.read(chatLocalRepositoryProvider);
    final api = _ref.read(chatApiRepositoryProvider);

    _chatRepo = ChatRepository(local: local, api: api);
    _chatRepo!.onNewMessage = _onNewMessage;

    _chatRepo!.initializeForUser(userId);
    loadHistory();
  }

  /// Dọn dẹp khi đăng xuất.
  Future<void> disposeForUser() async {
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

  /// Gửi tin nhắn mới.
  Future<void> sendMessage(String content) async {
    if (_chatRepo == null || content.trim().isEmpty) return;

    state = state.copyWith(isSending: true, clearError: true);

    try {
      await _chatRepo!.sendMessage(content.trim());
      state = state.copyWith(isSending: false);
      // _onNewMessage đã update state rồi
    } catch (e) {
      state = state.copyWith(
        isSending: false,
        errorMessage: 'Gửi tin nhắn thất bại. Vui lòng thử lại.',
      );
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

  void _listenConnectivity() {
    Connectivity().onConnectivityChanged.listen((results) {
      final hasInternet = results.any((r) => r != ConnectivityResult.none);
      state = state.copyWith(isOffline: !hasInternet);

      if (hasInternet) {
        syncPendingMessages();
      }
    });
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
