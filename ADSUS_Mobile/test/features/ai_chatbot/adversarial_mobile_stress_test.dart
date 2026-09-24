import 'dart:async';
import 'dart:convert';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_markdown_plus/flutter_markdown_plus.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:adsus_mobile/core/theme/app_theme.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/dtos/chat_dto.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/dtos/chat_stream_event.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/models/chat_message_model.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/repositories/chat_api_repository.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/repositories/chat_local_repository.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/repositories/chat_repository.dart';
import 'package:adsus_mobile/features/ai_chatbot/domain/entities/chat_message.dart';
import 'package:adsus_mobile/features/ai_chatbot/presentation/viewmodels/ai_chat_view_model.dart';

/// In-memory mock for Hive local storage tracking all operations.
class MockLocalRepository implements ChatLocalRepository {
  final Map<String, ChatMessageModel> storage = {};
  int saveCount = 0;
  int markSyncedCount = 0;

  @override
  Future<void> openForUser(String userId) async {}

  @override
  Future<void> close() async {
    storage.clear();
  }

  @override
  Future<void> saveMessage(ChatMessageModel message) async {
    saveCount++;
    storage[message.id] = message;
  }

  @override
  Future<void> saveMessages(List<ChatMessageModel> msgs) async {
    for (final m in msgs) {
      saveCount++;
      storage[m.id] = m;
    }
  }

  @override
  List<ChatMessageModel> getAllMessages() {
    final list = storage.values.toList();
    list.sort((a, b) => a.createdAt.compareTo(b.createdAt));
    return list;
  }

  @override
  List<ChatMessageModel> getUnsyncedMessages() {
    return storage.values.where((m) => !m.isSynced).toList();
  }

  @override
  Future<void> markAsSynced(String id) async {
    markSyncedCount++;
    final existing = storage[id];
    if (existing != null) {
      storage[id] = existing.copyWith(isSynced: true);
    }
  }

  @override
  Future<void> clearAll() async {
    storage.clear();
  }
}

/// Mock API repository allowing fine-grained streaming control.
class MockApiRepository implements ChatApiRepository {
  MockApiRepository({this.streamHandler});

  Stream<ChatStreamEvent> Function(String content, CancelToken? cancelToken)? streamHandler;
  int streamCalls = 0;

  @override
  Dio get dio => throw UnimplementedError();

