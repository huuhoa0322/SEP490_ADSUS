import 'package:hive_flutter/hive_flutter.dart';

import '../models/chat_message_model.dart';

/// Repository lưu trữ tin nhắn chat cục bộ bằng Hive.
///
/// Mỗi userId có một Hive box riêng (`chat_messages_<userId>`) để isolation.
class ChatLocalRepository {
  ChatLocalRepository();

  Box<ChatMessageModel>? _box;

  /// Mở Hive box cho user cụ thể. Gọi sau khi đăng nhập thành công.
  Future<void> openForUser(String userId) async {
    _box = await Hive.openBox<ChatMessageModel>('chat_messages_$userId');
  }

  /// Đóng box hiện tại. Gọi khi đăng xuất.
  Future<void> close() async {
    await _box?.close();
    _box = null;
  }

  Box<ChatMessageModel> get _safeBox {
    if (_box == null || !_box!.isOpen) {
      throw StateError(
          'ChatLocalRepository: box not opened. Call openForUser() first.');
    }
    return _box!;
  }

  /// Lưu một tin nhắn.
  Future<void> saveMessage(ChatMessageModel message) async {
    await _safeBox.put(message.id, message);
    await _trimToLimit();
  }

  /// Lưu nhiều tin nhắn cùng lúc.
  Future<void> saveMessages(List<ChatMessageModel> messages) async {
    final map = {for (final m in messages) m.id: m};
    await _safeBox.putAll(map);
    await _trimToLimit();
  }

  /// Lấy tất cả tin nhắn, sắp xếp theo createdAt tăng dần.
  List<ChatMessageModel> getAllMessages() {
    final list = _safeBox.values.toList();
    list.sort((a, b) => a.createdAt.compareTo(b.createdAt));
    return list;
  }

  /// Lấy tin nhắn chưa sync lên server.
  List<ChatMessageModel> getUnsyncedMessages() {
    return _safeBox.values.where((m) => !m.isSynced).toList();
  }

  /// Đánh dấu một tin nhắn là đã sync.
  Future<void> markAsSynced(String id) async {
    final existing = _safeBox.get(id);
    if (existing != null) {
      await _safeBox.put(id, existing.copyWith(isSynced: true));
    }
  }

  /// Xoá toàn bộ tin nhắn (sau khi user xác nhận xoá lịch sử).
  Future<void> clearAll() async {
    await _safeBox.clear();
  }

  /// Giới hạn số tin nhắn lưu cục bộ để tránh box phình quá lớn.
  /// Giữ 200 tin nhắn gần nhất.
  Future<void> _trimToLimit() async {
    const limit = 200;
    if (_safeBox.length <= limit) return;

    final sorted = _safeBox.values.toList()
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));

    final toDelete = sorted.skip(limit).map((m) => m.id).toList();
    await _safeBox.deleteAll(toDelete);
  }
}
