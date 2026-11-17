import 'package:flutter/material.dart';
import 'package:uuid/uuid.dart';
import '../models/task_note.dart';
import '../services/storage_service.dart';
import '../theme/app_theme.dart';

class TasksScreen extends StatefulWidget {
  final StorageService storage;

  const TasksScreen({super.key, required this.storage});

  @override
  State<TasksScreen> createState() => _TasksScreenState();
}

class _TasksScreenState extends State<TasksScreen> {
  late List<SalesTask> _tasks;

  @override
  void initState() {
    super.initState();
    _loadTasks();
  }

  void _loadTasks() {
    setState(() {
      _tasks = widget.storage.getTasks();
    });
  }

  void _openAddTaskModal() {
    final titleCtrl = TextEditingController();

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
              const Text('Add Sales Follow-up Task', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
              const SizedBox(height: 14),
              TextField(
                controller: titleCtrl,
                decoration: const InputDecoration(labelText: 'Task Description / Reminder'),
              ),
              const SizedBox(height: 16),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: () async {
                    if (titleCtrl.text.trim().isEmpty) return;
                    final newTask = SalesTask(
                      id: const Uuid().v4(),
                      businessId: 'general',
                      title: titleCtrl.text.trim(),
                      dueDateUtc: DateTime.now().add(const Duration(days: 1)).toUtc(),
                    );
                    await widget.storage.addTask(newTask);
                    if (ctx.mounted) Navigator.pop(ctx);
                    _loadTasks();
                  },
                  child: const Text('Save Task'),
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
    return Scaffold(
      appBar: AppBar(
        title: const Text('Sales Tasks & Follow-ups'),
      ),
      body: _tasks.isEmpty
          ? const Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.check_circle_outline, size: 48, color: AppTheme.textMuted),
                  SizedBox(height: 12),
                  Text('All sales follow-ups completed!', style: TextStyle(color: AppTheme.textSecondary)),
                ],
              ),
            )
          : ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: _tasks.length,
              itemBuilder: (ctx, idx) {
                final task = _tasks[idx];
                return Card(
                  margin: const EdgeInsets.only(bottom: 10),
                  child: CheckboxListTile(
                    value: task.isCompleted,
                    activeColor: AppTheme.success,
                    contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
                    title: Text(
                      task.title,
                      style: TextStyle(
                        fontSize: 14,
                        decoration: task.isCompleted ? TextDecoration.lineThrough : null,
                        color: task.isCompleted ? AppTheme.textMuted : AppTheme.textPrimary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    subtitle: task.dueDateUtc != null
                        ? Text(
                            'Due: ${task.dueDateUtc!.toLocal().toString().substring(0, 10)}',
                            style: const TextStyle(fontSize: 11, color: AppTheme.textMuted),
                          )
                        : null,
                    onChanged: (_) async {
                      await widget.storage.toggleTaskCompleted(task.id);
                      _loadTasks();
                    },
                  ),
                );
              },
            ),
      floatingActionButton: FloatingActionButton(
        backgroundColor: AppTheme.primary,
        foregroundColor: Colors.white,
        onPressed: _openAddTaskModal,
        child: const Icon(Icons.add_task),
      ),
    );
  }
}
