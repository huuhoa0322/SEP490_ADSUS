import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/auth/presentation/views/home_screen.dart';
import '../../features/medication_reminder/presentation/views/medication_reminder_screen.dart';
import '../../features/auth/presentation/views/profile_screen.dart';
import '../../features/ai_chatbot/presentation/views/ai_chatbot_screen.dart';
import '../../features/medication_reminder/presentation/providers/medication_tab_provider.dart';
import 'providers/app_providers.dart';
import '../../core/theme/app_theme.dart';

/// Main shell chứa bottom navigation cố định.
///
/// Bottom-nav gồm 4 tab:
///   0 — Trang chủ  (HomeScreen)
///   1 — Thuốc      (MedicationReminderScreen)
///   2 — Chat Bot   (AiChatbotScreen)
///   3 — Cá nhân   (ProfileScreen)
class MainShell extends ConsumerStatefulWidget {
  const MainShell({super.key});

  @override
  ConsumerState<MainShell> createState() => _MainShellState();
}

class _MainShellState extends ConsumerState<MainShell> {
  int _currentIndex = 0;

  @override
  void initState() {
    super.initState();
    // Change 1: Listen widget open → tab Thuốc signal.
    // Khi user tap widget, MainActivity gọi MethodChannel "openMedicationTab"
    // → Flutter set initialMedicationTabProvider = true → listener đặt flag.
    // build() phát hiện flag → setState _currentIndex = 1 → reset flag.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.listen<bool>(initialMedicationTabProvider, (_, shouldOpen) {
        if (shouldOpen) {
          setState(() => _currentIndex = 1);
          // Reset provider sau khi đã set tab — tránh side-effect lần sau
          Future.microtask(() {
            ref.read(initialMedicationTabProvider.notifier).state = false;
          });
        }
      });
    });
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // T-3.3: Mỗi khi MainShell mount (app mở / quay lại từ background),
    // trigger widget sync để widget luôn có data mới nhất.
    ref.read(widgetSyncServiceProvider).triggerSync();
  }

  static const _navItems = [
    _NavItem(icon: Icons.home_outlined, activeIcon: Icons.home, label: 'Trang chủ'),
    _NavItem(icon: Icons.medication_outlined, activeIcon: Icons.medication, label: 'Thuốc'),
    _NavItem(icon: Icons.chat_bubble_outline, activeIcon: Icons.chat_bubble, label: 'Chat Bot'),
    _NavItem(icon: Icons.person_outline, activeIcon: Icons.person, label: 'Cá nhân'),
  ];

  Widget _buildBody(int index) {
    switch (index) {
      case 0:
        return const HomeScreen();
      case 1:
        return const MedicationReminderScreen();
      case 2:
        return const AiChatbotScreen();
      case 3:
        return const ProfileScreen();
      default:
        return const HomeScreen();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: _currentIndex,
        children: List.generate(_navItems.length, (i) => _buildBody(i)),
      ),
      bottomNavigationBar: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          border: Border(
            top: BorderSide(color: AppColors.border, width: 0.8),
          ),
        ),
        child: SafeArea(
          child: SizedBox(
            height: 64,
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceAround,
              children: List.generate(_navItems.length, (index) {
                final item = _navItems[index];
                final isActive = _currentIndex == index;
                return Expanded(
                  child: InkWell(
                    onTap: () => setState(() => _currentIndex = index),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(
                          isActive ? item.activeIcon : item.icon,
                          color: isActive ? AppColors.teal : AppColors.muted,
                          size: 24,
                        ),
                        const SizedBox(height: 4),
                        Text(
                          item.label,
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: isActive ? FontWeight.w600 : FontWeight.w400,
                            color: isActive ? AppColors.teal : AppColors.muted,
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              }),
            ),
          ),
        ),
      ),
    );
  }
}

class _NavItem {
  const _NavItem({
    required this.icon,
    required this.activeIcon,
    required this.label,
  });

  final IconData icon;
  final IconData activeIcon;
  final String label;
}
