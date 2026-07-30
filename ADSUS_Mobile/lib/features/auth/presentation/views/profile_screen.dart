import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theme/app_theme.dart';
import '../viewmodels/auth_view_model.dart';
import '../viewmodels/profile_view_model.dart';
import 'widgets/message_banner.dart';

/// SCR-03 — hồ sơ cá nhân trên Mobile (UC-10), kèm bật/tắt sinh trắc học (UC-02).
///
/// Chỉ sửa được họ tên, email, ngày sinh. Số điện thoại hiển thị nhưng KHÔNG sửa được
/// (BR-02) — nó là định danh đăng nhập. Không có trường dữ liệu y tế nào ở đây (BR-03).
class ProfileScreen extends ConsumerStatefulWidget {
  const ProfileScreen({super.key});

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  final _fullNameController = TextEditingController();
  final _emailController = TextEditingController();
  DateTime? _dateOfBirth;
  bool _formReady = false;

  @override
  void initState() {
    super.initState();
    // Nạp hồ sơ sau khi khung dựng xong, tránh sửa provider ngay trong initState.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(profileViewModelProvider.notifier).load();
    });
  }

  @override
  void dispose() {
    _fullNameController.dispose();
    _emailController.dispose();
    super.dispose();
  }

  /// Đổ dữ liệu từ máy chủ vào form đúng MỘT lần, để không ghi đè thứ người dùng đang gõ.
  void _fillFormOnce(ProfileState state) {
    if (_formReady || state.profile == null) return;
    final p = state.profile!;
    _fullNameController.text = p.fullName;
    _emailController.text = p.email ?? '';
    if (p.dateOfBirth != null) _dateOfBirth = DateTime.tryParse(p.dateOfBirth!);
    _formReady = true;
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _dateOfBirth ?? DateTime(now.year - 30),
      firstDate: DateTime(1900),
      // BR-01: ngày sinh không được ở tương lai — chặn ngay ở bộ chọn ngày.
      lastDate: now,
    );
    if (picked != null) setState(() => _dateOfBirth = picked);
  }

  String? get _dateOfBirthText => _dateOfBirth == null
      ? null
      : '${_dateOfBirth!.year.toString().padLeft(4, '0')}-'
          '${_dateOfBirth!.month.toString().padLeft(2, '0')}-'
          '${_dateOfBirth!.day.toString().padLeft(2, '0')}';

  Future<void> _save() async {
    if (_fullNameController.text.trim().isEmpty) return;
    await ref.read(profileViewModelProvider.notifier).save(
          fullName: _fullNameController.text,
          email: _emailController.text,
          dateOfBirth: _dateOfBirthText,
        );
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(profileViewModelProvider);
    _fillFormOnce(state);

    if (state.isLoading && state.profile == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Hồ sơ cá nhân')),
        body: const Center(child: CircularProgressIndicator()),
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: const Text('Hồ sơ cá nhân'),
        actions: [
          IconButton(
            tooltip: 'Đăng xuất',
            icon: const Icon(Icons.logout),
            onPressed: () => ref.read(authViewModelProvider.notifier).signOut(),
          ),
        ],
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 16, 24, 32),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _label('HỌ VÀ TÊN'),
              TextField(
                controller: _fullNameController,
                enabled: !state.isSaving,
                decoration: const InputDecoration(
                  prefixIcon: Icon(Icons.person_outline),
                ),
              ),
              const SizedBox(height: 18),

              // BR-02 / AF-02: số điện thoại chỉ đọc, kèm lời nhắc liên hệ phòng khám.
              _label('SỐ ĐIỆN THOẠI'),
              TextField(
                enabled: false,
                controller: TextEditingController(
                    text: state.profile?.phoneNumber ?? ''),
                decoration: const InputDecoration(
                  prefixIcon: Icon(Icons.phone_outlined),
                  suffixIcon: Icon(Icons.lock_outline, size: 18),
                ),
              ),
              const Padding(
                padding: EdgeInsets.only(left: 20, top: 6),
                child: Text(
                  'Số điện thoại là tài khoản đăng nhập. Cần đổi vui lòng liên hệ phòng khám.',
                  style: TextStyle(fontSize: 12, color: AppColors.muted),
                ),
              ),
              const SizedBox(height: 18),

              _label('EMAIL'),
              TextField(
                controller: _emailController,
                enabled: !state.isSaving,
                keyboardType: TextInputType.emailAddress,
                decoration: const InputDecoration(
                  hintText: 'Không bắt buộc',
                  prefixIcon: Icon(Icons.mail_outline),
                ),
              ),
              const SizedBox(height: 18),

              _label('NGÀY SINH'),
              InkWell(
                onTap: state.isSaving ? null : _pickDate,
                borderRadius: BorderRadius.circular(28),
                child: InputDecorator(
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.calendar_today_outlined),
                  ),
                  child: Text(
                    _dateOfBirthText ?? 'Không bắt buộc',
                    style: TextStyle(
                      fontSize: 16,
                      color: _dateOfBirth == null ? AppColors.muted : Colors.black87,
                    ),
                  ),
                ),
              ),

              if (state.errorMessage != null) ...[
                const SizedBox(height: 18),
                MessageBanner(message: state.errorMessage!),
              ],
              if (state.successMessage != null) ...[
                const SizedBox(height: 18),
                MessageBanner(message: state.successMessage!, isError: false),
              ],

              const SizedBox(height: 24),
              ElevatedButton(
                onPressed: state.isSaving ? null : _save,
                child: state.isSaving
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(
                            strokeWidth: 2, color: Colors.white),
                      )
                    : const Text('LƯU THAY ĐỔI'),
              ),

              const SizedBox(height: 32),
              const Divider(),
              const SizedBox(height: 12),
              _buildBiometricSection(state),
            ],
          ),
        ),
      ),
    );
  }

  /// UC-02 — bật/tắt đăng nhập sinh trắc học.
  Widget _buildBiometricSection(ProfileState state) {
    final auth = ref.watch(authViewModelProvider);
    final enabled = state.profile?.biometricEnabled ?? false;

    if (!auth.biometricAvailable) {
      return const Row(
        children: [
          Icon(Icons.fingerprint, color: AppColors.muted),
          SizedBox(width: 12),
          Expanded(
            child: Text(
              'Máy này chưa đăng ký vân tay hoặc khuôn mặt nào, '
              'nên chưa bật được đăng nhập sinh trắc học.',
              style: TextStyle(fontSize: 13, color: AppColors.muted, height: 1.4),
            ),
          ),
        ],
      );
    }

    return SwitchListTile.adaptive(
      contentPadding: EdgeInsets.zero,
      secondary: const Icon(Icons.fingerprint, color: AppColors.navy),
      title: const Text(
        'Đăng nhập bằng vân tay',
        style: TextStyle(fontWeight: FontWeight.w600),
      ),
      subtitle: const Text(
        'Mẫu vân tay được điện thoại giữ, hệ thống không bao giờ lưu.',
        style: TextStyle(fontSize: 12, height: 1.4),
      ),
      value: enabled,
      onChanged: state.isSaving
          ? null
          : (value) async {
              final ok = await ref
                  .read(profileViewModelProvider.notifier)
                  .setBiometric(value);
              if (ok) {
                await ref.read(authViewModelProvider.notifier).refreshBiometricStatus();
              }
            },
    );
  }

  Widget _label(String text) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Text(
          text,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w600,
            letterSpacing: 1.1,
            color: AppColors.navy,
          ),
        ),
      );
}
