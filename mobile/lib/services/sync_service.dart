import 'dart:convert';
import 'package:http/http.dart' as http;
import '../models/lead.dart';
import '../models/sync_models.dart';
import 'storage_service.dart';

class MobileSyncService {
  final StorageService _storage;
  final http.Client _client;

  MobileSyncService(this._storage, [http.Client? client])
      : _client = client ?? http.Client();

  Future<SyncStatus> checkHealth() async {
    final baseUrl = _storage.getBaseUrl().replaceAll(RegExp(r'/+$'), '');
    final lastSync = _storage.getLastSyncedAt();
    final leads = _storage.getLeads();
    final pendingCount = lastSync != null 
        ? leads.where((l) => l.updatedAtUtc.isAfter(lastSync)).length 
        : leads.length;

    try {
      final res = await _client
          .get(Uri.parse('$baseUrl/health'))
          .timeout(const Duration(seconds: 5));

      if (res.statusCode == 200) {
        final data = jsonDecode(res.body);
        return SyncStatus(
          isOnline: true,
          serverVersion: data['version'] as String?,
          serverMessage: data['status'] as String?,
          lastSyncedAtUtc: lastSync,
          pendingChangesCount: pendingCount,
        );
      }
    } catch (_) {}

    return SyncStatus(
      isOnline: false,
      serverMessage: 'Server unreachable',
      lastSyncedAtUtc: lastSync,
      pendingChangesCount: pendingCount,
    );
  }

  Future<MobileSyncResult> synchronize() async {
    final baseUrl = _storage.getBaseUrl().replaceAll(RegExp(r'/+$'), '');
    final token = _storage.getToken();
    final deviceId = _storage.getDeviceId();
    final lastSync = _storage.getLastSyncedAt();

    final headers = {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      if (token != null && token.isNotEmpty) 'Authorization': 'Bearer $token',
    };

    int pushed = 0;
    int pulled = 0;

    try {
      // 1. Push Phase
      final allLeads = _storage.getLeads();
      final toPush = lastSync != null
          ? allLeads.where((l) => l.updatedAtUtc.isAfter(lastSync)).toList()
          : allLeads;

      if (toPush.isNotEmpty) {
        final pushUrl = token != null && token.isNotEmpty
            ? '$baseUrl/sync/push'
            : '$baseUrl/dev-sync/push';

        final payload = {
          'device_id': deviceId,
          'businesses': toPush.map((l) => l.toJson()).toList(),
        };

        final pushRes = await _client
            .post(Uri.parse(pushUrl), headers: headers, body: jsonEncode(payload))
            .timeout(const Duration(seconds: 10));

        if (pushRes.statusCode == 200) {
          final pushData = jsonDecode(pushRes.body);
          pushed = pushData['pushed_count'] as int? ?? toPush.length;
        } else {
          return MobileSyncResult(
            success: false,
            errorMessage: 'Push failed: HTTP ${pushRes.statusCode}',
          );
        }
      }

      // 2. Pull Phase
      var pullUrl = token != null && token.isNotEmpty
          ? '$baseUrl/sync/pull'
          : '$baseUrl/dev-sync/pull';

      if (lastSync != null) {
        pullUrl += '?since=${Uri.encodeComponent(lastSync.toIso8601String())}';
      }

      final pullRes = await _client
          .get(Uri.parse(pullUrl), headers: headers)
          .timeout(const Duration(seconds: 10));

      if (pullRes.statusCode == 200) {
        final pullData = jsonDecode(pullRes.body);
        final remoteBusinesses = pullData['businesses'] as List? ?? [];
        pulled = remoteBusinesses.length;

        // Apply remote businesses to local storage
        final currentLeads = _storage.getLeads();
        for (final item in remoteBusinesses) {
          final isDel = item['is_deleted'] as bool? ?? false;
          final remoteLead = Lead.fromJson(item);
          final idx = currentLeads.indexWhere((l) => l.id == remoteLead.id);

          if (isDel) {
            if (idx >= 0) {
              currentLeads.removeAt(idx);
            }
          } else {
            if (idx >= 0) {
              currentLeads[idx] = remoteLead;
            } else {
              currentLeads.insert(0, remoteLead);
            }
          }
        }
        await _storage.saveLeads(currentLeads);

        // Update sync cursor
        final tsStr = pullData['server_timestamp'] as String?;
        if (tsStr != null) {
          final sTime = DateTime.tryParse(tsStr)?.toUtc() ?? DateTime.now().toUtc();
          await _storage.setLastSyncedAt(sTime);
        }
      } else {
        return MobileSyncResult(
          success: false,
          pushedCount: pushed,
          errorMessage: 'Pull failed: HTTP ${pullRes.statusCode}',
        );
      }

      return MobileSyncResult(
        success: true,
        pushedCount: pushed,
        pulledCount: pulled,
      );
    } catch (e) {
      return MobileSyncResult(
        success: false,
        pushedCount: pushed,
        errorMessage: e.toString(),
      );
    }
  }
}
