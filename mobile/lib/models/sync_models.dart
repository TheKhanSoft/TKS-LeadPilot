class SyncStatus {
  final bool isOnline;
  final String? serverVersion;
  final String? serverMessage;
  final DateTime? lastSyncedAtUtc;
  final int pendingChangesCount;

  SyncStatus({
    required this.isOnline,
    this.serverVersion,
    this.serverMessage,
    this.lastSyncedAtUtc,
    this.pendingChangesCount = 0,
  });
}

class MobileSyncResult {
  final bool success;
  final int pushedCount;
  final int pulledCount;
  final String? errorMessage;
  final DateTime timestamp;

  MobileSyncResult({
    required this.success,
    this.pushedCount = 0,
    this.pulledCount = 0,
    this.errorMessage,
    DateTime? timestamp,
  }) : timestamp = timestamp ?? DateTime.now().toUtc();
}
