import 'dart:async';
import 'dart:convert';

/// Kiểu sự kiện Server-Sent Events (SSE) từ pipeline Chatbot AI.
sealed class ChatStreamEvent {
  const ChatStreamEvent();

  /// Parse sự kiện từ tên event và chuỗi data.
  factory ChatStreamEvent.parse(String eventType, String rawData) {
    final trimmedData = rawData.trim();
    Map<String, dynamic>? jsonMap;
    if (trimmedData.startsWith('{') && trimmedData.endsWith('}')) {
      try {
        final decoded = jsonDecode(trimmedData);
        if (decoded is Map<String, dynamic>) {
          jsonMap = decoded;
        }
      } catch (_) {
        // Fallback sang plain text nếu JSON malformed
      }
    }

    final normalizedEvent = eventType.trim().toLowerCase();

    // 1. Thinking event
    if (normalizedEvent == 'thinking' ||
        (normalizedEvent.isEmpty && jsonMap != null && jsonMap['status'] == 'thinking')) {
      final status = jsonMap != null ? (jsonMap['status'] as String? ?? 'thinking') : 'thinking';
      return ChatStreamThinkingEvent(status: status);
    }

    // 2. Delta event
    if (normalizedEvent == 'delta' ||
        (normalizedEvent.isEmpty && jsonMap != null && jsonMap.containsKey('chunk'))) {
      final chunk = jsonMap != null ? (jsonMap['chunk'] as String? ?? '') : rawData;
      return ChatStreamDeltaEvent(chunk);
    }

    // 3. Done event
    if (normalizedEvent == 'done' ||
        (normalizedEvent.isEmpty &&
            jsonMap != null &&
            (jsonMap.containsKey('messageId') || jsonMap.containsKey('content')))) {
      if (jsonMap != null) {
        return ChatStreamDoneEvent.fromJson(jsonMap);
      }
      return ChatStreamDoneEvent(
        messageId: '',
        role: 'ASSISTANT',
        content: rawData,
        createdAt: DateTime.now(),
      );
    }

    // 4. Error event
    if (normalizedEvent == 'error' ||
        (normalizedEvent.isEmpty &&
            jsonMap != null &&
            (jsonMap.containsKey('code') || jsonMap.containsKey('error')))) {
      final message = jsonMap != null
          ? (jsonMap['message'] as String? ?? jsonMap['error'] as String? ?? rawData)
          : rawData;
      final code = jsonMap != null ? jsonMap['code'] as String? : null;
      return ChatStreamErrorEvent(message, code: code);
    }

    // Fallback: nếu event không rõ nhưng có data, coi là delta text
    if (rawData.isNotEmpty) {
      if (jsonMap != null && jsonMap.containsKey('chunk')) {
        return ChatStreamDeltaEvent(jsonMap['chunk'] as String? ?? '');
      }
      return ChatStreamDeltaEvent(rawData);
    }

    return const ChatStreamThinkingEvent();
  }
}

/// Sự kiện model đang suy nghĩ / xử lý pre-checks.
class ChatStreamThinkingEvent extends ChatStreamEvent {
  const ChatStreamThinkingEvent({this.status = 'thinking'});

  final String status;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is ChatStreamThinkingEvent && runtimeType == other.runtimeType && status == other.status;

  @override
  int get hashCode => status.hashCode;

  @override
  String toString() => 'ChatStreamThinkingEvent(status: $status)';
}

/// Sự kiện từng chunk text được LLM sinh ra.
class ChatStreamDeltaEvent extends ChatStreamEvent {
  const ChatStreamDeltaEvent(this.chunk);

  final String chunk;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is ChatStreamDeltaEvent && runtimeType == other.runtimeType && chunk == other.chunk;

  @override
  int get hashCode => chunk.hashCode;

  @override
  String toString() => 'ChatStreamDeltaEvent(chunk: $chunk)';
}

/// Sự kiện kết thúc luồng streaming, chứa toàn bộ metadata và message đã lưu DB.
class ChatStreamDoneEvent extends ChatStreamEvent {
  const ChatStreamDoneEvent({
    required this.messageId,
    required this.role,
    required this.content,
    required this.createdAt,
    this.isSafetyResponse = false,
    this.detectedIntent,
    this.isRateLimitExceeded = false,
  });

