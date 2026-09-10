import 'package:hive_flutter/hive_flutter.dart';

part 'chat_message_model.g.dart';

/// Vai trò tin nhắn — dùng chung cho Hive model.
@HiveType(typeId: 0)
enum HiveChatRole {
  @HiveField(0)
  user,
  @HiveField(1)
  assistant,
}

/// Tin nhắn chat được lưu trong Hive — map 1-1 với ChatMessage entity.
///
/// Thuộc tính `isSynced` đánh dấu tin nhắn đã được gửi lên server chưa.
/// Tin nhắn USER chưa sync = pending; tin nhắn ASSISTANT luôn = synced.
@HiveType(typeId: 1)
class ChatMessageModel extends HiveObject {
  @HiveField(0)
  final String id;

  @HiveField(1)
  final String content;

  @HiveField(2)
  final HiveChatRole role;

  @HiveField(3)
  final DateTime createdAt;

  @HiveField(4)
  final bool isSafetyResponse;

  @HiveField(5)
  final String? detectedIntent;

  @HiveField(6)
  final bool isSynced;

  ChatMessageModel({
    required this.id,
    required this.content,
    required this.role,
    required this.createdAt,
    required this.isSafetyResponse,
    this.detectedIntent,
    required this.isSynced,
  });

  ChatMessageModel copyWith({
    String? id,
    String? content,
    HiveChatRole? role,
    DateTime? createdAt,
    bool? isSafetyResponse,
    String? detectedIntent,
    bool? isSynced,
  }) {
    return ChatMessageModel(
      id: id ?? this.id,
      content: content ?? this.content,
      role: role ?? this.role,
      createdAt: createdAt ?? this.createdAt,
      isSafetyResponse: isSafetyResponse ?? this.isSafetyResponse,
      detectedIntent: detectedIntent ?? this.detectedIntent,
      isSynced: isSynced ?? this.isSynced,
    );
  }
}
