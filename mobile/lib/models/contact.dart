class Contact {
  final String id;
  final String businessId;
  String fullName;
  String? jobTitle;
  String? phone;
  String? email;
  bool isPrimary;

  Contact({
    required this.id,
    required this.businessId,
    required this.fullName,
    this.jobTitle,
    this.phone,
    this.email,
    this.isPrimary = false,
  });

  factory Contact.fromJson(Map<String, dynamic> json) {
    return Contact(
      id: json['id'] as String,
      businessId: json['business_id'] as String? ?? '',
      fullName: json['full_name'] as String? ?? 'Unknown Contact',
      jobTitle: json['job_title'] as String?,
      phone: json['phone'] as String?,
      email: json['email'] as String?,
      isPrimary: json['is_primary'] as bool? ?? false,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'business_id': businessId,
      'full_name': fullName,
      'job_title': jobTitle,
      'phone': phone,
      'email': email,
      'is_primary': isPrimary,
    };
  }
}
