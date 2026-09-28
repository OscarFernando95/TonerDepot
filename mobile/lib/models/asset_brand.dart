class AssetBrand {
  final String id;
  final String name;

  AssetBrand({required this.id, required this.name});

  factory AssetBrand.fromJson(Map<String, dynamic> json) =>
      AssetBrand(id: json['id'] as String, name: json['name'] as String);
}
