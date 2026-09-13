import '../entities/patient_relationship.dart';

/// Repository interface cho module Patient Relationship.
///
/// Tách interface ra domain layer để có thể mock trong test
/// và thay đổi implementation mà không ảnh hưởng UI.
abstract class PatientRelationshipRepository {
  /// Lấy danh sách người thân của user hiện tại.
  Future<List<PatientRelationship>> getRelatives();

  /// Thêm một người thân mới.
  ///
  /// [fullName]: Bắt buộc - Họ tên người thân
  /// [phone]: Bắt buộc - Số điện thoại người thân
  /// [dateOfBirth]: Tùy chọn - Ngày sinh
  /// [relationshipName]: Tùy chọn - Nhãn quan hệ (VD: "Mẹ", "Vợ", "Con gái")
  Future<PatientRelationship> addRelative({
    required String fullName,
    required String phone,
    DateTime? dateOfBirth,
    String? relationshipName,
  });

  /// Cập nhật thông tin người thân.
  Future<PatientRelationship> updateRelative(
    String id, {
    String? fullName,
    String? phone,
    DateTime? dateOfBirth,
    String? relationshipName,
  });

  /// Xóa một người thân khỏi danh bạ.
  Future<void> deleteRelative(String id);

  /// Kiểm tra xem số điện thoại đã được đăng ký tài khoản chưa.
  ///
  /// Trả về true nếu SĐT đã có account → không cho phép thêm làm người thân
  /// (vì người đó có thể tự đặt lịch).
  Future<bool> checkPhoneRegistered(String phone);
}
