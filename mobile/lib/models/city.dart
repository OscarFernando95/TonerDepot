class City {
  final String id;
  final String name;
  final String stateOrProvince;

  City({required this.id, required this.name, required this.stateOrProvince});

  factory City.fromJson(Map<String, dynamic> json) => City(
        id: json['id'] as String,
        name: json['name'] as String,
        stateOrProvince: json['stateOrProvince'] as String,
      );
}
