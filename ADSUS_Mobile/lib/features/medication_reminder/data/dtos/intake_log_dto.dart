/// DTO cho Module 7 — IntakeLogResponse từ backend.
///
/// Chỉ dùng nội bộ feature. Khi refactor `ApiEnvelope` lên `core/network/`,
/// có thể bỏ file này và dùng chung.
class IntakeLogEnvelope {
  const IntakeLogEnvelope({required this.data, required this.message});

  final List<Map<String, dynamic>>? data;
  final String message;

  factory IntakeLogEnvelope.fromJson(Map<String, dynamic>? json) {
    if (json == null) {
      return const IntakeLogEnvelope(data: null, message: '');
    }
    return IntakeLogEnvelope(
      data: (json['data'] as List?)?.cast<Map<String, dynamic>>(),
      message: (json['message'] as String?) ?? '',
    );
  }
}