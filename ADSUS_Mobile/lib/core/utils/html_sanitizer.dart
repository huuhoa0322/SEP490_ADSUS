/// Utility làm sạch chuỗi văn bản, loại bỏ thẻ HTML và mã độc (XSS) trước khi hiển thị trên UI.
class HtmlSanitizer {
  const HtmlSanitizer._();

  static final RegExp _scriptBlock = RegExp(
    r'<\s*script[^>]*>[\s\S]*?<\s*/\s*script\s*>',
    caseSensitive: false,
  );
  static final RegExp _styleBlock = RegExp(
    r'<\s*style[^>]*>[\s\S]*?<\s*/\s*style\s*>',
    caseSensitive: false,
  );
  static final RegExp _iframeBlock = RegExp(
    r'<\s*iframe[^>]*>[\s\S]*?<\s*/\s*iframe\s*>',
    caseSensitive: false,
  );
  static final RegExp _htmlPattern = RegExp(r'<[a-zA-Z\/][^>]*>');
  static final RegExp _htmlTag = RegExp(r'<[^>]+>');
  static final RegExp _multiSpace = RegExp(r'\s+');

  /// Kiểm tra xem chuỗi có chứa thẻ HTML không.
  /// Trả về `true` cho các thẻ HTML như `<html>`, `<b>`, `<script>`.
  /// Trả về `false` cho các ký hiệu y khoa như '< 3 ngày', '> 38.5°C', chuỗi rỗng hoặc null.
  static bool containsHtml(String? input) {
    if (input == null || input.isEmpty) return false;
    return _htmlPattern.hasMatch(input);
  }

  /// Loại bỏ toàn bộ thẻ HTML, script, iframe và decode các thực thể HTML an toàn.
  /// Ví dụ: `<b>Nguyễn</b><script>alert(1)</script>` -> `'Nguyễn'`
  static String sanitize(String? input) {
    if (input == null || input.trim().isEmpty) return '';

    var text = input;
    // 1. Xóa toàn bộ khối script, style, iframe cùng nội dung bên trong
    text = text.replaceAll(_scriptBlock, '');
    text = text.replaceAll(_styleBlock, '');
    text = text.replaceAll(_iframeBlock, '');

    // 2. Bóc toàn bộ các thẻ HTML còn lại
    text = text.replaceAll(_htmlTag, '');

    // 3. Giải mã các ký tự thực thể HTML thông dụng
    text = _decodeEntities(text);

    // 4. Chuẩn hóa khoảng trắng và cắt tỉa
    return text.replaceAll(_multiSpace, ' ').trim();
  }

  /// Giải mã thực thể HTML an toàn (thực thể &amp; thay thế cuối cùng để tránh double decode).
  static String _decodeEntities(String text) {
    return text
        .replaceAll('&lt;', '<')
        .replaceAll('&gt;', '>')
        .replaceAll('&quot;', '"')
        .replaceAll('&#39;', "'")
        .replaceAll('&apos;', "'")
        .replaceAll('&nbsp;', ' ')
        .replaceAll('&amp;', '&');
  }

  /// Định dạng số điện thoại an toàn (VD: '0912 345 678' hoặc 'Chưa cập nhật').
  static String formatPhone(String? phone) {
    final clean = sanitize(phone);
    if (clean.isEmpty) return 'Chưa cập nhật';
    if (RegExp(r'^0\d{9}$').hasMatch(clean)) {
      return '${clean.substring(0, 4)} ${clean.substring(4, 7)} ${clean.substring(7)}';
    }
    return clean;
  }

  /// Định dạng giới tính an toàn.
  static String formatGender(String? gender) {
    final clean = sanitize(gender);
    if (clean.isEmpty) return 'Chưa cập nhật';
    return clean;
  }

  /// Định dạng ngày (dd/MM/yyyy) an toàn.
  static String formatDate(DateTime? date) {
    if (date == null) return 'Chưa cập nhật';
    final d = date.day.toString().padLeft(2, '0');
    final m = date.month.toString().padLeft(2, '0');
    final y = date.year.toString();
    return '$d/$m/$y';
  }
}
