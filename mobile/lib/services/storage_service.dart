import 'dart:convert';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:uuid/uuid.dart';
import '../models/lead.dart';
import '../models/contact.dart';
import '../models/task_note.dart';

class StorageService {
  static const String _keyLeads = 'lp_leads_v1';
  static const String _keyContacts = 'lp_contacts_v1';
  static const String _keyTasks = 'lp_tasks_v1';
  static const String _keyNotes = 'lp_notes_v1';
  static const String _keyBaseUrl = 'lp_sync_base_url';
  static const String _keyToken = 'lp_sync_token';
  static const String _keyDeviceId = 'lp_sync_device_id';
  static const String _keyLastSync = 'lp_sync_last_at';

  final SharedPreferences _prefs;
  final _uuid = const Uuid();

  StorageService(this._prefs);

  static Future<StorageService> init() async {
    final prefs = await SharedPreferences.getInstance();
    final service = StorageService(prefs);
    await service._ensureSeededData();
    return service;
  }

  // --- Leads CRUD ---
  List<Lead> getLeads() {
    final raw = _prefs.getString(_keyLeads);
    if (raw == null || raw.isEmpty) return [];
    try {
      final List decoded = jsonDecode(raw);
      return decoded.map((e) => Lead.fromJson(e)).where((l) => !l.isDeleted).toList();
    } catch (_) {
      return [];
    }
  }

  Future<void> saveLeads(List<Lead> leads) async {
    final encoded = jsonEncode(leads.map((e) => e.toJson()).toList());
    await _prefs.setString(_keyLeads, encoded);
  }

  Future<void> addOrUpdateLead(Lead lead) async {
    final all = getLeads();
    final idx = all.indexWhere((l) => l.id == lead.id);
    lead.updatedAtUtc = DateTime.now().toUtc();
    if (idx >= 0) {
      all[idx] = lead;
    } else {
      all.insert(0, lead);
    }
    await saveLeads(all);
  }

  Future<void> toggleLeadStar(String leadId) async {
    final all = getLeads();
    final idx = all.indexWhere((l) => l.id == leadId);
    if (idx >= 0) {
      all[idx].isStarred = !all[idx].isStarred;
      all[idx].updatedAtUtc = DateTime.now().toUtc();
      await saveLeads(all);
    }
  }

  // --- Contacts ---
  List<Contact> getContactsForBusiness(String businessId) {
    final raw = _prefs.getString(_keyContacts);
    if (raw == null || raw.isEmpty) return [];
    try {
      final List decoded = jsonDecode(raw);
      return decoded
          .map((e) => Contact.fromJson(e))
          .where((c) => c.businessId == businessId)
          .toList();
    } catch (_) {
      return [];
    }
  }

  Future<void> addContact(Contact contact) async {
    final raw = _prefs.getString(_keyContacts);
    List<Contact> all = [];
    if (raw != null && raw.isNotEmpty) {
      try {
        final List decoded = jsonDecode(raw);
        all = decoded.map((e) => Contact.fromJson(e)).toList();
      } catch (_) {}
    }
    all.add(contact);
    await _prefs.setString(_keyContacts, jsonEncode(all.map((e) => e.toJson()).toList()));
  }

  // --- Tasks & Notes ---
  List<SalesTask> getTasks() {
    final raw = _prefs.getString(_keyTasks);
    if (raw == null || raw.isEmpty) return [];
    try {
      final List decoded = jsonDecode(raw);
      return decoded.map((e) => SalesTask.fromJson(e)).toList();
    } catch (_) {
      return [];
    }
  }

  Future<void> saveTasks(List<SalesTask> tasks) async {
    await _prefs.setString(_keyTasks, jsonEncode(tasks.map((e) => e.toJson()).toList()));
  }

  Future<void> toggleTaskCompleted(String taskId) async {
    final tasks = getTasks();
    final idx = tasks.indexWhere((t) => t.id == taskId);
    if (idx >= 0) {
      tasks[idx].isCompleted = !tasks[idx].isCompleted;
      tasks[idx].completedAtUtc = tasks[idx].isCompleted ? DateTime.now().toUtc() : null;
      await saveTasks(tasks);
    }
  }

  Future<void> addTask(SalesTask task) async {
    final tasks = getTasks();
    tasks.insert(0, task);
    await saveTasks(tasks);
  }

  List<SalesNote> getNotesForBusiness(String businessId) {
    final raw = _prefs.getString(_keyNotes);
    if (raw == null || raw.isEmpty) return [];
    try {
      final List decoded = jsonDecode(raw);
      return decoded
          .map((e) => SalesNote.fromJson(e))
          .where((n) => n.businessId == businessId)
          .toList();
    } catch (_) {
      return [];
    }
  }

