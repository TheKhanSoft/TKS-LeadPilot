import 'package:flutter/material.dart';
import '../models/sync_models.dart';
import '../services/storage_service.dart';
import '../services/sync_service.dart';
import '../theme/app_theme.dart';

class SyncScreen extends StatefulWidget {
  final StorageService storage;
  final MobileSyncService syncService;

  const SyncScreen({super.key, required this.storage, required this.syncService});

  @override
  State<SyncScreen> createState() => _SyncScreenState();
}

class _SyncScreenState extends State<SyncScreen> {
  late TextEditingController _urlCtrl;
  late TextEditingController _tokenCtrl;
  SyncStatus? _status;
  bool _isLoading = false;
  String? _lastMessage;

  @override
  void initState() {
    super.initState();
    _urlCtrl = TextEditingController(text: widget.storage.getBaseUrl());
    _tokenCtrl = TextEditingController(text: widget.storage.getToken() ?? '');
    _checkStatus();
  }

  Future<void> _checkStatus() async {
    setState(() => _isLoading = true);
    final status = await widget.syncService.checkHealth();
    setState(() {
      _status = status;
      _isLoading = false;
    });
  }

  Future<void> _saveSettings() async {
    await widget.storage.setBaseUrl(_urlCtrl.text.trim());
    await widget.storage.setToken(_tokenCtrl.text.trim().isNotEmpty ? _tokenCtrl.text.trim() : null);
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('✅ Sync settings saved')),
      );
    }
    _checkStatus();
  }

  Future<void> _triggerSync() async {
    setState(() {
      _isLoading = true;
      _lastMessage = 'Synchronizing with central API...';
    });

    final res = await widget.syncService.synchronize();

    setState(() {
      _isLoading = false;
      _lastMessage = res.success
          ? '✅ Sync Complete! Pushed: ${res.pushedCount}, Pulled: ${res.pulledCount}'
          : '❌ Sync failed: ${res.errorMessage}';
    });
    _checkStatus();
  }

  @override
  Widget build(BuildContext context) {
    final deviceId = widget.storage.getDeviceId();
    final lastSync = widget.storage.getLastSyncedAt();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Central Synchronization'),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          // 1. Connection Status Card
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        'SERVER CONNECTION',
                        style: TextStyle(fontSize: 11, fontWeight: FontWeight.bold, color: AppTheme.textMuted),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: (_status?.isOnline ?? false)
                              ? AppTheme.success.withAlpha(30)
                              : AppTheme.danger.withAlpha(30),
                          borderRadius: BorderRadius.circular(6),
                          border: Border.all(
                            color: (_status?.isOnline ?? false) ? AppTheme.success : AppTheme.danger,
                          ),
                        ),
                        child: Text(
                          (_status?.isOnline ?? false) ? 'ONLINE' : 'UNREACHABLE',
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.bold,
                            color: (_status?.isOnline ?? false) ? AppTheme.success : AppTheme.danger,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Text(
                    _status?.serverMessage ?? 'Checking status...',
                    style: const TextStyle(fontSize: 13, color: AppTheme.textPrimary),
                  ),
                  if (_status?.serverVersion != null) ...[
                    const SizedBox(height: 4),
                    Text(
                      'API Version: v${_status!.serverVersion}',
                      style: const TextStyle(fontSize: 12, color: AppTheme.textSecondary),
                    ),
                  ],
                  const SizedBox(height: 8),
                  Text(
                    'Last Synced: ${lastSync != null ? lastSync.toLocal().toString().substring(0, 19) : 'Never'}',
                    style: const TextStyle(fontSize: 12, color: AppTheme.accent),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // 2. Settings Card
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'API ENDPOINT & CREDENTIALS',
                    style: TextStyle(fontSize: 11, fontWeight: FontWeight.bold, color: AppTheme.textMuted),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _urlCtrl,
                    decoration: const InputDecoration(
                      labelText: 'Central API URL',
                      hintText: 'http://10.0.2.2:8000/api/v1 (Android Emulator)',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _tokenCtrl,
                    obscureText: true,
                    decoration: const InputDecoration(
                      labelText: 'Sanctum Bearer Token (Optional for dev)',
                      hintText: '1|token...',
                    ),
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    'Mobile Device UUID:',
                    style: TextStyle(fontSize: 11, color: AppTheme.textMuted),
                  ),
                  const SizedBox(height: 2),
                  SelectableText(
                    deviceId,
                    style: const TextStyle(fontSize: 12, color: AppTheme.textSecondary, fontFamily: 'monospace'),
                  ),
                  const SizedBox(height: 16),
                  Row(
                    children: [
                      Expanded(
                        child: ElevatedButton(
                          onPressed: _saveSettings,
                          child: const Text('Save Settings'),
                        ),
                      ),
                      const SizedBox(width: 10),
                      IconButton.filledTonal(
                        icon: const Icon(Icons.refresh),
                        onPressed: _checkStatus,
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // 3. Sync Action
          Card(
            color: AppTheme.surfaceElevated,
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                children: [
                  SizedBox(
                    width: double.infinity,
                    height: 48,
                    child: ElevatedButton.icon(
                      icon: _isLoading
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                            )
                          : const Icon(Icons.sync),
                      label: const Text('Synchronize Now'),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppTheme.success,
                      ),
                      onPressed: _isLoading ? null : _triggerSync,
                    ),
                  ),
                  if (_lastMessage != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      _lastMessage!,
                      textAlign: TextAlign.center,
                      style: const TextStyle(fontSize: 12, color: AppTheme.accent),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
