import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tks_leadpilot_mobile/main.dart';
import 'package:tks_leadpilot_mobile/models/lead.dart';
import 'package:tks_leadpilot_mobile/services/storage_service.dart';
import 'package:tks_leadpilot_mobile/services/sync_service.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('Lead model serializes and deserializes correctly', () {
    final lead = Lead(
      id: 'test-uuid-001',
      name: 'Shifa International Care',
      category: 'Healthcare & Hospital',
      city: 'Islamabad',
      websiteUrl: 'https://shifa.pk',
      leadScore: 82,
      isStarred: true,
      syncVersion: 2,
    );

    final json = lead.toJson();
    expect(json['id'], 'test-uuid-001');
    expect(json['name'], 'Shifa International Care');
    expect(json['lead_score'], 82);

    final restored = Lead.fromJson(json);
    expect(restored.id, 'test-uuid-001');
    expect(restored.name, 'Shifa International Care');
    expect(restored.city, 'Islamabad');
    expect(restored.leadScore, 82);
    expect(restored.isStarred, true);
  });

  testWidgets('LeadPilotMobileApp smoke test renders navigation and leads', (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues({});
    final storage = await StorageService.init();
    final syncService = MobileSyncService(storage);

    await tester.pumpWidget(LeadPilotMobileApp(storage: storage, syncService: syncService));
    await tester.pumpAndSettle();

    // Verify Title and Bottom Navigation
    expect(find.text('TKS LeadPilot'), findsOneWidget);
    expect(find.text('Leads'), findsOneWidget);
    expect(find.text('Tasks'), findsOneWidget);
    expect(find.text('Sync'), findsOneWidget);

    // Verify Seed Leads Rendered
    expect(find.text('Apex Dental & Implant Care'), findsOneWidget);
    expect(find.text('Careway Pharmacy & Diagnostic'), findsOneWidget);
  });
}