  Future<void> addNote(SalesNote note) async {
    final raw = _prefs.getString(_keyNotes);
    List<SalesNote> all = [];
    if (raw != null && raw.isNotEmpty) {
      try {
        final List decoded = jsonDecode(raw);
        all = decoded.map((e) => SalesNote.fromJson(e)).toList();
      } catch (_) {}
    }
    all.insert(0, note);
    await _prefs.setString(_keyNotes, jsonEncode(all.map((e) => e.toJson()).toList()));
  }

  // --- Sync Preferences ---
  String getBaseUrl() => _prefs.getString(_keyBaseUrl) ?? 'http://10.0.2.2:8000/api/v1'; // 10.0.2.2 for Android emulator
  Future<void> setBaseUrl(String url) => _prefs.setString(_keyBaseUrl, url);

  String? getToken() => _prefs.getString(_keyToken);
  Future<void> setToken(String? token) => token != null ? _prefs.setString(_keyToken, token) : _prefs.remove(_keyToken);

  String getDeviceId() {
    var id = _prefs.getString(_keyDeviceId);
    if (id == null || id.isEmpty) {
      id = _uuid.v4();
      _prefs.setString(_keyDeviceId, id);
    }
    return id;
  }

  DateTime? getLastSyncedAt() {
    final str = _prefs.getString(_keyLastSync);
    return str != null ? DateTime.tryParse(str)?.toUtc() : null;
  }

  Future<void> setLastSyncedAt(DateTime timestamp) async {
    await _prefs.setString(_keyLastSync, timestamp.toIso8601String());
  }

  // --- Initial Seed Data ---
  Future<void> _ensureSeededData() async {
    if (_prefs.containsKey(_keyLeads)) return;

    final sampleLeads = [
      Lead(
        id: 'biz-apex-001',
        name: 'Apex Dental & Implant Care',
        category: 'Dentist & Dental Clinic',
        city: 'Lahore',
        addressLine1: 'Gulberg III, Main Boulevard',
        phonePrimary: '+92 42 35789012',
        emailPrimary: 'info@apexdental.pk',
        websiteUrl: 'http://apexdental.pk',
        websiteStatus: 1,
        leadScore: 88,
        tags: 'Dental, Needs SSL, High Opportunity',
        isStarred: true,
        isGoogleVerified: true,
        googleRating: '4.8',
      ),
      Lead(
        id: 'biz-careway-002',
        name: 'Careway Pharmacy & Diagnostic',
        category: 'Pharmacy & Healthcare',
        city: 'Karachi',
        addressLine1: 'Clifton Block 5',
        phonePrimary: '+92 21 35876543',
        emailPrimary: 'contact@careway.pk',
        websiteUrl: null,
        websiteStatus: 0,
        leadScore: 78,
        tags: 'No Website, Healthcare',
      ),
      Lead(
        id: 'biz-sunrise-003',
        name: 'Sunrise Grand Hotel',
        category: 'Hotels & Hospitality',
        city: 'Islamabad',
        addressLine1: 'F-7 Markaz',
        phonePrimary: '+92 51 2654321',
        emailPrimary: 'reservations@sunrisegrand.pk',
        websiteUrl: 'https://sunrisegrand.pk',
        websiteStatus: 1,
        leadScore: 48,
        tags: 'Hospitality, Moderate',
      ),
    ];

    final sampleContacts = [
      Contact(
        id: 'cnt-001',
        businessId: 'biz-apex-001',
        fullName: 'Dr. Tariq Mahmood',
        jobTitle: 'Principal Dentist',
        phone: '+92 300 1234567',
        email: 'dr.tariq@apexdental.pk',
        isPrimary: true,
      ),
      Contact(
        id: 'cnt-002',
        businessId: 'biz-careway-002',
        fullName: 'Zubair Qureshi',
        jobTitle: 'Managing Partner',
        phone: '+92 321 9876543',
        email: 'zubair@careway.pk',
        isPrimary: true,
      ),
    ];

    final sampleTasks = [
      SalesTask(
        id: 'tsk-001',
        businessId: 'biz-apex-001',
        title: 'Follow up on technical audit report via WhatsApp',
        dueDateUtc: DateTime.now().add(const Duration(days: 1)).toUtc(),
      ),
      SalesTask(
        id: 'tsk-002',
        businessId: 'biz-careway-002',
        title: 'Share website landing page mockup for pharmacy',
        dueDateUtc: DateTime.now().add(const Duration(days: 2)).toUtc(),
      ),
    ];

    await saveLeads(sampleLeads);
    await _prefs.setString(_keyContacts, jsonEncode(sampleContacts.map((c) => c.toJson()).toList()));
    await saveTasks(sampleTasks);
  }
}