  final String messageId;
  final String role;
  final String content;
  final DateTime createdAt;
  final bool isSafetyResponse;
  final String? detectedIntent;
  final bool isRateLimitExceeded;

  factory ChatStreamDoneEvent.fromJson(Map<String, dynamic> json) {
    DateTime parsedDate;
    if (json['createdAt'] != null) {
      parsedDate = DateTime.tryParse(json['createdAt'].toString()) ?? DateTime.now();
    } else {
      parsedDate = DateTime.now();
    }

    return ChatStreamDoneEvent(
      messageId: json['messageId'] as String? ?? '',
      role: json['role'] as String? ?? 'ASSISTANT',
      content: json['content'] as String? ?? '',
      createdAt: parsedDate,
      isSafetyResponse: json['isSafetyResponse'] as bool? ?? false,
      detectedIntent: json['detectedIntent'] as String?,
      isRateLimitExceeded: json['isRateLimitExceeded'] as bool? ?? false,
    );
  }

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is ChatStreamDoneEvent &&
          runtimeType == other.runtimeType &&
          messageId == other.messageId &&
          role == other.role &&
          content == other.content &&
          isSafetyResponse == other.isSafetyResponse &&
          detectedIntent == other.detectedIntent &&
          isRateLimitExceeded == other.isRateLimitExceeded;

  @override
  int get hashCode => Object.hash(
        messageId,
        role,
        content,
        isSafetyResponse,
        detectedIntent,
        isRateLimitExceeded,
      );

  @override
  String toString() =>
      'ChatStreamDoneEvent(id: $messageId, role: $role, intent: $detectedIntent, safety: $isSafetyResponse, limit: $isRateLimitExceeded)';
}

/// Sự kiện lỗi trong quá trình streaming.
class ChatStreamErrorEvent extends ChatStreamEvent {
  const ChatStreamErrorEvent(this.message, {this.code});

  final String message;
  final String? code;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is ChatStreamErrorEvent &&
          runtimeType == other.runtimeType &&
          message == other.message &&
          code == other.code;

  @override
  int get hashCode => Object.hash(message, code);

  @override
  String toString() => 'ChatStreamErrorEvent(message: $message, code: $code)';
}

/// Helper phân giải byte stream thành `Stream<ChatStreamEvent>`.
///
/// Xử lý phân tách chunk UTF-8 (multibyte tiếng Việt) bằng `utf8.decoder`
/// và ghép line bị ngắt packet bằng `LineSplitter`.
Stream<ChatStreamEvent> parseSseStream(Stream<List<int>> byteStream) async* {
  String currentEvent = '';
  final dataBuffer = StringBuffer();

  final lineStream = byteStream
      .cast<List<int>>()
      .transform(utf8.decoder)
      .transform(const LineSplitter());

  await for (final line in lineStream) {
    if (line.isEmpty) {
      // Dòng trống kết thúc 1 frame SSE
      final dataStr = dataBuffer.toString();
      if (currentEvent.isNotEmpty || dataStr.isNotEmpty) {
        yield ChatStreamEvent.parse(currentEvent, dataStr);
      }
      currentEvent = '';
      dataBuffer.clear();
      continue;
    }

    if (line.startsWith(':')) {
      // Comment hoặc SSE ping/keep-alive
      continue;
    }

    if (line.startsWith('event:')) {
      currentEvent = line.substring(6).trim();
    } else if (line.startsWith('data:')) {
      final value = line.substring(5).trimLeft();
      if (dataBuffer.isNotEmpty) {
        dataBuffer.write('\n');
      }
      dataBuffer.write(value);
    }
  }

  // Flush nếu stream đóng mà không có dòng trống cuối
  final remainingData = dataBuffer.toString();
  if (currentEvent.isNotEmpty || remainingData.isNotEmpty) {
    yield ChatStreamEvent.parse(currentEvent, remainingData);
  }
}
