import 'dart:async';
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:adsus_mobile/features/ai_chatbot/data/dtos/chat_stream_event.dart';

void main() {
  group('ChatStreamEvent Parsing Unit Tests', () {
    test('parses thinking event correctly', () {
      final event1 = ChatStreamEvent.parse('thinking', '{"status":"thinking"}');
      expect(event1, isA<ChatStreamThinkingEvent>());
      expect((event1 as ChatStreamThinkingEvent).status, 'thinking');

      final event2 = ChatStreamEvent.parse('', '{"status":"thinking"}');
      expect(event2, isA<ChatStreamThinkingEvent>());
    });

    test('parses delta event with json chunk', () {
      final event = ChatStreamEvent.parse('delta', '{"chunk":"Xin chào"}');
      expect(event, isA<ChatStreamDeltaEvent>());
      expect((event as ChatStreamDeltaEvent).chunk, 'Xin chào');
    });

    test('parses delta event with raw string data', () {
      final event = ChatStreamEvent.parse('delta', 'Bạn cần hỗ trợ gì?');
      expect(event, isA<ChatStreamDeltaEvent>());
      expect((event as ChatStreamDeltaEvent).chunk, 'Bạn cần hỗ trợ gì?');
    });

    test('parses done event with complete metadata', () {
      final jsonStr = jsonEncode({
        'messageId': 'msg-999',
        'role': 'ASSISTANT',
        'content': 'Đây là câu trả lời đầy đủ.',
        'createdAt': '2026-09-24T10:00:00.000Z',
        'isSafetyResponse': true,
        'detectedIntent': 'prescription',
        'isRateLimitExceeded': false,
      });

      final event = ChatStreamEvent.parse('done', jsonStr);
      expect(event, isA<ChatStreamDoneEvent>());
      final done = event as ChatStreamDoneEvent;
      expect(done.messageId, 'msg-999');
      expect(done.role, 'ASSISTANT');
      expect(done.content, 'Đây là câu trả lời đầy đủ.');
      expect(done.createdAt, DateTime.parse('2026-09-24T10:00:00.000Z').toLocal());
      expect(done.isSafetyResponse, isTrue);
      expect(done.detectedIntent, 'prescription');
      expect(done.isRateLimitExceeded, isFalse);
    });

    test('parses error event with json message and code', () {
      final jsonStr = jsonEncode({
        'message': 'Trợ lý AI đang bận. Vui lòng thử lại sau.',
        'code': 'STREAM_ERROR',
      });

      final event = ChatStreamEvent.parse('error', jsonStr);
      expect(event, isA<ChatStreamErrorEvent>());
      final err = event as ChatStreamErrorEvent;
      expect(err.message, 'Trợ lý AI đang bận. Vui lòng thử lại sau.');
      expect(err.code, 'STREAM_ERROR');
    });

    test('parses error event with raw string', () {
      final event = ChatStreamEvent.parse('error', 'Lỗi kết nối máy chủ');
      expect(event, isA<ChatStreamErrorEvent>());
      expect((event as ChatStreamErrorEvent).message, 'Lỗi kết nối máy chủ');
    });
  });

  group('parseSseStream Stream Transformer Unit Tests', () {
    test('parses well-formed SSE stream into sequential events', () async {
      final sseRaw = '''
event: thinking
data: {"status":"thinking"}

event: delta
data: {"chunk":"Chào "}

event: delta
data: {"chunk":"bạn!"}

event: done
data: {"messageId":"m1","role":"ASSISTANT","content":"Chào bạn!","createdAt":"2026-09-24T12:00:00.000Z","isSafetyResponse":false,"detectedIntent":"greeting"}

''';

      final byteStream = Stream.value(utf8.encode(sseRaw));
      final events = await parseSseStream(byteStream).toList();

      expect(events.length, 4);
      expect(events[0], isA<ChatStreamThinkingEvent>());
      expect(events[1], isA<ChatStreamDeltaEvent>());
      expect((events[1] as ChatStreamDeltaEvent).chunk, 'Chào ');
      expect(events[2], isA<ChatStreamDeltaEvent>());
      expect((events[2] as ChatStreamDeltaEvent).chunk, 'bạn!');
      expect(events[3], isA<ChatStreamDoneEvent>());
      expect((events[3] as ChatStreamDoneEvent).messageId, 'm1');
    });

    test('handles split UTF-8 multibyte characters across chunk boundaries', () async {
      // "Tiếng Việt" contains multi-byte UTF-8 sequences.
      // E.g. 'ế' is encoded as 0xC3 0xAA.
      final fullText = 'event: delta\ndata: {"chunk":"Tiếng Việt"}\n\n';
      final bytes = utf8.encode(fullText);

      // Split the bytes right in the middle of a multi-byte character
      final part1 = bytes.sublist(0, 26);
      final part2 = bytes.sublist(26);

      final controller = StreamController<List<int>>();
      final streamFuture = parseSseStream(controller.stream).toList();

      controller.add(part1);
      controller.add(part2);
      await controller.close();

      final events = await streamFuture;
      expect(events.length, 1);
      expect(events.first, isA<ChatStreamDeltaEvent>());
      expect((events.first as ChatStreamDeltaEvent).chunk, 'Tiếng Việt');
    });

    test('handles packet fragmentation across line boundaries', () async {
      // Packet 1 splits "event: del"
      // Packet 2 provides "ta\ndata: {"chunk":"chunk1"}\n"
      // Packet 3 provides "\n" (empty line completing the event)
      final controller = StreamController<List<int>>();
      final streamFuture = parseSseStream(controller.stream).toList();

      controller.add(utf8.encode('event: del'));
      controller.add(utf8.encode('ta\ndata: {"chunk":"chunk1"}\n'));
      controller.add(utf8.encode('\n'));
      await controller.close();

      final events = await streamFuture;
      expect(events.length, 1);
      expect(events.first, isA<ChatStreamDeltaEvent>());
      expect((events.first as ChatStreamDeltaEvent).chunk, 'chunk1');
    });

    test('ignores SSE comment lines and ping frames', () async {
      final sseRaw = '''
: this is an SSE comment
: ping

event: delta
data: {"chunk":"Data"}

''';

      final byteStream = Stream.value(utf8.encode(sseRaw));
      final events = await parseSseStream(byteStream).toList();

      expect(events.length, 1);
      expect(events.first, isA<ChatStreamDeltaEvent>());
      expect((events.first as ChatStreamDeltaEvent).chunk, 'Data');
    });

    test('flushes trailing event even without ending newline', () async {
      final sseRaw = 'event: delta\ndata: {"chunk":"No trailing newline"}';

      final byteStream = Stream.value(utf8.encode(sseRaw));
      final events = await parseSseStream(byteStream).toList();

      expect(events.length, 1);
      expect(events.first, isA<ChatStreamDeltaEvent>());
      expect((events.first as ChatStreamDeltaEvent).chunk, 'No trailing newline');
    });
  });
}
