import 'package:flutter_test/flutter_test.dart';
import 'package:adsus_mobile/features/health_log/presentation/viewmodels/health_log_view_model.dart';

void main() {
  group('HealthLogState Unit Tests', () {
    test('default state has isSubmitting false and errorMessage null', () {
      const state = HealthLogState();
      expect(state.isSubmitting, isFalse);
      expect(state.errorMessage, isNull);
    });

    test('copyWith updates isSubmitting and errorMessage', () {
      const state = HealthLogState();
      final updated = state.copyWith(isSubmitting: true, errorMessage: 'Lỗi mạng');

      expect(updated.isSubmitting, isTrue);
      expect(updated.errorMessage, 'Lỗi mạng');
    });

    test('copyWith with clearError resets errorMessage to null', () {
      const state = HealthLogState(isSubmitting: false, errorMessage: 'Lỗi cũ');
      expect(state.errorMessage, 'Lỗi cũ');

      final cleared = state.copyWith(clearError: true);
      expect(cleared.errorMessage, isNull);
      expect(cleared.isSubmitting, isFalse);
    });
  });
}
