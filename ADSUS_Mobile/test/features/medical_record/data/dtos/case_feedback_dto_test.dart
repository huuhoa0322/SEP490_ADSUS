import 'package:adsus_mobile/features/medical_record/data/dtos/case_feedback_dto.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('CaseFeedbackDto.fromJson', () {
    test('parse dung 4 field tu JSON (FT-37)', () {
      final dto = CaseFeedbackDto.fromJson({
        'id': 'feedback-1',
        'rating': 5,
        'content': 'Bac si rat tan tam',
        'submittedAt': '2026-08-20T09:30:00Z',
      });

      expect(dto.id, 'feedback-1');
      expect(dto.rating, 5);
      expect(dto.content, 'Bac si rat tan tam');
      expect(dto.submittedAt, '2026-08-20T09:30:00Z');
    });

    test('content null (khong bat buoc) thi field cung null, khong nem loi', () {
      final dto = CaseFeedbackDto.fromJson({
        'id': 'feedback-1',
        'rating': 4,
        'content': null,
        'submittedAt': '2026-08-20T09:30:00Z',
      });

      expect(dto.content, isNull);
    });
  });
}
