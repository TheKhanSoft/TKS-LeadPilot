import 'package:flutter/material.dart';
import 'package:uuid/uuid.dart';
import '../models/lead.dart';
import '../services/storage_service.dart';
import '../services/sync_service.dart';
import '../theme/app_theme.dart';
import 'lead_detail_screen.dart';

class LeadsScreen extends StatefulWidget {
  final StorageService storage;
  final MobileSyncService syncService;

  const LeadsScreen({super.key, required this.storage, required this.syncService});

  @override
  State<LeadsScreen> createState() => _LeadsScreenState();
}

class _LeadsScreenState extends State<LeadsScreen> {
  late List<Lead> _allLeads;
  String _searchQuery = '';
  String _selectedCity = 'All';
  bool _onlyStarred = false;

  final List<String> _cities = ['All', 'Lahore', 'Karachi', 'Islamabad', 'Peshawar', 'Rawalpindi'];

  @override
  void initState() {
    super.initState();
    _loadLeads();
  }

  void _loadLeads() {
    setState(() {
      _allLeads = widget.storage.getLeads();
    });
  }

  List<Lead> get _filteredLeads {
    return _allLeads.where((lead) {
      if (_onlyStarred && !lead.isStarred) return false;
      if (_selectedCity != 'All' && lead.city != _selectedCity) return false;
      if (_searchQuery.isNotEmpty) {
        final q = _searchQuery.toLowerCase();
        final nameMatch = lead.name.toLowerCase().contains(q);
        final catMatch = (lead.category ?? '').toLowerCase().contains(q);
        final cityMatch = (lead.city ?? '').toLowerCase().contains(q);
        if (!nameMatch && !catMatch && !cityMatch) return false;
      }
      return true;
    }).toList();
  }

