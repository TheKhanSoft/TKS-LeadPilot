import 'package:flutter/material.dart';
import 'screens/home_screen.dart';
import 'services/storage_service.dart';
import 'services/sync_service.dart';
import 'theme/app_theme.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final storage = await StorageService.init();
  final syncService = MobileSyncService(storage);

  runApp(LeadPilotMobileApp(storage: storage, syncService: syncService));
}

class LeadPilotMobileApp extends StatelessWidget {
  final StorageService storage;
  final MobileSyncService syncService;

  const LeadPilotMobileApp({
    super.key,
    required this.storage,
    required this.syncService,
  });

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'TKS LeadPilot',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.darkTheme,
      home: HomeScreen(storage: storage, syncService: syncService),
    );
  }
}
