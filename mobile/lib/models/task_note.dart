class SalesTask {
  final String id;
  final String businessId;
  String title;
  DateTime? dueDateUtc;
  bool isCompleted;
  DateTime? completedAtUtc;

  SalesTask({
    required this.id,
    required this.businessId,
    required this.title,
    this.dueDateUtc,
    this.isCompleted = false,
    this.completedAtUtc,
  });

  factory SalesTask.fromJson(Map<String, dynamic> json) {
    return SalesTask(
      id: json['id'] as String,
      businessId: json['business_id'] as String? ?? '',
      title: json['title'] as String? ?? 'Untitled Task',
      dueDateUtc: json['due_date_utc'] != null
          ? DateTime.tryParse(json['due_date_utc'] as String)?.toUtc()
          : null,
      isCompleted: json['is_completed'] as bool? ?? false,
      completedAtUtc: json['completed_at_utc'] != null
          ? DateTime.tryParse(json['completed_at_utc'] as String)?.toUtc()
          : null,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'business_id': businessId,
      'title': title,
      'due_date_utc': dueDateUtc?.toIso8601String(),
      'is_completed': isCompleted,
      'completed_at_utc': completedAtUtc?.toIso8601String(),
    };
  }
}

class SalesNote {
  final String id;
  final String businessId;
  String content;
  String authorName;
  DateTime createdAtUtc;

  SalesNote({
    required this.id,
    required this.businessId,
    required this.content,
    this.authorName = 'Mobile Operator',
    DateTime? createdAtUtc,
  }) : createdAtUtc = createdAtUtc ?? DateTime.now().toUtc();

  factory SalesNote.fromJson(Map<String, dynamic> json) {
    return SalesNote(
      id: json['id'] as String,
      businessId: json['business_id'] as String? ?? '',
      content: json['content'] as String? ?? '',
      authorName: json['author_name'] as String? ?? 'Mobile Operator',
      createdAtUtc: json['created_at'] != null
          ? DateTime.tryParse(json['created_at'] as String)?.toUtc() ?? DateTime.now().toUtc()
          : DateTime.now().toUtc(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'business_id': businessId,
      'content': content,
      'author_name': authorName,
      'created_at': createdAtUtc.toIso8601String(),
    };
  }
}
