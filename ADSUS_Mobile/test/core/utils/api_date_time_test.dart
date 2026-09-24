import 'package:flutter_test/flutter_test.dart';
import 'package:adsus_mobile/core/utils/api_date_time.dart';

void main() {
  // Không phụ thuộc múi giờ của máy chạy test (máy dev UTC+7, CI thường là UTC): chỉ kiểm tra
  // kết quả là giờ máy (isUtc = false) và vẫn đúng cùng một thời điểm.
  group('ApiDateTime', () {
    test('parse: mốc UTC có hậu tố Z được đổi sang giờ máy, giữ nguyên thời điểm', () {
      const raw = '2026-09-24T01:30:00Z';

      final result = ApiDateTime.parse(raw);

      expect(result.isUtc, isFalse);
      expect(result.millisecondsSinceEpoch,
          DateTime.utc(2026, 9, 24, 1, 30).millisecondsSinceEpoch);
      expect(result, DateTime.utc(2026, 9, 24, 1, 30).toLocal());
    });

    test('parse: chuỗi chỉ có ngày (DateOnly) giữ nguyên ngày', () {
      final result = ApiDateTime.parse('2026-09-18');

      expect(result, DateTime(2026, 9, 18));
    });

    test('tryParse: null, rỗng hoặc sai định dạng trả null', () {
      expect(ApiDateTime.tryParse(null), isNull);
      expect(ApiDateTime.tryParse(''), isNull);
      expect(ApiDateTime.tryParse('khong-phai-ngay'), isNull);
    });

    test('tryParse: mốc UTC hợp lệ được đổi sang giờ máy', () {
      final result = ApiDateTime.tryParse('2026-09-24T01:30:00.000Z');

      expect(result, isNotNull);
      expect(result!.isUtc, isFalse);
      expect(result, DateTime.utc(2026, 9, 24, 1, 30).toLocal());
    });
  });
}