  @override
  Stream<ChatStreamEvent> streamMessage(String content, {CancelToken? cancelToken}) async* {
    streamCalls++;
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

  // ═════════════════════════════════════════════════════════════════════════
  // 1. ADVERSARIAL SSE STREAMING & NETWORK CHUNK BOUNDARY TESTS
  // ═════════════════════════════════════════════════════════════════════════
  group('Adversarial 1: SSE Chunk Boundary & Multibyte UTF-8 Splitting', () {
    test('Extreme stress: 1-byte chunks for Vietnamese UTF-8 SSE stream', () async {
      // Vietnamese text containing 2-byte and 3-byte UTF-8 code points
      const rawSse = 'event: thinking\n'
          'data: {"status":"thinking"}\n\n'
          'event: delta\n'
          'data: {"chunk":"Phụ nữ mang thai cần bổ sung acid folic và sắt đầy đủ."}\n\n'
          'event: done\n'
          'data: {"messageId":"m-1","role":"ASSISTANT","content":"Phụ nữ mang thai cần bổ sung acid folic và sắt đầy đủ.","createdAt":"2026-09-24T12:00:00.000Z"}\n\n';

      final allBytes = utf8.encode(rawSse);
      final controller = StreamController<List<int>>();

      final parsedEventsFuture = parseSseStream(controller.stream).toList();

      // Feed byte-by-byte (1 byte per chunk) to deliberately split multi-byte UTF-8 sequences
      for (int i = 0; i < allBytes.length; i++) {
        controller.add([allBytes[i]]);
      }
      await controller.close();

      final events = await parsedEventsFuture;
      expect(events.length, 3);
      expect(events[0], isA<ChatStreamThinkingEvent>());
      expect(events[1], isA<ChatStreamDeltaEvent>());
      expect((events[1] as ChatStreamDeltaEvent).chunk,
          'Phụ nữ mang thai cần bổ sung acid folic và sắt đầy đủ.');
      expect(events[2], isA<ChatStreamDoneEvent>());
      expect((events[2] as ChatStreamDoneEvent).content,
          'Phụ nữ mang thai cần bổ sung acid folic và sắt đầy đủ.');
    });

    test('Arbitrary packet cuts splitting JSON keywords and UTF-8 multi-byte characters', () async {
      const chunk1 = 'event: delta\ndata: {"ch';
      const chunk2 = 'unk":"Chào bạn, bạn có triệu ';
      const chunk3 = 'chứng gì lạ kh';
      const chunk4 = 'ông?"}\n\nevent: done\ndata: {"messageId":"done-';
      const chunk5 = '123","role":"ASSISTANT","content":"Chào bạn, bạn có triệu chứng gì lạ không?"}\n\n';

      final fullString = '$chunk1$chunk2$chunk3$chunk4$chunk5';
      final allBytes = utf8.encode(fullString);

      // Cut arbitrarily at arbitrary positions
      final cuts = [15, 37, 58, 92, 120, allBytes.length];
      final controller = StreamController<List<int>>();
      final streamFuture = parseSseStream(controller.stream).toList();

      int prev = 0;
      for (final cut in cuts) {
        controller.add(allBytes.sublist(prev, cut));
        prev = cut;
      }
      await controller.close();

      final events = await streamFuture;
      expect(events.length, 2);
      expect(events[0], isA<ChatStreamDeltaEvent>());
      expect((events[0] as ChatStreamDeltaEvent).chunk, 'Chào bạn, bạn có triệu chứng gì lạ không?');
      expect(events[1], isA<ChatStreamDoneEvent>());
      expect((events[1] as ChatStreamDoneEvent).messageId, 'done-123');
    });

    test('Handles Windows CRLF (\\r\\n) line endings seamlessly', () async {
      const crlfSse = 'event: delta\r\ndata: {"chunk":"Dòng 1"}\r\n\r\nevent: delta\r\ndata: {"chunk":"Dòng 2"}\r\n\r\n';
      final byteStream = Stream.value(utf8.encode(crlfSse));
      final events = await parseSseStream(byteStream).toList();

      expect(events.length, 2);
      expect((events[0] as ChatStreamDeltaEvent).chunk, 'Dòng 1');
      expect((events[1] as ChatStreamDeltaEvent).chunk, 'Dòng 2');
    });

    test('Coalesced packets: multiple SSE events packed in a single TCP frame', () async {
      const coalesced = 'event: thinking\ndata: {}\n\n'
          'event: delta\ndata: {"chunk":"A"}\n\n'
          'event: delta\ndata: {"chunk":"B"}\n\n'
          'event: delta\ndata: {"chunk":"C"}\n\n'
          ': keepalive\n\n'
          'event: done\ndata: {"messageId":"id1","content":"ABC"}\n\n';

      final byteStream = Stream.value(utf8.encode(coalesced));
      final events = await parseSseStream(byteStream).toList();

      expect(events.length, 5);
      expect(events[0], isA<ChatStreamThinkingEvent>());
      expect((events[1] as ChatStreamDeltaEvent).chunk, 'A');
      expect((events[2] as ChatStreamDeltaEvent).chunk, 'B');
      expect((events[3] as ChatStreamDeltaEvent).chunk, 'C');
      expect(events[4], isA<ChatStreamDoneEvent>());
    });

    test('Malformed JSON line in SSE fallback without crashing', () async {
      const raw = 'event: delta\ndata: {unquoted_broken_json\n\n';
      final byteStream = Stream.value(utf8.encode(raw));
      final events = await parseSseStream(byteStream).toList();

      expect(events.length, 1);
      expect(events[0], isA<ChatStreamDeltaEvent>());
      expect((events[0] as ChatStreamDeltaEvent).chunk, '{unquoted_broken_json');
    });
  });

  // ═════════════════════════════════════════════════════════════════════════
  // 2. ADVERSARIAL CONCURRENCY & RAPID DOUBLE-TAP TESTS
  // ═════════════════════════════════════════════════════════════════════════
  group('Adversarial 2: Concurrency & Double Submission Prevention', () {
    late MockLocalRepository mockLocal;
    late MockApiRepository mockApi;
    late ProviderContainer container;

    setUp(() {
      mockLocal = MockLocalRepository();
      mockApi = MockApiRepository();
      container = ProviderContainer(
        overrides: [
          chatLocalRepositoryProvider.overrideWithValue(mockLocal),
          chatApiRepositoryProvider.overrideWithValue(mockApi),
        ],
      );
    });

    tearDown(() {
      container.dispose();
    });

    test('Rapid spam: 50 concurrent calls to sendMessage only triggers 1 network stream', () async {
      final streamController = StreamController<ChatStreamEvent>();
      mockApi.streamHandler = (content, cancelToken) => streamController.stream;

      final vm = container.read(aiChatViewModelProvider.notifier);
      await vm.initialize('user-stress-1');

      // Fire 50 calls in parallel simultaneously
      final futures = List.generate(50, (i) => vm.sendMessage('Query $i'));

      await Future<void>.delayed(Duration.zero);

      // Verify that EXACTLY 1 stream was initiated
      expect(mockApi.streamCalls, 1);
      expect(vm.state.isSending, isTrue);

      // Verify that only the first message is in state.messages (user message)
      expect(vm.state.messages.length, 1);
      expect(vm.state.messages.first.content, 'Query 0');

      // Send done event to finish stream
      streamController.add(ChatStreamDoneEvent(
        messageId: 'res-1',
        role: 'ASSISTANT',
        content: 'Answer 0',
        createdAt: DateTime.now(),
      ));
      await streamController.close();
      await Future.wait(futures);

      expect(vm.state.isSending, isFalse);
      expect(vm.state.messages.length, 2);
      expect(vm.state.messages.last.content, 'Answer 0');
    });

    test('Whitespace-only or empty strings are strictly rejected without network calls', () async {
      final vm = container.read(aiChatViewModelProvider.notifier);
      await vm.initialize('user-stress-2');

      await vm.sendMessage('');
      await vm.sendMessage('   ');
      await vm.sendMessage('\n\t  \r');

      expect(mockApi.streamCalls, 0);
      expect(vm.state.isSending, isFalse);
      expect(vm.state.messages, isEmpty);
    });

    test('Sequential message sends succeed once previous stream completes', () async {
      final controller1 = StreamController<ChatStreamEvent>();
      final controller2 = StreamController<ChatStreamEvent>();
      int currentCall = 0;

      mockApi.streamHandler = (content, cancelToken) {
        currentCall++;
        return currentCall == 1 ? controller1.stream : controller2.stream;
      };

      final vm = container.read(aiChatViewModelProvider.notifier);
      await vm.initialize('user-stress-3');

      // Send 1
      final send1 = vm.sendMessage('First message');
      await Future<void>.delayed(Duration.zero);
      expect(vm.state.isSending, isTrue);

      controller1.add(ChatStreamDoneEvent(
        messageId: 'd1',
        role: 'ASSISTANT',
        content: 'Reply 1',
        createdAt: DateTime.now(),
      ));
      await controller1.close();
      await send1;

      expect(vm.state.isSending, isFalse);
      expect(vm.state.messages.length, 2);

      // Send 2
      final send2 = vm.sendMessage('Second message');
      await Future<void>.delayed(Duration.zero);
      expect(vm.state.isSending, isTrue);

      controller2.add(ChatStreamDoneEvent(
        messageId: 'd2',
        role: 'ASSISTANT',
        content: 'Reply 2',
        createdAt: DateTime.now(),
      ));
      await controller2.close();
      await send2;

      expect(vm.state.isSending, isFalse);
      expect(vm.state.messages.length, 4);
      expect(mockApi.streamCalls, 2);
    });
  });

  // ═════════════════════════════════════════════════════════════════════════
  // 3. ADVERSARIAL HIVE DISK POLLUTION ON MID-STREAM NETWORK DROPS
  // ═════════════════════════════════════════════════════════════════════════
  group('Adversarial 3: Hive Disk Pollution & Stream Drop Invariants', () {
    late MockLocalRepository mockLocal;
    late MockApiRepository mockApi;
    late ChatRepository chatRepository;

    setUp(() {
      mockLocal = MockLocalRepository();
      mockApi = MockApiRepository();
      chatRepository = ChatRepository(local: mockLocal, api: mockApi);
    });

    test('Network drops mid-stream after receiving 5 deltas: Hive contains ZERO assistant messages', () async {
      final controller = StreamController<ChatStreamEvent>();
      mockApi.streamHandler = (content, token) => controller.stream;

      final events = <ChatStreamEvent>[];
      final subscription = chatRepository.streamMessage('Triệu chứng sốt xuất huyết').listen(events.add);

      await Future<void>.delayed(Duration.zero);

      // User message is saved immediately
      expect(mockLocal.storage.length, 1);
      final userMsgKey = mockLocal.storage.keys.first;
      expect(mockLocal.storage[userMsgKey]!.role, HiveChatRole.user);
      expect(mockLocal.storage[userMsgKey]!.isSynced, isFalse);

      // Deliver 5 deltas
      controller.add(const ChatStreamDeltaEvent('Sốt '));
      controller.add(const ChatStreamDeltaEvent('cao '));
      controller.add(const ChatStreamDeltaEvent('đột '));
      controller.add(const ChatStreamDeltaEvent('ngột, '));
      controller.add(const ChatStreamDeltaEvent('đau đầu.'));
      await Future<void>.delayed(Duration.zero);

      // Verify Hive is STILL clean from partial assistant chunks
      expect(mockLocal.storage.length, 1);
      expect(mockLocal.storage.values.every((m) => m.role == HiveChatRole.user), isTrue,
          reason: 'No partial assistant message must be committed to Hive disk');

      // Abrupt drop: controller errors out and closes
      controller.addError(DioException(
        requestOptions: RequestOptions(path: '/api/v1/me/chat/messages'),
        type: DioExceptionType.connectionError,
        error: 'Network connection reset by peer',
      ));
      await controller.close();
      await subscription.asFuture().catchError((_) {});

      // Verify final Hive state: Hive has ONLY 1 message (the user message) and ZERO assistant messages
      expect(mockLocal.storage.length, 1);
      expect(mockLocal.storage.values.first.role, HiveChatRole.user);
      expect(mockLocal.storage.values.first.content, 'Triệu chứng sốt xuất huyết');
      expect(mockLocal.storage.values.first.isSynced, isFalse,
          reason: 'User message must remain unsynced for later retry');
    });

    test('ViewModel handles mid-stream drop: resets isSending/isThinking without crashing', () async {
      final mockLocal = MockLocalRepository();
      final mockApi = MockApiRepository();
      final container = ProviderContainer(
        overrides: [
          chatLocalRepositoryProvider.overrideWithValue(mockLocal),
          chatApiRepositoryProvider.overrideWithValue(mockApi),
        ],
      );

      final controller = StreamController<ChatStreamEvent>();
      mockApi.streamHandler = (content, token) => controller.stream;

      final vm = container.read(aiChatViewModelProvider.notifier);
      await vm.initialize('test-user-drop');

      final sendFuture = vm.sendMessage('Kiểm tra drop');
      await Future<void>.delayed(Duration.zero);

      expect(vm.state.isSending, isTrue);

      controller.add(const ChatStreamDeltaEvent('Dữ liệu dở 1'));
      controller.add(const ChatStreamDeltaEvent('Dữ liệu dở 2'));
      await Future<void>.delayed(Duration.zero);

      // Abrupt close without Done event
      await controller.close();
      await sendFuture;

      // Lock must be released
      expect(vm.state.isSending, isFalse);
      expect(vm.state.isThinking, isFalse);

      container.dispose();
    });
  });

  // ═════════════════════════════════════════════════════════════════════════
  // 4. ADVERSARIAL MARKDOWN SANITIZATION & RENDERING TESTS
  // ═════════════════════════════════════════════════════════════════════════
  group('Adversarial 4: Markdown Sanitization & Rendering Edge Cases', () {
    test('Strips triple-asterisk disclaimers cleanly without stripping valid markdown', () {
      const rawResponse = '''
***Lưu ý: Thông tin do AI sinh ra chỉ mang tính tham khảo.***
### Chế độ ăn cho phụ nữ mang thai

Dưới đây là các chất dinh dưỡng thiết yếu:
- **Acid folic**: 400mcg mỗi ngày
- **Canxi**: 1000mg - 1200mg mỗi ngày
- **Sắt**: 30mg mỗi ngày

*Chú ý uống nhiều nước (khoảng 2-2.5 lít/ngày).*
***Luôn hỏi bác sĩ phụ trách trước khi dùng thêm thuốc bổ sung.***
''';

      final sanitized = sanitizeAssistantContent(rawResponse);

      // Disclaimers MUST be removed
      expect(sanitized, isNot(contains('Thông tin do AI sinh ra')));
      expect(sanitized, isNot(contains('Luôn hỏi bác sĩ phụ trách')));

      // Valid Markdown MUST be preserved
      expect(sanitized, contains('### Chế độ ăn cho phụ nữ mang thai'));
      expect(sanitized, contains('- **Acid folic**: 400mcg mỗi ngày'));
      expect(sanitized, contains('- **Canxi**: 1000mg - 1200mg mỗi ngày'));
      expect(sanitized, contains('- **Sắt**: 30mg mỗi ngày'));
      expect(sanitized, contains('*Chú ý uống nhiều nước (khoảng 2-2.5 lít/ngày).*'));
    });

    test('Preserves markdown headers starting with "Chú ý" or "Lưu ý" when not AI disclaimers', () {
      const raw = '''
### Chú ý quan trọng khi dùng Duphaston
Không tự ý ngưng thuốc khi chưa có chỉ định.
''';
      final sanitized = sanitizeAssistantContent(raw);
      expect(sanitized, contains('### Chú ý quan trọng khi dùng Duphaston'));
      expect(sanitized, contains('Không tự ý ngưng thuốc khi chưa có chỉ định.'));
    });

    test('Preserves bullet lists with bold markers like - **Acid folic**', () {
      const raw = '''
Các vi chất cần thiết:
- **Acid folic**: Ngừa dị tật ống thần kinh
- **DHA**: Phát triển trí não thai nhi
- **I-ốt**: Hỗ trợ tuyến giáp
''';
      final sanitized = sanitizeAssistantContent(raw);
      expect(sanitized, contains('- **Acid folic**: Ngừa dị tật ống thần kinh'));
      expect(sanitized, contains('- **DHA**: Phát triển trí não thai nhi'));
      expect(sanitized, contains('- **I-ốt**: Hỗ trợ tuyến giáp'));
    });

    testWidgets('MarkdownBody widget renders formatted bold, headers, and lists correctly',
        (tester) async {
      const markdownContent = '''
### Dinh dưỡng thai kỳ

- **Acid folic**: 400mcg
- **Canxi**: 1000mg

*Uống sau khi ăn.*
''';

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: MarkdownBody(
              data: markdownContent,
              selectable: true,
              styleSheet: MarkdownStyleSheet(
                p: const TextStyle(fontSize: 14, color: AppColors.navy),
                strong: const TextStyle(fontWeight: FontWeight.w700, color: AppColors.navy),
                h3: const TextStyle(fontSize: 15, fontWeight: FontWeight.bold, color: AppColors.navy),
              ),
            ),
          ),
        ),
      );

      await tester.pumpAndSettle();

      // Ensure MarkdownBody rendered without errors
      expect(find.byType(MarkdownBody), findsOneWidget);
      // The text elements should be present in the widget tree
      expect(find.textContaining('Dinh dưỡng thai kỳ'), findsOneWidget);
      expect(find.textContaining('Acid folic'), findsOneWidget);
      expect(find.textContaining('Canxi'), findsOneWidget);
    });
  });
}
