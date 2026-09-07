import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Provider dùng làm bridge khi widget tap → open Medication tab.
/// MainActivity gọi MethodChannel "openMedicationTab" → set state = true.
/// MainShell listen provider này → set _currentIndex = 1 → reset về false.
final initialMedicationTabProvider = StateProvider<bool>((ref) => false);
