import 'dart:async';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:adsus_mobile/features/ai_chatbot/data/dtos/chat_dto.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/dtos/chat_stream_event.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/models/chat_message_model.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/repositories/chat_api_repository.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/repositories/chat_local_repository.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/repositories/chat_repository.dart';

class FakeChatLocalRepository implements ChatLocalRepository {
  final Map<String, ChatMessageModel> messages = {};
  bool isOpened = false;

  @override
  Future<void> openForUser(String userId) async {
    isOpened = true;
  }

  @override
  Future<void> close() async {
    isOpened = false;
  }

  @override
  Future<void> saveMessage(ChatMessageModel message) async {
    messages[message.id] = message;
  }

  @override
  Future<void> saveMessages(List<ChatMessageModel> msgs) async {
    for (final m in msgs) {
      messages[m.id] = m;
    }
  }

  @override
  List<ChatMessageModel> getAllMessages() {
    final list = messages.values.toList();
    list.sort((a, b) => a.createdAt.compareTo(b.createdAt));
    return list;
  }

  @override
  List<ChatMessageModel> getUnsyncedMessages() {
    return messages.values.where((m) => !m.isSynced).toList();
  }

  @override
  Future<void> markAsSynced(String id) async {
    final existing = messages[id];
    if (existing != null) {
      messages[id] = existing.copyWith(isSynced: true);
    }
  }

  @override
  Future<void> clearAll() async {
    messages.clear();
  }
}

class FakeChatApiRepository implements ChatApiRepository {
  FakeChatApiRepository({this.streamHandler});

  final Stream<ChatStreamEvent> Function(String content, CancelToken? cancelToken)? streamHandler;

  @override
  Dio get dio => throw UnimplementedError();

  @override
  Stream<ChatStreamEvent> streamMessage(String content, {CancelToken? cancelToken}) async* {
    if (streamHandler != null) {
      yield* streamHandler!(content, cancelToken);
    }
  }

  @override
  Future<ChatMessageDto> sendMessage(SendChatMessageRequest request) async {
    throw UnimplementedError();
  }

  @override
  Future<ChatHistoryDto> getHistory(ChatHistoryRequest request) async {
    return const ChatHistoryDto(messages: []);
  }
}

void main() {
  group('ChatRepository SSE Streaming & Hive Persistence Invariants', () {
    late FakeChatLocalRepository fakeLocal;
    late FakeChatApiRepository fakeApi;
    late ChatRepository repository;

    setUp(() {
      fakeLocal = FakeChatLocalRepository();
      fakeApi = FakeChatApiRepository();
      repository = ChatRepository(local: fakeLocal, api: fakeApi);
    });

    test('saves USER message to Hive immediately, but only commits ASSISTANT on done', () async {
      final controller = StreamController<ChatStreamEvent>();
      fakeApi = FakeChatApiRepository(
        streamHandler: (content, cancelToken) => controller.stream,
      );
      repository = ChatRepository(local: fakeLocal, api: fakeApi);

      ChatMessageModel? callbackUserMessage;
      repository.onNewMessage = (msg) {
        callbackUserMessage = msg;
      };

      final streamEvents = <ChatStreamEvent>[];
      final subscription = repository.streamMessage('Tôi bị sốt').listen(streamEvents.add);

      // 1. Sau khi gọi streamMessage, user message PHẢI đã được lưu Hive ngay lập tức
      await Future<void>.delayed(Duration.zero);
      expect(fakeLocal.messages.length, 1);
      final userMsg = fakeLocal.messages.values.first;
      expect(userMsg.role, HiveChatRole.user);
      expect(userMsg.content, 'Tôi bị sốt');
      expect(userMsg.isSynced, isFalse);
      expect(callbackUserMessage, isNotNull);

      // 2. Stream thinking event
      controller.add(const ChatStreamThinkingEvent());
      await Future<void>.delayed(Duration.zero);
      expect(fakeLocal.messages.length, 1); // Vẫn chỉ có user message

      // 3. Stream các delta chunks (QUAN TRỌNG: KHÔNG ĐƯỢC LƯU VÀO HIVE)
      controller.add(const ChatStreamDeltaEvent('Bạn nên '));
      controller.add(const ChatStreamDeltaEvent('đo nhiệt độ.'));
      await Future<void>.delayed(Duration.zero);
      expect(fakeLocal.messages.length, 1,
          reason: 'Intermediate delta chunks must NOT be saved to Hive cache');

      // 4. Stream done event: Bây giờ assistant message mới được lưu Hive hoàn chỉnh
      final doneDate = DateTime(2026, 9, 24, 12, 0);
      controller.add(ChatStreamDoneEvent(
        messageId: 'assistant-100',
        role: 'ASSISTANT',
        content: 'Bạn nên đo nhiệt độ.',
        createdAt: doneDate,
        isSafetyResponse: false,
        detectedIntent: 'prescription',
      ));
      await controller.close();
      await subscription.asFuture();

      // Kiểm tra Hive sau khi done:
      expect(fakeLocal.messages.length, 2);
      final assistantMsg = fakeLocal.messages['assistant-100'];
      expect(assistantMsg, isNotNull);
      expect(assistantMsg!.role, HiveChatRole.assistant);
      expect(assistantMsg.content, 'Bạn nên đo nhiệt độ.');
      expect(assistantMsg.isSynced, isTrue);
      expect(assistantMsg.detectedIntent, 'prescription');

      // User message phải được chuyển thành isSynced: true
      expect(fakeLocal.messages[userMsg.id]!.isSynced, isTrue);
    });

    test('does NOT save assistant message to Hive if stream aborts or errors', () async {
      final controller = StreamController<ChatStreamEvent>();
      fakeApi = FakeChatApiRepository(
        streamHandler: (content, cancelToken) => controller.stream,
      );
      repository = ChatRepository(local: fakeLocal, api: fakeApi);

      final streamEvents = <ChatStreamEvent>[];
      final subscription = repository.streamMessage('Câu hỏi').listen(streamEvents.add);

      await Future<void>.delayed(Duration.zero);
      expect(fakeLocal.messages.length, 1); // User message saved

      // Emit delta rồi error
      controller.add(const ChatStreamDeltaEvent('Đang viết dở...'));
      controller.add(const ChatStreamErrorEvent('Mất kết nối đột ngột'));
      await controller.close();
      await subscription.asFuture();

      // Hive không được chứa assistant message dở dang
      expect(fakeLocal.messages.length, 1);
      expect(fakeLocal.messages.values.first.role, HiveChatRole.user);
    });
  });
}