  Future<void> _handleRefresh() async {
    final res = await widget.syncService.synchronize();
    _loadLeads();
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(res.success
              ? '✅ Synced: Pushed ${res.pushedCount}, Pulled ${res.pulledCount}'
              : '⚠️ Sync notice: ${res.errorMessage ?? "Offline"}'),
          duration: const Duration(seconds: 2),
        ),
      );
    }
  }

  void _openAddLeadModal() {
    final nameCtrl = TextEditingController();
    final catCtrl = TextEditingController(text: 'Dentist & Dental Clinic');
    final cityCtrl = TextEditingController(text: 'Lahore');
    final phoneCtrl = TextEditingController();

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppTheme.surface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) {
        return Padding(
          padding: EdgeInsets.only(
            left: 20,
            right: 20,
            top: 20,
            bottom: MediaQuery.of(ctx).viewInsets.bottom + 20,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('Add Quick Lead', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
              const SizedBox(height: 14),
              TextField(controller: nameCtrl, decoration: const InputDecoration(labelText: 'Business Name')),
              const SizedBox(height: 10),
              TextField(controller: catCtrl, decoration: const InputDecoration(labelText: 'Category')),
              const SizedBox(height: 10),
              TextField(controller: cityCtrl, decoration: const InputDecoration(labelText: 'City')),
              const SizedBox(height: 10),
              TextField(controller: phoneCtrl, decoration: const InputDecoration(labelText: 'Primary Phone')),
              const SizedBox(height: 16),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: () async {
                    if (nameCtrl.text.trim().isEmpty) return;
                    final newLead = Lead(
                      id: const Uuid().v4(),
                      name: nameCtrl.text.trim(),
                      category: catCtrl.text.trim(),
                      city: cityCtrl.text.trim(),
                      phonePrimary: phoneCtrl.text.trim().isNotEmpty ? phoneCtrl.text.trim() : null,
                      leadScore: 65,
                    );
                    await widget.storage.addOrUpdateLead(newLead);
                    if (ctx.mounted) Navigator.pop(ctx);
                    _loadLeads();
                  },
                  child: const Text('Save Lead Offline'),
                ),
              ),
            ],
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final leads = _filteredLeads;

    return Scaffold(
      appBar: AppBar(
        title: const Text('TKS LeadPilot'),
        actions: [
          IconButton(
            icon: Icon(
              _onlyStarred ? Icons.star : Icons.star_border,
              color: _onlyStarred ? Colors.amber : Colors.grey,
            ),
            onPressed: () {
              setState(() {
                _onlyStarred = !_onlyStarred;
              });
            },
          ),
          IconButton(
            icon: const Icon(Icons.sync),
            onPressed: _handleRefresh,
          ),
        ],
      ),
      body: Column(
        children: [
          // 1. Search Box
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            child: TextField(
              decoration: InputDecoration(
                hintText: 'Search business name, category, city...',
                prefixIcon: const Icon(Icons.search, size: 20, color: AppTheme.textMuted),
                suffixIcon: _searchQuery.isNotEmpty
                    ? IconButton(
                        icon: const Icon(Icons.clear, size: 18),
                        onPressed: () => setState(() => _searchQuery = ''),
                      )
                    : null,
                contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              ),
              onChanged: (val) => setState(() => _searchQuery = val),
            ),
          ),

          // 2. City Filter Chips
          SizedBox(
            height: 40,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              itemCount: _cities.length,
              separatorBuilder: (_, _) => const SizedBox(width: 8),
              itemBuilder: (ctx, i) {
                final city = _cities[i];
                final isSelected = _selectedCity == city;
                return ChoiceChip(
                  label: Text(city),
                  selected: isSelected,
                  selectedColor: AppTheme.primary,
                  backgroundColor: AppTheme.surface,
                  labelStyle: TextStyle(
                    color: isSelected ? Colors.white : AppTheme.textSecondary,
                    fontSize: 12,
                    fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                  ),
                  onSelected: (selected) {
                    if (selected) setState(() => _selectedCity = city);
                  },
                );
              },
            ),
          ),
          const SizedBox(height: 8),

          // 3. Leads List
          Expanded(
            child: RefreshIndicator(
              onRefresh: _handleRefresh,
              child: leads.isEmpty
                  ? Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Icon(Icons.business_center_outlined, size: 48, color: AppTheme.textMuted),
                          const SizedBox(height: 12),
                          Text(
                            _allLeads.isEmpty ? 'No leads available offline.' : 'No matching leads found.',
                            style: const TextStyle(color: AppTheme.textSecondary),
                          ),
                        ],
                      ),
                    )
                  : ListView.builder(
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                      itemCount: leads.length,
                      itemBuilder: (ctx, idx) {
                        final lead = leads[idx];
                        return Card(
                          margin: const EdgeInsets.only(bottom: 10),
                          child: InkWell(
                            borderRadius: BorderRadius.circular(12),
                            onTap: () async {
                              await Navigator.push(
                                context,
                                MaterialPageRoute(
                                  builder: (_) => LeadDetailScreen(lead: lead, storage: widget.storage),
                                ),
                              );
                              _loadLeads();
                            },
                            child: Padding(
                              padding: const EdgeInsets.all(14),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Row(
                                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                    children: [
                                      Expanded(
                                        child: Text(
                                          lead.name,
                                          style: const TextStyle(
                                            fontSize: 15,
                                            fontWeight: FontWeight.bold,
                                            color: AppTheme.textPrimary,
                                          ),
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                        ),
                                      ),
                                      Container(
                                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                                        decoration: BoxDecoration(
                                          color: AppTheme.success.withAlpha(30),
                                          borderRadius: BorderRadius.circular(6),
                                          border: Border.all(color: AppTheme.success.withAlpha(100)),
                                        ),
                                        child: Text(
                                          '${lead.leadScore}',
                                          style: const TextStyle(
                                            color: AppTheme.success,
                                            fontWeight: FontWeight.bold,
                                            fontSize: 12,
                                          ),
                                        ),
                                      ),
                                    ],
                                  ),
                                  const SizedBox(height: 4),
                                  Text(
                                    lead.category ?? 'Uncategorized',
                                    style: const TextStyle(color: AppTheme.accent, fontSize: 12),
                                  ),
                                  const SizedBox(height: 6),
                                  Row(
                                    children: [
                                      if (lead.city != null) ...[
                                        const Icon(Icons.location_on, size: 13, color: AppTheme.textMuted),
                                        const SizedBox(width: 4),
                                        Text(lead.city!, style: const TextStyle(color: AppTheme.textMuted, fontSize: 11)),
                                        const SizedBox(width: 12),
                                      ],
                                      Icon(
                                        lead.websiteUrl != null ? Icons.language : Icons.public_off,
                                        size: 13,
                                        color: lead.websiteUrl != null ? AppTheme.accent : AppTheme.danger,
                                      ),
                                      const SizedBox(width: 4),
                                      Text(
                                        lead.websiteUrl != null ? 'Has Website' : 'No Website',
                                        style: TextStyle(
                                          color: lead.websiteUrl != null ? AppTheme.accent : AppTheme.danger,
                                          fontSize: 11,
                                        ),
                                      ),
                                      const Spacer(),
                                      GestureDetector(
                                        onTap: () async {
                                          await widget.storage.toggleLeadStar(lead.id);
                                          _loadLeads();
                                        },
                                        child: Icon(
                                          lead.isStarred ? Icons.star : Icons.star_border,
                                          color: lead.isStarred ? Colors.amber : Colors.grey,
                                          size: 18,
                                        ),
                                      ),
                                    ],
                                  ),
                                ],
                              ),
                            ),
                          ),
                        );
                      },
                    ),
            ),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton(
        backgroundColor: AppTheme.primary,
        foregroundColor: Colors.white,
        onPressed: _openAddLeadModal,
        child: const Icon(Icons.add),
      ),
    );
  }
}
