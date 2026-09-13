import 'package:flutter_test/flutter_test.dart';
import 'package:adsus_mobile/features/appointment_scheduling/data/dtos/appointment_dtos.dart';

void main() {
  group('AppointmentDto.fromJson', () {
    test('reads scheduleSlotId when present', () {
      final json = {
        'appointmentId': 'apt-1',
        'scheduleSlotId': 'sched-slot-100',
        'patientProfileId': 'patient-1',
        'reason': 'Kham tong quat',
        'status': 0,
      };

      final dto = AppointmentDto.fromJson(json);

      expect(dto.appointmentId, 'apt-1');
      expect(dto.slotId, 'sched-slot-100');
      expect(dto.status, '0');
    });

    test('falls back to slotId when scheduleSlotId is missing', () {
      final json = {
        'appointmentId': 'apt-2',
        'slotId': 'legacy-slot-200',
        'patientProfileId': 'patient-1',
      };

      final dto = AppointmentDto.fromJson(json);

      expect(dto.slotId, 'legacy-slot-200');
    });

    test('prefers scheduleSlotId over slotId when both are present', () {
      final json = {
        'appointmentId': 'apt-3',
        'scheduleSlotId': 'priority-slot-300',
        'slotId': 'fallback-slot-300',
      };

      final dto = AppointmentDto.fromJson(json);

      expect(dto.slotId, 'priority-slot-300');
    });

    test('sets slotId to null when neither scheduleSlotId nor slotId is present', () {
      final json = {
        'appointmentId': 'apt-4',
      };

      final dto = AppointmentDto.fromJson(json);

      expect(dto.slotId, isNull);
    });

    test('reads cancellationReason when present', () {
      final json = {
        'appointmentId': 'apt-5',
        'cancellationReason': 'Bac si ban cong tac',
        'status': 1,
      };

      final dto = AppointmentDto.fromJson(json);

      expect(dto.cancelledReason, 'Bac si ban cong tac');
      expect(dto.status, '1');
    });

    test('falls back to cancelledReason when cancellationReason is missing', () {
      final json = {
        'appointmentId': 'apt-6',
        'cancelledReason': 'Benh nhan huy hen',
      };

      final dto = AppointmentDto.fromJson(json);

      expect(dto.cancelledReason, 'Benh nhan huy hen');
    });

    test('prefers cancellationReason over cancelledReason when both are present', () {
      final json = {
        'appointmentId': 'apt-7',
        'cancellationReason': 'Ly do moi nhat',
        'cancelledReason': 'Ly do cu',
      };

      final dto = AppointmentDto.fromJson(json);

      expect(dto.cancelledReason, 'Ly do moi nhat');
    });

    test('sets cancelledReason to null when neither cancellation field is present', () {
      final json = {
        'appointmentId': 'apt-8',
      };

      final dto = AppointmentDto.fromJson(json);

      expect(dto.cancelledReason, isNull);
    });
  });

  group('AppointmentSummaryDto.fromJson', () {
    test('reads scheduleSlotId when present', () {
      final json = {
        'appointmentId': 'summary-1',
        'scheduleSlotId': 'sched-slot-400',
        'doctorId': 'doc-1',
        'doctorName': 'Dr. Nam',
        'status': 0,
      };

      final dto = AppointmentSummaryDto.fromJson(json);

      expect(dto.appointmentId, 'summary-1');
      expect(dto.slotId, 'sched-slot-400');
      expect(dto.status, '0');
    });

    test('falls back to slotId when scheduleSlotId is missing', () {
      final json = {
        'appointmentId': 'summary-2',
        'slotId': 'legacy-slot-500',
      };

      final dto = AppointmentSummaryDto.fromJson(json);

      expect(dto.slotId, 'legacy-slot-500');
    });

    test('prefers scheduleSlotId over slotId when both are present', () {
      final json = {
        'appointmentId': 'summary-3',
        'scheduleSlotId': 'priority-slot-600',
        'slotId': 'fallback-slot-600',
      };

      final dto = AppointmentSummaryDto.fromJson(json);

      expect(dto.slotId, 'priority-slot-600');
    });

    test('sets slotId to null when neither scheduleSlotId nor slotId is present', () {
      final json = {
        'appointmentId': 'summary-4',
      };

      final dto = AppointmentSummaryDto.fromJson(json);

      expect(dto.slotId, isNull);
    });

    test('reads cancellationReason when present', () {
      final json = {
        'appointmentId': 'summary-5',
        'cancellationReason': 'Doi gio hen',
      };

      final dto = AppointmentSummaryDto.fromJson(json);

      expect(dto.cancelledReason, 'Doi gio hen');
    });

    test('falls back to cancelledReason when cancellationReason is missing', () {
      final json = {
        'appointmentId': 'summary-6',
        'cancelledReason': 'Nghi dot xuat',
      };

      final dto = AppointmentSummaryDto.fromJson(json);

      expect(dto.cancelledReason, 'Nghi dot xuat');
    });

    test('prefers cancellationReason over cancelledReason when both are present', () {
      final json = {
        'appointmentId': 'summary-7',
        'cancellationReason': 'Ly do moi tu BE',
        'cancelledReason': 'Ly do cu',
      };

      final dto = AppointmentSummaryDto.fromJson(json);

      expect(dto.cancelledReason, 'Ly do moi tu BE');
    });

    test('sets cancelledReason to null when neither cancellation field is present', () {
      final json = {
        'appointmentId': 'summary-8',
      };

      final dto = AppointmentSummaryDto.fromJson(json);

      expect(dto.cancelledReason, isNull);
    });
  });
}
