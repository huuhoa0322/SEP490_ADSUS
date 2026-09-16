import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

import 'package:adsus_mobile/features/auth/domain/entities/auth_session.dart';
import 'package:adsus_mobile/features/auth/domain/entities/user_profile.dart';
import 'package:adsus_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:adsus_mobile/features/auth/presentation/viewmodels/auth_view_model.dart';
import 'package:adsus_mobile/features/auth/presentation/views/profile_screen.dart';
import 'package:adsus_mobile/features/patient_relationship/domain/repositories/patient_relationship_repository.dart';
import 'package:adsus_mobile/features/patient_relationship/presentation/views/my_relatives_screen.dart';
import 'package:adsus_mobile/shared/providers/app_providers.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockPatientRelationshipRepository extends Mock
    implements PatientRelationshipRepository {}

class _FakeAuthViewModel extends StateNotifier<AuthState>
    implements AuthViewModel {
  _FakeAuthViewModel() : super(const AuthState(biometricAvailable: false));

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  late _MockAuthRepository mockAuthRepo;
  late _MockPatientRelationshipRepository mockRelationshipRepo;

  const testProfile = UserProfile(
    fullName: 'Nguyễn Văn A',
    phoneNumber: '0901234567',
    email: 'test@example.com',
    role: UserRole.patient,
    biometricEnabled: false,
  );

  setUp(() {
    mockAuthRepo = _MockAuthRepository();
    mockRelationshipRepo = _MockPatientRelationshipRepository();

    when(() => mockAuthRepo.getMyProfile()).thenAnswer((_) async => testProfile);
    when(() => mockRelationshipRepo.getRelatives()).thenAnswer((_) async => []);
  });

  Widget buildTestWidget() {
    return ProviderScope(
      overrides: [
        authRepositoryProvider.overrideWithValue(mockAuthRepo),
        patientRelationshipRepositoryProvider
            .overrideWithValue(mockRelationshipRepo),
        authViewModelProvider.overrideWith((ref) => _FakeAuthViewModel()),
      ],
      child: const MaterialApp(
        home: ProfileScreen(),
      ),
    );
  }

  group('ProfileScreen Widget Tests', () {
    testWidgets(
      'TC-PROF-01: renders "Quản lý người thân" ListTile with Icons.people_outline directly following "Đổi mật khẩu"',
      (tester) async {
        tester.view.physicalSize = const Size(1080, 2400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(() {
          tester.view.resetPhysicalSize();
          tester.view.resetDevicePixelRatio();
        });

        await tester.pumpWidget(buildTestWidget());
        await tester.pumpAndSettle();

        // Check Change Password exists
        expect(find.widgetWithText(ListTile, 'Đổi mật khẩu'), findsOneWidget);

        // Check Manage Relatives exists
        final relativesTileFinder =
            find.widgetWithText(ListTile, 'Quản lý người thân');
        expect(relativesTileFinder, findsOneWidget);

        // Check Icon is people_outline
        final listTileWidget = tester.widget<ListTile>(relativesTileFinder);
        final leadingIcon = listTileWidget.leading as Icon?;
        expect(leadingIcon?.icon, Icons.people_outline);
      },
    );

    testWidgets(
      'TC-PROF-02: tapping "Quản lý người thân" navigates to MyRelativesScreen',
      (tester) async {
        tester.view.physicalSize = const Size(1080, 2400);
        tester.view.devicePixelRatio = 1.0;
        addTearDown(() {
          tester.view.resetPhysicalSize();
          tester.view.resetDevicePixelRatio();
        });

        await tester.pumpWidget(buildTestWidget());
        await tester.pumpAndSettle();

        final relativesTileFinder =
            find.widgetWithText(ListTile, 'Quản lý người thân');
        await tester.ensureVisible(relativesTileFinder);
        await tester.tap(relativesTileFinder);
        await tester.pumpAndSettle();

        // Verify MyRelativesScreen is displayed
        expect(find.byType(MyRelativesScreen), findsOneWidget);
        expect(find.text('Danh bạ người thân'), findsOneWidget);
      },
    );
  });
}
