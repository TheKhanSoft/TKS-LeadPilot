import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tks_leadpilot_mobile/models/lead.dart';
import 'package:tks_leadpilot_mobile/services/storage_service.dart';
import 'package:tks_leadpilot_mobile/services/sync_service.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('StorageService Tests', () {
    test('Initializes with default seed leads and contacts', () async {
      SharedPreferences.setMockInitialValues({});
      final storage = await StorageService.init();

      final leads = storage.getLeads();
      expect(leads.length, greaterThanOrEqualTo(3));

      final tasks = storage.getTasks();
      expect(tasks.length, greaterThanOrEqualTo(2));
    });

    test('Add and toggle lead star works offline', () async {
      SharedPreferences.setMockInitialValues({});
      final storage = await StorageService.init();

      final lead = Lead(
        id: 'new-lead-123',
        name: 'Khyber Medical Diagnostic',
        city: 'Peshawar',
        leadScore: 79,
      );

      await storage.addOrUpdateLead(lead);

      var leads = storage.getLeads();
      expect(leads.any((l) => l.id == 'new-lead-123'), isTrue);

      await storage.toggleLeadStar('new-lead-123');
      leads = storage.getLeads();
      final updated = leads.firstWhere((l) => l.id == 'new-lead-123');
      expect(updated.isStarred, isTrue);
    });
  });

  group('MobileSyncService Tests', () {
    test('checkHealth returns online status when API is reachable', () async {
      SharedPreferences.setMockInitialValues({});
      final storage = await StorageService.init();

      final mockClient = MockClient((request) async {
        expect(request.url.path, '/api/v1/health');
        return http.Response(
          jsonEncode({
            'status': 'healthy',
            'version': '1.0.0',
            'sync_protocol': 'v1-uuid-delta',
          }),
          200,
          headers: {'content-type': 'application/json'},
        );
      });

      final syncService = MobileSyncService(storage, mockClient);
      final status = await syncService.checkHealth();

      expect(status.isOnline, isTrue);
      expect(status.serverVersion, '1.0.0');
      expect(status.serverMessage, 'healthy');
    });

    test('synchronize successfully pushes and pulls delta records', () async {
      SharedPreferences.setMockInitialValues({});
      final storage = await StorageService.init();

      final mockClient = MockClient((request) async {
        if (request.method == 'POST' && request.url.path.contains('/push')) {
          return http.Response(
            jsonEncode({
              'success': true,
              'pushed_count': 3,
              'conflicts_count': 0,
            }),
            200,
            headers: {'content-type': 'application/json'},
          );
        } else if (request.method == 'GET' && request.url.path.contains('/pull')) {
          return http.Response(
            jsonEncode({
              'server_timestamp': '2026-11-17T12:00:00Z',
              'businesses': [
                {
                  'id': 'remote-biz-777',
                  'name': 'Lahore Dental Specialists',
                  'category': 'Healthcare',
                  'city': 'Lahore',
                  'lead_score': 90,
                  'is_deleted': false,
                }
              ],
            }),
            200,
            headers: {'content-type': 'application/json'},
          );
        }
        return http.Response('Not found', 404);
      });

      final syncService = MobileSyncService(storage, mockClient);
      final result = await syncService.synchronize();

      expect(result.success, isTrue);
      expect(result.pulledCount, 1);

      final leads = storage.getLeads();
      expect(leads.any((l) => l.id == 'remote-biz-777'), isTrue);
    });
  });
}
