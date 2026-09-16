import 'package:flutter_test/flutter_test.dart';
import 'package:adsus_mobile/core/utils/html_sanitizer.dart';

void main() {
  group('HtmlSanitizer Unit Tests', () {
    group('sanitize()', () {
      test('TC-SEC-01: strips basic and nested HTML tags', () {
        expect(HtmlSanitizer.sanitize('<b>Nguyễn Văn A</b>'), 'Nguyễn Văn A');
        expect(HtmlSanitizer.sanitize('<div><span>Bố</span></div>'), 'Bố');
        expect(
          HtmlSanitizer.sanitize('<p>Hồ sơ của <i>Trần Văn B</i></p>'),
          'Hồ sơ của Trần Văn B',
        );
      });

      test('TC-SEC-02: strips script tag and its payload completely (alert, XSS)', () {
        // Crucial requirement from ORIGINAL_REQUEST & DISPATCH
        expect(
          HtmlSanitizer.sanitize('<b>Nguyễn</b><script>alert(1)</script>'),
          'Nguyễn',
        );
        expect(
          HtmlSanitizer.sanitize('<script>alert("xss")</script>'),
          '',
        );
        expect(
          HtmlSanitizer.sanitize(
            '<SCRIPT type="text/javascript">\nalert(document.cookie);\n</SCRIPT>Mẹ',
          ),
          'Mẹ',
        );
      });

      test('TC-SEC-03: strips style and iframe blocks and their payloads', () {
        expect(
          HtmlSanitizer.sanitize(
            '<style>body { display: none; }</style>Người thân',
          ),
          'Người thân',
        );
        expect(
          HtmlSanitizer.sanitize(
            '<iframe src="javascript:alert(1)"></iframe>Chị gái',
          ),
          'Chị gái',
        );
      });

      test('TC-SEC-04: strips inline event handlers and malformed tags', () {
        expect(
          HtmlSanitizer.sanitize('<img src=x onerror=alert(1)>Em trai'),
          'Em trai',
        );
        expect(
          HtmlSanitizer.sanitize('<a href="javascript:void(0)">Bác</a>'),
          'Bác',
        );
      });

      test('TC-SEC-05: decodes common HTML entities safely without double decode', () {
        expect(HtmlSanitizer.sanitize('A &amp; B'), 'A & B');
        expect(HtmlSanitizer.sanitize('&lt;Hello&gt;'), '<Hello>');
        expect(HtmlSanitizer.sanitize('&quot;Bố&quot;'), '"Bố"');
        expect(HtmlSanitizer.sanitize('Mẹ&#39;s note'), "Mẹ's note");
        expect(HtmlSanitizer.sanitize('Cô&apos;s phone'), "Cô's phone");
        expect(HtmlSanitizer.sanitize('Họ&nbsp;Tên'), 'Họ Tên');
        // Check safe order: &amp;lt; does not double-decode into <
        expect(HtmlSanitizer.sanitize('&amp;lt;'), '&lt;');
      });

      test('TC-SEC-06: handles null, empty, and whitespace-only strings', () {
        expect(HtmlSanitizer.sanitize(null), '');
        expect(HtmlSanitizer.sanitize(''), '');
        expect(HtmlSanitizer.sanitize('   '), '');
        expect(HtmlSanitizer.sanitize('\n\t  \r\n'), '');
      });

      test('TC-SEC-07: normalizes multiple whitespace and trims edges', () {
        expect(
          HtmlSanitizer.sanitize('   Nguyễn    Thị     Nở   '),
          'Nguyễn Thị Nở',
        );
      });
    });

    group('containsHtml()', () {
      test('TC-HTML-01: detects opening, closing, and standalone HTML tags', () {
        expect(HtmlSanitizer.containsHtml('<html>'), isTrue);
        expect(HtmlSanitizer.containsHtml('<b>Nguyễn</b>'), isTrue);
        expect(HtmlSanitizer.containsHtml('<script>alert(1)</script>'), isTrue);
        expect(HtmlSanitizer.containsHtml('<img src=x onerror=alert(1)>'), isTrue);
        expect(HtmlSanitizer.containsHtml('</div>'), isTrue);
        expect(HtmlSanitizer.containsHtml('<br/>'), isTrue);
      });

      test('TC-HTML-02: does not falsely flag medical symbols and comparisons', () {
        expect(HtmlSanitizer.containsHtml('Đau bụng < 3 ngày'), isFalse);
        expect(HtmlSanitizer.containsHtml('Nhiệt độ > 38.5°C'), isFalse);
        expect(HtmlSanitizer.containsHtml('< 120/80 mmHg và > 38 độ'), isFalse);
        expect(HtmlSanitizer.containsHtml('HA <140 mmHg'), isFalse);
      });

      test('TC-HTML-03: returns false for null, empty, and plain text', () {
        expect(HtmlSanitizer.containsHtml(null), isFalse);
        expect(HtmlSanitizer.containsHtml(''), isFalse);
        expect(HtmlSanitizer.containsHtml('   '), isFalse);
        expect(HtmlSanitizer.containsHtml('Khám bệnh tổng quát'), isFalse);
      });
    });

    group('formatPhone()', () {
      test('TC-PHONE-01: formats valid 10-digit phone starting with 0', () {
        expect(HtmlSanitizer.formatPhone('0912345678'), '0912 345 678');
        expect(HtmlSanitizer.formatPhone('0987654321'), '0987 654 321');
      });

      test('TC-PHONE-02: returns fallback for null or empty input', () {
        expect(HtmlSanitizer.formatPhone(null), 'Chưa cập nhật');
        expect(HtmlSanitizer.formatPhone(''), 'Chưa cập nhật');
        expect(HtmlSanitizer.formatPhone('   '), 'Chưa cập nhật');
      });

      test('TC-PHONE-03: sanitizes HTML in phone before formatting', () {
        expect(
          HtmlSanitizer.formatPhone('<b>0912345678</b>'),
          '0912 345 678',
        );
        expect(
          HtmlSanitizer.formatPhone('0912345678<script>alert(1)</script>'),
          '0912 345 678',
        );
      });

      test('TC-PHONE-04: returns sanitized string if non-standard phone length', () {
        expect(HtmlSanitizer.formatPhone('09123'), '09123');
        expect(HtmlSanitizer.formatPhone('19001000'), '19001000');
      });
    });

    group('formatGender()', () {
      test('TC-GENDER-01: returns formatted gender with fallback', () {
        expect(HtmlSanitizer.formatGender('Nam'), 'Nam');
        expect(HtmlSanitizer.formatGender('Nữ'), 'Nữ');
        expect(HtmlSanitizer.formatGender(null), 'Chưa cập nhật');
        expect(HtmlSanitizer.formatGender(''), 'Chưa cập nhật');
        expect(HtmlSanitizer.formatGender('<b>Nam</b>'), 'Nam');
      });
    });

    group('formatDate()', () {
      test('TC-DATE-01: formats DateTime to dd/MM/yyyy with fallback', () {
        expect(
          HtmlSanitizer.formatDate(DateTime(1995, 4, 9)),
          '09/04/1995',
        );
        expect(
          HtmlSanitizer.formatDate(DateTime(2026, 12, 31)),
          '31/12/2026',
        );
        expect(HtmlSanitizer.formatDate(null), 'Chưa cập nhật');
      });
    });
  });
}
