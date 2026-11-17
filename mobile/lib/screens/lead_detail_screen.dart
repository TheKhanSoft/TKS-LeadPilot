import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:uuid/uuid.dart';
import '../models/lead.dart';
import '../models/contact.dart';
import '../models/task_note.dart';
import '../services/storage_service.dart';
import '../theme/app_theme.dart';

class LeadDetailScreen extends StatefulWidget {
  final Lead lead;
  final StorageService storage;

  const LeadDetailScreen({super.key, required this.lead, required this.storage});

  @override
  State<LeadDetailScreen> createState() => _LeadDetailScreenState();
}

class _LeadDetailScreenState extends State<LeadDetailScreen> {
  late Lead _lead;
  late List<Contact> _contacts;
  late List<SalesNote> _notes;
  final _noteController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _lead = widget.lead;
    _loadDetails();
  }

  void _loadDetails() {
    setState(() {
      _contacts = widget.storage.getContactsForBusiness(_lead.id);
      _notes = widget.storage.getNotesForBusiness(_lead.id);
    });
  }

  Future<void> _launchUri(String uriString) async {
    final uri = Uri.parse(uriString);
    try {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Could not open $uriString')),
        );
      }
    }
  }

  Future<void> _addNote() async {
    final text = _noteController.text.trim();
    if (text.isEmpty) return;

    final note = SalesNote(
      id: const Uuid().v4(),
      businessId: _lead.id,
      content: text,
      authorName: 'Mobile Sales Agent',
    );

    await widget.storage.addNote(note);
    _noteController.clear();
    _loadDetails();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(_lead.name),
        actions: [
          IconButton(
            icon: Icon(
              _lead.isStarred ? Icons.star : Icons.star_border,
              color: _lead.isStarred ? Colors.amber : Colors.grey,
            ),
            onPressed: () async {
              await widget.storage.toggleLeadStar(_lead.id);
              setState(() {
                _lead.isStarred = !_lead.isStarred;
              });
            },
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          // 1. Overview Card
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Expanded(
                        child: Text(
                          _lead.name,
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.bold,
                            color: AppTheme.textPrimary,
                          ),
                        ),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: AppTheme.success.withAlpha(40),
                          borderRadius: BorderRadius.circular(8),
                          border: Border.all(color: AppTheme.success),
                        ),
                        child: Text(
                          '${_lead.leadScore}/100',
                          style: const TextStyle(
                            color: AppTheme.success,
                            fontWeight: FontWeight.bold,
                            fontSize: 13,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Text(
                    _lead.category ?? 'Uncategorized',
                    style: const TextStyle(color: AppTheme.accent, fontSize: 13),
                  ),
                  if (_lead.city != null) ...[
                    const SizedBox(height: 4),
                    Text(
                      '📍 ${_lead.city}${_lead.addressLine1 != null ? ', ${_lead.addressLine1}' : ''}',
                      style: const TextStyle(color: AppTheme.textSecondary, fontSize: 12),
                    ),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),

          // 2. Quick Action Bar
          Row(
            children: [
              if (_lead.phonePrimary != null)
                Expanded(
                  child: ElevatedButton.icon(
                    icon: const Icon(Icons.phone, size: 16),
                    label: const Text('Call'),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppTheme.surfaceElevated,
                      foregroundColor: AppTheme.accent,
                    ),
                    onPressed: () => _launchUri('tel:${_lead.phonePrimary}'),
                  ),
                ),
              const SizedBox(width: 8),
              if (_lead.phonePrimary != null)
                Expanded(
                  child: ElevatedButton.icon(
                    icon: const Icon(Icons.chat, size: 16),
                    label: const Text('WhatsApp'),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppTheme.success.withAlpha(40),
                      foregroundColor: AppTheme.success,
                    ),
                    onPressed: () {
                      final clean = _lead.phonePrimary!.replaceAll(RegExp(r'[^\d]'), '');
                      _launchUri('https://wa.me/$clean?text=${Uri.encodeComponent('Hello from TheKhanSoft!')}');
                    },
                  ),
                ),
              const SizedBox(width: 8),
              if (_lead.emailPrimary != null)
                Expanded(
                  child: ElevatedButton.icon(
                    icon: const Icon(Icons.email, size: 16),
                    label: const Text('Email'),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppTheme.surfaceElevated,
                      foregroundColor: AppTheme.primary,
                    ),
                    onPressed: () => _launchUri('mailto:${_lead.emailPrimary}'),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 16),

          // 3. Diagnostics & Signals Card
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'WEBSITE & DIAGNOSTICS',
                    style: TextStyle(fontSize: 11, fontWeight: FontWeight.bold, color: AppTheme.textMuted),
                  ),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      Icon(
                        _lead.websiteUrl != null ? Icons.language : Icons.public_off,
                        color: _lead.websiteUrl != null ? AppTheme.accent : AppTheme.danger,
                        size: 18,
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          _lead.websiteUrl ?? 'No official website detected (High opportunity)',
                          style: TextStyle(
                            color: _lead.websiteUrl != null ? AppTheme.accent : AppTheme.danger,
                            fontSize: 13,
                          ),
                        ),
                      ),
                      if (_lead.websiteUrl != null)
                        IconButton(
                          icon: const Icon(Icons.open_in_new, size: 16),
                          onPressed: () => _launchUri(_lead.websiteUrl!),
                        ),
                    ],
                  ),
                  if (_lead.tags != null) ...[
                    const SizedBox(height: 8),
                    Wrap(
                      spacing: 6,
                      runSpacing: 4,
                      children: _lead.tags!.split(',').map((t) {
                        return Container(
                          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                          decoration: BoxDecoration(
                            color: AppTheme.surfaceElevated,
                            borderRadius: BorderRadius.circular(6),
                            border: Border.all(color: AppTheme.border),
                          ),
                          child: Text(
                            t.trim(),
                            style: const TextStyle(fontSize: 11, color: AppTheme.warning),
                          ),
                        );
                      }).toList(),
                    ),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // 4. Key Contacts
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'KEY CONTACTS',
                    style: TextStyle(fontSize: 11, fontWeight: FontWeight.bold, color: AppTheme.textMuted),
                  ),
                  const SizedBox(height: 10),
                  if (_contacts.isEmpty)
                    const Text('No individual contacts logged yet.', style: TextStyle(color: AppTheme.textMuted, fontSize: 12))
                  else
                    ..._contacts.map((c) => ListTile(
                          contentPadding: EdgeInsets.zero,
                          title: Text(c.fullName, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14)),
                          subtitle: Text('${c.jobTitle ?? 'Contact'} • ${c.phone ?? c.email ?? ''}', style: const TextStyle(color: AppTheme.textSecondary, fontSize: 12)),
                          trailing: c.phone != null
                              ? IconButton(
                                  icon: const Icon(Icons.phone, size: 18, color: AppTheme.accent),
                                  onPressed: () => _launchUri('tel:${c.phone}'),
                                )
                              : null,
                        )),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // 5. Activity Notes & Field Timeline
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'ACTIVITY NOTES & TIMELINE',
                    style: TextStyle(fontSize: 11, fontWeight: FontWeight.bold, color: AppTheme.textMuted),
                  ),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          controller: _noteController,
                          decoration: const InputDecoration(
                            hintText: 'Add field note, meeting result...',
                            isDense: true,
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      IconButton.filled(
                        icon: const Icon(Icons.send, size: 18),
                        onPressed: _addNote,
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  if (_notes.isEmpty)
                    const Text('No notes recorded yet.', style: TextStyle(color: AppTheme.textMuted, fontSize: 12))
                  else
                    ..._notes.map((n) => Padding(
                          padding: const EdgeInsets.only(bottom: 8),
                          child: Container(
                            padding: const EdgeInsets.all(10),
                            decoration: BoxDecoration(
                              color: AppTheme.surfaceElevated,
                              borderRadius: BorderRadius.circular(8),
                              border: Border.all(color: AppTheme.border),
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(n.content, style: const TextStyle(fontSize: 13, color: AppTheme.textPrimary)),
                                const SizedBox(height: 4),
                                Text(
                                  '${n.authorName} • ${n.createdAtUtc.toLocal().toString().substring(0, 16)}',
                                  style: const TextStyle(fontSize: 10, color: AppTheme.textMuted),
                                ),
                              ],
                            ),
                          ),
                        )),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
