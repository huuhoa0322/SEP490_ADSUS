import 'package:dio/dio.dart';

import '../dtos/chat_dto.dart';
import '../dtos/chat_stream_event.dart';
import '../models/chat_message_model.dart';
import 'chat_api_repository.dart';
import 'chat_local_repository.dart';

/// Repository gộp: đọc từ local trước (offline-first), merge thêm từ server.
///
/// Luồng load ban đầu:
///  1. Load tất cả từ Hive → hiển thị ngay (offline-first)
///  2. Fetch từ API → merge vào Hive (tránh trùng lặp theo id)
///  3. Trả về danh sách gộp, sorted theo createdAt
///
/// Luồng gửi tin nhắn:
///  1. Lưu USER message vào Hive (ngay lập tức)
///  2. Gọi API → nhận ASSISTANT message
///  3. Lưu ASSISTANT message vào Hive
///  4. Gọi callback để UI cập nhật
class ChatRepository {
  /// Callback để thông báo cho ViewModel khi có message mới được lưu.
  void Function(ChatMessageModel message)? onNewMessage;

  ChatRepository({required this._local, required this._api});

  final ChatLocalRepository _local;
  final ChatApiRepository _api;

  /// Khởi tạo cho user cụ thể — gọi sau khi đăng nhập thành công.
  Future<void> initializeForUser(String userId) async {
    await _local.openForUser(userId);
  }

  /// Dọn dẹp khi đăng xuất.
  Future<void> dispose() async {
    await _local.close();
  }

  /// Load lịch sử: local trước → merge server.
  Future<List<ChatMessageModel>> loadHistory({
    void Function(List<ChatMessageModel> localMessages)? onLocalLoaded,
    void Function(List<ChatMessageModel> mergedMessages)? onMergeComplete,
  }) async {
    final localMessages = _local.getAllMessages();
    onLocalLoaded?.call(localMessages);

    try {
      final serverResponse = await _api.getHistory(
        ChatHistoryRequest(limit: 100),
      );

      final serverMessages =
          serverResponse.messages.map(_toLocalModel).toList();

      final existingIds = localMessages.map((m) => m.id).toSet();

      for (final msg in serverMessages) {
        if (!existingIds.contains(msg.id)) {
          await _local.saveMessage(msg.copyWith(isSynced: true));
        } else {
          await _local.markAsSynced(msg.id);
        }
      }

      final merged = _local.getAllMessages();
      onMergeComplete?.call(merged);
      return merged;
    } catch (_) {
      return localMessages;
    }
  }

  /// Gửi tin nhắn qua SSE stream:
  ///  1. Lưu USER message vào Hive (ngay lập tức) & phát qua onNewMessage
  ///  2. Mở SSE stream từ API
  ///  3. Chuyển tiếp các sự kiện stream (thinking, delta, done, error)
  ///  4. QUAN TRỌNG: CHỈ lưu ASSISTANT message vào Hive khi nhận được sự kiện `done`.
  ///     Các delta trung gian KHÔNG được ghi vào Hive để tránh I/O thrashing và dữ liệu dở dang.
  ///  5. Khi done: lưu ASSISTANT message hoàn chỉnh vào Hive và đánh dấu user message là synced.
  Stream<ChatStreamEvent> streamMessage(String content, {CancelToken? cancelToken}) async* {
    final tempUserId = _generateTempId();

    final userMessage = ChatMessageModel(
      id: tempUserId,
      content: content,
      role: HiveChatRole.user,
      createdAt: DateTime.now(),
      isSafetyResponse: false,
      isSynced: false,
    );
    await _local.saveMessage(userMessage);
    onNewMessage?.call(userMessage);

    try {
      await for (final event in _api.streamMessage(content, cancelToken: cancelToken)) {
        if (event is ChatStreamDoneEvent) {
          // Phòng vệ lệch giờ (clock skew): Đảm bảo timestamp tin nhắn trả lời của AI luôn lớn hơn tin nhắn hỏi của user
          final effectiveCreatedAt = event.createdAt.isBefore(userMessage.createdAt)
              ? userMessage.createdAt.add(const Duration(milliseconds: 500))
              : event.createdAt;

          final assistantMessage = ChatMessageModel(
            id: event.messageId.isNotEmpty ? event.messageId : _generateTempId(),
            content: event.content,
            role: event.role.toLowerCase() == 'assistant'
                ? HiveChatRole.assistant
                : HiveChatRole.user,
            createdAt: effectiveCreatedAt,
            isSafetyResponse: event.isSafetyResponse,
            detectedIntent: event.detectedIntent,
            isSynced: true,
          );
          await _local.saveMessage(assistantMessage);
          await _local.markAsSynced(tempUserId);
        }
        yield event;
      }
    } catch (e) {
      yield ChatStreamErrorEvent(e.toString());
    }
  }

  /// Lưu trực tiếp message assistant hoàn chỉnh vào Hive (dùng cho ViewModel nếu cần).
  Future<void> persistAssistantMessage(ChatMessageModel message, {String? syncedUserMessageId}) async {
    await _local.saveMessage(message);
    if (syncedUserMessageId != null) {
      await _local.markAsSynced(syncedUserMessageId);
    }
  }

  /// Gửi tin nhắn: lưu local → gọi API → lưu reply local.
  Future<ChatMessageModel?> sendMessage(String content) async {
    final userId = _generateTempId();

    final userMessage = ChatMessageModel(
      id: userId,
      content: content,
      role: HiveChatRole.user,
      createdAt: DateTime.now(),
      isSafetyResponse: false,
      isSynced: false,
    );
    await _local.saveMessage(userMessage);
    onNewMessage?.call(userMessage);

    try {
      final response = await _api.sendMessage(
        SendChatMessageRequest(content: content),
      );

      final assistantMessage =
          _toLocalModel(response).copyWith(isSynced: true);
      await _local.saveMessage(assistantMessage);
      onNewMessage?.call(assistantMessage);
      await _local.markAsSynced(userId);

      return assistantMessage;
    } catch (_) {
      return null;
    }
  }

  /// Sync các message chưa được gửi lên server.
  Future<void> syncPendingMessages() async {
    final unsynced = _local.getUnsyncedMessages();
    for (final msg in unsynced) {
      if (msg.role == HiveChatRole.user) {
        try {
          await _api.sendMessage(SendChatMessageRequest(content: msg.content));
          await _local.markAsSynced(msg.id);
        } catch (_) {}
      }
    }
  }

  /// Lấy tất cả message local.
  List<ChatMessageModel> getAllLocalMessages() => _local.getAllMessages();

  /// Xoá toàn bộ lịch sử chat local.
  Future<void> clearHistory() => _local.clearAll();

  // ─── Private helpers ────────────────────────────────────────────────────

  ChatMessageModel _toLocalModel(ChatMessageDto r) {
    return ChatMessageModel(
      id: r.messageId,
      content: r.content,
      role: r.role.toLowerCase() == 'assistant'
          ? HiveChatRole.assistant
          : HiveChatRole.user,
      createdAt: r.createdAt,
      isSafetyResponse: r.isSafetyResponse,
      detectedIntent: r.detectedIntent,
      isSynced: true,
    );
  }

  int _counter = 0;

  String _generateTempId() {
    return 'local_${DateTime.now().millisecondsSinceEpoch}_${_counter++}';
  }
}
