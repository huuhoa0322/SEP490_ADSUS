import 'dart:async';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:adsus_mobile/features/ai_chatbot/data/dtos/chat_dto.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/dtos/chat_stream_event.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/models/chat_message_model.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/repositories/chat_api_repository.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/repositories/chat_local_repository.dart';
import 'package:adsus_mobile/features/ai_chatbot/domain/entities/chat_message.dart';
import 'package:adsus_mobile/features/ai_chatbot/presentation/viewmodels/ai_chat_view_model.dart';

class FakeChatLocalRepository implements ChatLocalRepository {
  final Map<String, ChatMessageModel> messages = {};

  @override
  Future<void> openForUser(String userId) async {}

  @override
  Future<void> close() async {
    messages.clear();
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
  List<ChatMessageModel> getUnsyncedMessages() => [];

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
  TestWidgetsFlutterBinding.ensureInitialized();

  group('AiChatState Unit Tests', () {
    test('default state has correct initial values', () {
      const state = AiChatState();
      expect(state.messages, isEmpty);
      expect(state.isLoading, isFalse);
      expect(state.isSending, isFalse);
      expect(state.isThinking, isFalse);
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
        isThinking: true,
        isOffline: true,
        errorMessage: 'Network error',
        lastDetectedIntent: ChatIntent.healthLog,
        hasLoadedHistory: true,
      );

      expect(updated.messages.length, 1);
      expect(updated.messages.first.content, 'Bụng đau');
      expect(updated.isLoading, isTrue);
      expect(updated.isSending, isTrue);
      expect(updated.isThinking, isTrue);
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

  group('AiChatViewModel SSE Streaming State Lifecycle', () {
    late FakeChatLocalRepository fakeLocal;
    late FakeChatApiRepository fakeApi;
    late ProviderContainer container;

    setUp(() {
      fakeLocal = FakeChatLocalRepository();
      fakeApi = FakeChatApiRepository();
      container = ProviderContainer(
        overrides: [
          chatLocalRepositoryProvider.overrideWithValue(fakeLocal),
          chatApiRepositoryProvider.overrideWithValue(fakeApi),
        ],
      );
    });

    tearDown(() {
      container.dispose();
    });

    test('streaming lifecycle: thinking -> deltas in memory -> done finalizes message and resets isSending', () async {
      final controller = StreamController<ChatStreamEvent>();
      fakeApi = FakeChatApiRepository(
        streamHandler: (content, cancelToken) => controller.stream,
      );
      container = ProviderContainer(
        overrides: [
          chatLocalRepositoryProvider.overrideWithValue(fakeLocal),
          chatApiRepositoryProvider.overrideWithValue(fakeApi),
        ],
      );

      final vm = container.read(aiChatViewModelProvider.notifier);
      await vm.initialize('test-user');

      // Bắt đầu gửi tin nhắn
      final sendFuture = vm.sendMessage('Tư vấn uống thuốc');
      await Future<void>.delayed(Duration.zero);

      // isSending phải true ngay lập tức để khoá nút gửi
      expect(vm.state.isSending, isTrue);
      expect(vm.state.isThinking, isTrue);
      // User message đã hiển thị
      expect(vm.state.messages.length, 1);
      expect(vm.state.messages.first.role, ChatRole.user);

      // 1. Thinking event
      controller.add(const ChatStreamThinkingEvent());
      await Future<void>.delayed(Duration.zero);
      expect(vm.state.isThinking, isTrue);
      expect(vm.state.isSending, isTrue);

      // 2. First delta: assistant message xuất hiện trong state.messages
      controller.add(const ChatStreamDeltaEvent('Bạn nên uống '));
      await Future<void>.delayed(Duration.zero);
      expect(vm.state.isThinking, isFalse);
      expect(vm.state.isSending, isTrue);
      expect(vm.state.messages.length, 2);
      expect(vm.state.messages.last.role, ChatRole.assistant);
      expect(vm.state.messages.last.content, 'Bạn nên uống ');

      // 3. Second delta: nối chuỗi trực tiếp trên bộ nhớ
      controller.add(const ChatStreamDeltaEvent('sau bữa ăn.'));
      await Future<void>.delayed(Duration.zero);
      expect(vm.state.isSending, isTrue);
      expect(vm.state.messages.length, 2);
      expect(vm.state.messages.last.content, 'Bạn nên uống sau bữa ăn.');

      // 4. Done event: finalize messageId, intent, và mở khoá send button (isSending = false)
      controller.add(ChatStreamDoneEvent(
        messageId: 'msg-final-1',
        role: 'ASSISTANT',
        content: 'Bạn nên uống sau bữa ăn.',
        createdAt: DateTime(2026, 9, 24, 15, 0),
        detectedIntent: 'prescription',
      ));
      await controller.close();
      await sendFuture;

      expect(vm.state.isSending, isFalse);
      expect(vm.state.isThinking, isFalse);
      expect(vm.state.messages.length, 2);
      expect(vm.state.messages.last.messageId, 'msg-final-1');
      expect(vm.state.messages.last.detectedIntent, ChatIntent.prescription);
      expect(vm.state.lastDetectedIntent, ChatIntent.prescription);
    });

    test('streaming error event: resets isSending and sets user-friendly errorMessage', () async {
      final controller = StreamController<ChatStreamEvent>();
      fakeApi = FakeChatApiRepository(
        streamHandler: (content, cancelToken) => controller.stream,
      );
      container = ProviderContainer(
        overrides: [
          chatLocalRepositoryProvider.overrideWithValue(fakeLocal),
          chatApiRepositoryProvider.overrideWithValue(fakeApi),
        ],
      );

      final vm = container.read(aiChatViewModelProvider.notifier);
      await vm.initialize('test-user');

      final sendFuture = vm.sendMessage('Hỏi bệnh');
      await Future<void>.delayed(Duration.zero);
      expect(vm.state.isSending, isTrue);

      controller.add(const ChatStreamErrorEvent('Mất kết nối máy chủ'));
      await controller.close();
      await sendFuture;

      expect(vm.state.isSending, isFalse);
      expect(vm.state.isThinking, isFalse);
      expect(vm.state.errorMessage, 'Mất kết nối máy chủ');
    });

    test('prevents concurrent sends while streaming is active', () async {
      final controller = StreamController<ChatStreamEvent>();
      int streamCalls = 0;
      fakeApi = FakeChatApiRepository(
        streamHandler: (content, cancelToken) {
          streamCalls++;
          return controller.stream;
        },
      );
      container = ProviderContainer(
        overrides: [
          chatLocalRepositoryProvider.overrideWithValue(fakeLocal),
          chatApiRepositoryProvider.overrideWithValue(fakeApi),
        ],
      );

      final vm = container.read(aiChatViewModelProvider.notifier);
      await vm.initialize('test-user');

      // Gửi lần 1
      final sendFuture1 = vm.sendMessage('Tin nhắn 1');
      await Future<void>.delayed(Duration.zero);
      expect(vm.state.isSending, isTrue);
      expect(streamCalls, 1);

      // Thử gửi lần 2 trong khi stream 1 chưa xong -> PHẢI bị bỏ qua
      await vm.sendMessage('Tin nhắn 2 lặp');
      expect(streamCalls, 1, reason: 'Concurrent send must be prevented while isSending is true');

      controller.add(ChatStreamDoneEvent(
        messageId: 'm1',
        role: 'ASSISTANT',
        content: 'Phản hồi 1',
        createdAt: DateTime.now(),
      ));
      await controller.close();
      await sendFuture1;
      expect(vm.state.isSending, isFalse);
    });
  });
}
