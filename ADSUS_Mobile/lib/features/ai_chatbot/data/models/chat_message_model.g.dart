// GENERATED CODE — Manually written (no build_runner needed).

part of 'chat_message_model.dart';

class HiveChatRoleAdapter extends TypeAdapter<HiveChatRole> {
  @override
  final int typeId = 0;

  @override
  HiveChatRole read(BinaryReader reader) {
    final index = reader.readByte();
    return HiveChatRole.values[index];
  }

  @override
  void write(BinaryWriter writer, HiveChatRole obj) {
    writer.writeByte(obj.index);
  }
}

class ChatMessageModelAdapter extends TypeAdapter<ChatMessageModel> {
  @override
  final int typeId = 1;

  @override
  ChatMessageModel read(BinaryReader reader) {
    final numOfFields = reader.readByte();
    final fields = <int, dynamic>{
      for (int i = 0; i < numOfFields; i++) reader.readByte(): reader.read(),
    };
    return ChatMessageModel(
      id: fields[0] as String,
      content: fields[1] as String,
      role: fields[2] as HiveChatRole,
      createdAt: fields[3] as DateTime,
      isSafetyResponse: fields[4] as bool,
      detectedIntent: fields[5] as String?,
      isSynced: fields[6] as bool,
    );
  }

  @override
  void write(BinaryWriter writer, ChatMessageModel obj) {
    writer
      ..writeByte(7)
      ..writeByte(0)
      ..write(obj.id)
      ..writeByte(1)
      ..write(obj.content)
      ..writeByte(2)
      ..write(obj.role)
      ..writeByte(3)
      ..write(obj.createdAt)
      ..writeByte(4)
      ..write(obj.isSafetyResponse)
      ..writeByte(5)
      ..write(obj.detectedIntent)
      ..writeByte(6)
      ..write(obj.isSynced);
  }
}
