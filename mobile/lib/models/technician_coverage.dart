class TechnicianCoverage {
  final String id;
  final String cityId;
  final String cityName;

  TechnicianCoverage({required this.id, required this.cityId, required this.cityName});

  factory TechnicianCoverage.fromJson(Map<String, dynamic> json) => TechnicianCoverage(
        id: json['id'] as String,
        cityId: json['cityId'] as String,
        cityName: json['cityName'] as String,
      );
}
