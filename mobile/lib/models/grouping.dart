/// Agrupación jerárquica ciudad → cliente → contrato, genérica sobre el tipo
/// de item — espejo de `groupedByCity` en AssetsListView.vue /
/// MaintenanceSchedulesView.vue (misma forma de árbol en ambas). Cada
/// pantalla resuelve sus propias reglas de fallback (ej. "Sin ciudad",
/// "Bodega — Oficina principal" para Activos) ANTES de llamar a
/// [groupByCityClientContract] — el helper solo arma el árbol y ordena.
class ContractGroup<T> {
  final String contractLabel;
  final List<T> items;

  ContractGroup({required this.contractLabel, required this.items});
}

class ClientGroup<T> {
  final String clientLabel;
  final List<ContractGroup<T>> contractGroups;

  ClientGroup({required this.clientLabel, required this.contractGroups});

  int get count => contractGroups.fold(0, (n, c) => n + c.items.length);
}

class CityGroup<T> {
  final String city;
  final List<ClientGroup<T>> clientGroups;

  CityGroup({required this.city, required this.clientGroups});

  int get count => clientGroups.fold(0, (n, c) => n + c.count);
}

List<CityGroup<T>> groupByCityClientContract<T>(
  List<T> items, {
  required String Function(T) city,
  required String Function(T) clientKey,
  required String Function(T) clientLabel,
  required String Function(T) contractKey,
  required String Function(T) contractLabel,
}) {
  final byCity = <String, Map<String, Map<String, List<T>>>>{};
  final clientLabels = <String, String>{};
  final contractLabels = <String, String>{};

  for (final item in items) {
    final c = city(item);
    final ck = clientKey(item);
    final crk = contractKey(item);
    clientLabels[ck] = clientLabel(item);
    contractLabels[crk] = contractLabel(item);

    final byClient = byCity.putIfAbsent(c, () => {});
    final byContract = byClient.putIfAbsent(ck, () => {});
    byContract.putIfAbsent(crk, () => []).add(item);
  }

  final cityGroups = byCity.entries.map((cityEntry) {
    final clientGroups = cityEntry.value.entries.map((clientEntry) {
      final contractGroups = clientEntry.value.entries.map((contractEntry) {
        return ContractGroup<T>(
          contractLabel: contractLabels[contractEntry.key] ?? contractEntry.key,
          items: contractEntry.value,
        );
      }).toList()
        ..sort((a, b) => a.contractLabel.compareTo(b.contractLabel));
      return ClientGroup<T>(
        clientLabel: clientLabels[clientEntry.key] ?? clientEntry.key,
        contractGroups: contractGroups,
      );
    }).toList()
      ..sort((a, b) => a.clientLabel.compareTo(b.clientLabel));
    return CityGroup<T>(city: cityEntry.key, clientGroups: clientGroups);
  }).toList()
    ..sort((a, b) => a.city.compareTo(b.city));

  return cityGroups;
}
