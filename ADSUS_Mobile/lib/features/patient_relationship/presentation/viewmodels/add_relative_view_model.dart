import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/network/api_exception.dart';
import '../../../../shared/providers/app_providers.dart';
import '../../domain/entities/patient_relationship.dart' show PatientRelationship;

/// State cho màn hình Thêm người thân.
class AddRelativeState {
  const AddRelativeState({
    this.fullName = '',
    this.phone = '',
    this.dateOfBirth,
    this.relationshipName,
    this.isPhoneChecked = false,
    this.isPhoneRegistered = false,
    this.isCheckingPhone = false,
    this.isSaving = false,
    this.errorMessage,
    this.savedRelative,
  });

  final String fullName;
  final String phone;
  final DateTime? dateOfBirth;
  final String? relationshipName;

  /// Đã kiểm tra SĐT chưa (để hiển thị cảnh báo nếu SĐT đã có account).
  final bool isPhoneChecked;
  final bool isPhoneRegistered;

  /// Đang kiểm tra SĐT.
  final bool isCheckingPhone;

  /// Đang lưu người thân mới.
  final bool isSaving;

  final String? errorMessage;

  /// Người thân vừa lưu thành công.
  final PatientRelationship? savedRelative;

  AddRelativeState copyWith({
    String? fullName,
    String? phone,
    DateTime? dateOfBirth,
    String? relationshipName,
    bool? isPhoneChecked,
    bool? isPhoneRegistered,
    bool? isCheckingPhone,
    bool? isSaving,
    String? errorMessage,
    PatientRelationship? savedRelative,
    bool clearDateOfBirth = false,
    bool clearRelationshipName = false,
    bool clearError = false,
    bool clearSaved = false,
    bool clearPhoneRegistered = false,
  }) {
    return AddRelativeState(
      fullName: fullName ?? this.fullName,
      phone: phone ?? this.phone,
      dateOfBirth: clearDateOfBirth ? null : (dateOfBirth ?? this.dateOfBirth),
      relationshipName: clearRelationshipName
          ? null
          : (relationshipName ?? this.relationshipName),
      isPhoneChecked: isPhoneChecked ?? this.isPhoneChecked,
      isPhoneRegistered:
          clearPhoneRegistered ? false : (isPhoneRegistered ?? this.isPhoneRegistered),
      isCheckingPhone: isCheckingPhone ?? this.isCheckingPhone,
      isSaving: isSaving ?? this.isSaving,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
      savedRelative:
          clearSaved ? null : (savedRelative ?? this.savedRelative),
    );
  }

  /// Kiểm tra form có hợp lệ để enable nút Lưu không.
  bool get isValid {
    return fullName.trim().isNotEmpty && phone.trim().isNotEmpty;
  }

  /// Kiểm tra SĐT có bị trùng với tài khoản đã đăng ký không.
  bool get canSave {
    return isValid && isPhoneChecked && !isPhoneRegistered;
  }
}

/// ViewModel cho màn hình AddRelativeScreen.
class AddRelativeViewModel extends Notifier<AddRelativeState> {
  @override
  AddRelativeState build() {
    return const AddRelativeState();
  }

  /// Cập nhật họ tên.
  void updateFullName(String value) {
    state = state.copyWith(fullName: value, clearError: true);
  }

  /// Cập nhật số điện thoại.
  void updatePhone(String value) {
    // Reset phone check khi user thay đổi SĐT
    state = state.copyWith(
      phone: value,
      isPhoneChecked: false,
      isPhoneRegistered: false,
      clearError: true,
    );
  }

  /// Cập nhật ngày sinh.
  void updateDateOfBirth(DateTime? date) {
    if (date == null) {
      state = state.copyWith(clearDateOfBirth: true);
    } else {
      state = state.copyWith(dateOfBirth: date);
    }
  }

  /// Cập nhật nhãn quan hệ.
  void updateRelationshipName(String? value) {
    if (value == null || value.isEmpty) {
      state = state.copyWith(clearRelationshipName: true);
    } else {
      state = state.copyWith(relationshipName: value);
    }
  }

  /// Kiểm tra xem SĐT đã được đăng ký tài khoản chưa.
  Future<void> checkPhone() async {
    final phone = state.phone.trim();
    if (phone.isEmpty) {
      state = state.copyWith(
        errorMessage: 'Vui lòng nhập số điện thoại.',
      );
      return;
    }

    state = state.copyWith(isCheckingPhone: true, clearError: true);

    try {
      final isRegistered = await ref
          .read(patientRelationshipRepositoryProvider)
          .checkPhoneRegistered(phone);

      state = state.copyWith(
        isCheckingPhone: false,
        isPhoneChecked: true,
        isPhoneRegistered: isRegistered,
      );
    } on ApiException catch (e) {
      state = state.copyWith(
        isCheckingPhone: false,
        isPhoneChecked: true,
        isPhoneRegistered: false,
        errorMessage: e.message,
      );
    } catch (e, st) {
      debugPrint('[DEBUG] AddRelativeViewModel.checkPhone error: $e\n$st');
      state = state.copyWith(
        isCheckingPhone: false,
        isPhoneChecked: true,
        isPhoneRegistered: false,
      );
    }
  }

  /// Lưu người thân mới.
  Future<bool> saveRelative() async {
    if (!state.isValid) {
      state = state.copyWith(
        errorMessage: 'Vui lòng nhập đầy đủ thông tin bắt buộc.',
      );
      return false;
    }

    // Kiểm tra SĐT nếu chưa kiểm tra
    if (!state.isPhoneChecked) {
      await checkPhone();
      if (state.isPhoneRegistered) {
        state = state.copyWith(
          errorMessage: 'Số điện thoại này đã được đăng ký tài khoản. '
              'Người đó có thể tự đặt lịch khám.',
        );
        return false;
      }
    }

    // Kiểm tra lại sau checkPhone
    if (state.isPhoneRegistered) {
      state = state.copyWith(
        errorMessage: 'Số điện thoại này đã được đăng ký tài khoản. '
            'Người đó có thể tự đặt lịch khám.',
      );
      return false;
    }

    state = state.copyWith(isSaving: true, clearError: true);

    try {
      final relative = await ref
          .read(patientRelationshipRepositoryProvider)
          .addRelative(
            fullName: state.fullName.trim(),
            phone: state.phone.trim(),
            dateOfBirth: state.dateOfBirth,
            relationshipName: state.relationshipName,
          );

      state = state.copyWith(
        isSaving: false,
        savedRelative: relative,
      );
      return true;
    } on ApiException catch (e) {
      state = state.copyWith(
        isSaving: false,
        errorMessage: e.message,
      );
      return false;
    } catch (e, st) {
      debugPrint('[DEBUG] AddRelativeViewModel.saveRelative error: $e\n$st');
      state = state.copyWith(
        isSaving: false,
        errorMessage: 'Thêm người thân thất bại.',
      );
      return false;
    }
  }

  /// Reset form.
  void reset() {
    state = const AddRelativeState();
  }
}

final addRelativeViewModelProvider =
    NotifierProvider<AddRelativeViewModel, AddRelativeState>(
  AddRelativeViewModel.new,
);
