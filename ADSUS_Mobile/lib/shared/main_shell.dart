import 'package:flutter/material.dart';

import '../../features/auth/presentation/views/home_screen.dart';
import '../../features/medication_reminder/presentation/views/medication_reminder_screen.dart';
import '../../features/auth/presentation/views/profile_screen.dart';
import '../../features/ai_chatbot/presentation/views/ai_chatbot_screen.dart';
import '../../core/theme/app_theme.dart';

/// Main shell chứa bottom navigation cố định.
///
/// Bottom-nav gồm 4 tab:
///   0 — Trang chủ  (HomeScreen)
///   1 — Thuốc      (MedicationReminderScreen)
///   2 — Chat Bot   (AiChatbotScreen)
///   3 — Cá nhân   (ProfileScreen)
class MainShell extends StatefulWidget {
  const MainShell({super.key});

  @override
  State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
  int _currentIndex = 0;

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
