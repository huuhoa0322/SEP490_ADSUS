/// Vỏ response chuẩn `{code, message, data}` của backend.
///
/// `auth_dtos.dart`/`appointment_dtos.dart` mỗi file đang giữ 1 bản riêng của lớp này —
/// module Medical Record dùng bản chung ở đây thay vì lặp lại lần thứ 3. KHÔNG sửa 2 file
/// kia để dùng chung — đó là quyết định ngoài phạm vi task này (xem ghi chú đầu Task 2).
class ApiEnvelope {
  const ApiEnvelope({required this.code, required this.message, this.data});

  final int code;
  final String message;

  /// Có thể là `List<dynamic>` hoặc `Map<String, dynamic>` tuỳ endpoint.
  final dynamic data;

  factory ApiEnvelope.fromJson(Map<String, dynamic> json) => ApiEnvelope(
        code: json['code'] as int? ?? 0,
        message: json['message'] as String? ?? '',
        data: json['data'],
      );
}

/// Vỏ phân trang chuẩn của backend — `PagedResult<T>`. GET /cases/me trả đúng shape này
/// (API Spec #25 — "same item shape as #24", #24 xác nhận PagedResult).
class PagedResultDto<T> {
  const PagedResultDto({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalItems,
    required this.totalPages,
  });

  final List<T> items;
  final int page;
  final int pageSize;
  final int totalItems;
  final int totalPages;

  factory PagedResultDto.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) itemBuilder,
  ) {
    final items = (json['items'] as List?)
            ?.cast<Map<String, dynamic>>()
            .map(itemBuilder)
            .toList() ??
        const <Never>[];
    return PagedResultDto<T>(
      items: items,
      page: json['page'] as int? ?? 1,
      pageSize: json['pageSize'] as int? ?? items.length,
      totalItems: json['totalItems'] as int? ?? items.length,
      totalPages: json['totalPages'] as int? ?? 1,
    );
  }
}
