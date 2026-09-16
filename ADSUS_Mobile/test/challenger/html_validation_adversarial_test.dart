import 'package:flutter_test/flutter_test.dart';
import 'package:adsus_mobile/core/utils/html_sanitizer.dart';

void main() {
  group('Adversarial Challenge: HtmlSanitizer.containsHtml()', () {
    test('Group 1: Tricky HTML/XSS vectors must be detected (returns true)', () {
      final vectors = [
        '<SCRIPT>alert(1)</SCRIPT>',
        '<div style="display:none">',
        '<img/src=x onerror=alert(1)>',
        '<svg onload=alert(1)>',
        '<a href="javascript:...">',
        '<br/>',
        '<hr/>',
        '<textarea></textarea>',
        '</br>',
        '</p>',
        '<IMG SRC="javascript:alert(1);">',
        '<svg/onload=alert(1)>',
        '<BODY ONLOAD=alert(1)>',
        '<iframe src="http://evil.com">',
        '<STYLE>.evil{display:none}</STYLE>',
        '<details ontoggle="alert(1)">',
        '<b>bold</b>',
        '<INPUT TYPE="IMAGE" SRC="javascript:alert(1);">',
      ];

      for (final payload in vectors) {
        expect(
          HtmlSanitizer.containsHtml(payload),
          isTrue,
          reason: 'Failed to detect XSS/HTML payload: $payload',
        );
      }
    });

    test('Group 2: Medical notations must NOT be rejected (returns false)', () {
      final notations = [
        'Đau bụng < 3 ngày',
        'Nhiệt độ > 38.5°C',
        'Huyết áp < 120/80 mmHg',
        'SpO2 > 95%',
        'Bạch cầu < 4.0 và SpO2 > 95%',
        'Thân nhiệt <38°C',
        'HbA1c < 6.5%',
        'Bạch cầu <4.0',
        'Glucose < 70 mg/dL',
        'Creatinine <1.2 mg/dL',
        'Kali < 3.5 mEq/L',
        'Tiểu cầu <150 G/L',
        'AST < 35 U/L & ALT < 35 U/L',
        'PaO2 < 60 mmHg, PaCO2 > 45 mmHg',
        'Sốt cao > 39°C kéo dài < 2 ngày',
        'Cân nặng < 2500g',
        'Chiều dài đầu mông < 10mm',
      ];

      for (final note in notations) {
        expect(
          HtmlSanitizer.containsHtml(note),
          isFalse,
          reason: 'False positive rejection on medical notation: $note',
        );
      }
    });

    test('Group 3: Null, empty, and whitespace strings (returns false)', () {
      final blanks = [
        null,
        '',
        '   ',
        '\t\n\r',
        '         ',
      ];

      for (final b in blanks) {
        expect(
          HtmlSanitizer.containsHtml(b),
          isFalse,
          reason: 'Failed on null/empty/whitespace: $b',
        );
      }
    });
  });
}
