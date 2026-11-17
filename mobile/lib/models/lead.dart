class Lead {
  final String id;
  String name;
  String? category;
  String? city;
  String? addressLine1;
  String? phonePrimary;
  String? emailPrimary;
  String? websiteUrl;
  int websiteStatus; // 0=NoWebsite, 1=Active, 2=Parked, 3=Error
  String pipelineStageId;
  int leadStatus;
  int leadScore;
  String? tags;
  bool isStarred;
  String? googleRating;
  bool isGoogleVerified;
  int syncVersion;
  bool isDeleted;
  DateTime updatedAtUtc;

  Lead({
    required this.id,
    required this.name,
    this.category,
    this.city,
    this.addressLine1,
    this.phonePrimary,
    this.emailPrimary,
    this.websiteUrl,
    this.websiteStatus = 0,
    this.pipelineStageId = 'stage-discovered',
    this.leadStatus = 0,
    this.leadScore = 0,
    this.tags,
    this.isStarred = false,
    this.googleRating,
    this.isGoogleVerified = false,
    this.syncVersion = 1,
    this.isDeleted = false,
    DateTime? updatedAtUtc,
  }) : updatedAtUtc = updatedAtUtc ?? DateTime.now().toUtc();

  factory Lead.fromJson(Map<String, dynamic> json) {
    return Lead(
      id: json['id'] as String,
      name: json['name'] as String? ?? 'Unknown',
      category: json['category'] as String?,
      city: json['city'] as String?,
      addressLine1: json['address_line1'] as String?,
      phonePrimary: json['phone_primary'] as String?,
      emailPrimary: json['email_primary'] as String?,
      websiteUrl: json['website_url'] as String?,
      websiteStatus: json['website_status'] as int? ?? 0,
      pipelineStageId: json['pipeline_stage_id'] as String? ?? 'stage-discovered',
      leadStatus: json['lead_status'] as int? ?? 0,
      leadScore: json['lead_score'] as int? ?? 0,
      tags: json['tags'] as String?,
      isStarred: json['is_starred'] as bool? ?? false,
      googleRating: json['google_rating']?.toString(),
      isGoogleVerified: json['is_google_verified'] as bool? ?? false,
      syncVersion: json['sync_version'] as int? ?? 1,
      isDeleted: json['is_deleted'] as bool? ?? false,
      updatedAtUtc: json['updated_at'] != null 
          ? DateTime.tryParse(json['updated_at'] as String)?.toUtc() ?? DateTime.now().toUtc()
          : DateTime.now().toUtc(),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'category': category,
      'city': city,
      'address_line1': addressLine1,
      'phone_primary': phonePrimary,
      'email_primary': emailPrimary,
      'website_url': websiteUrl,
      'website_status': websiteStatus,
      'pipeline_stage_id': pipelineStageId,
      'lead_status': leadStatus,
      'lead_score': leadScore,
      'tags': tags,
      'is_starred': isStarred,
      'is_google_verified': isGoogleVerified,
      'sync_version': syncVersion,
      'is_deleted': isDeleted,
      'updated_at': updatedAtUtc.toIso8601String(),
    };
  }
}
