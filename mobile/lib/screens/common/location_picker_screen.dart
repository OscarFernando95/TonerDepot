import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../services/device_capture.dart';
import '../../theme/app_theme.dart';

/// Selector de ubicación en mapa (OpenStreetMap: sin clave ni cuenta). Toca el mapa para fijar el punto; la
/// búsqueda de dirección usa Nominatim (un pedido por búsqueda del usuario). Devuelve un [LatLng] o null si se
/// cancela: `Navigator.push<LatLng>(...)`.
class LocationPickerScreen extends StatefulWidget {
  const LocationPickerScreen({super.key, this.initial, this.searchHint});

  final LatLng? initial;
  final String? searchHint;

  @override
  State<LocationPickerScreen> createState() => _LocationPickerScreenState();
}

class _SearchResult {
  const _SearchResult(this.label, this.point);

  final String label;
  final LatLng point;
}

class _LocationPickerScreenState extends State<LocationPickerScreen> {
  static const _colombiaCenter = LatLng(4.57, -74.3);

  final _mapController = MapController();
  late final TextEditingController _searchController;
  LatLng? _point;
  bool _searching = false;
  List<_SearchResult> _results = [];

  @override
  void initState() {
    super.initState();
    _point = widget.initial;
    _searchController = TextEditingController(text: widget.searchHint ?? '');
  }

  @override
  void dispose() {
    _searchController.dispose();
    _mapController.dispose();
    super.dispose();
  }

  void _setPoint(LatLng point, {double? zoom}) {
    setState(() {
      _point = LatLng(
        double.parse(point.latitude.toStringAsFixed(6)),
        double.parse(point.longitude.toStringAsFixed(6)),
      );
      _results = [];
    });
    if (zoom != null) _mapController.move(point, zoom);
  }

  Future<void> _search() async {
    final text = _searchController.text.trim();
    if (text.isEmpty) return;
    setState(() {
      _searching = true;
      _results = [];
    });
    try {
      final response = await Dio().get<List<dynamic>>(
        'https://nominatim.openstreetmap.org/search',
        queryParameters: {'format': 'jsonv2', 'limit': 5, 'countrycodes': 'co', 'q': text},
        // Nominatim exige identificar la aplicación.
        options: Options(headers: {'User-Agent': 'TonerApp/1.0 (com.tonerdepot.toner_tecnico)', 'Accept-Language': 'es'}),
      );
      final results = [
        for (final r in response.data ?? const [])
          _SearchResult(
            r['display_name'] as String,
            LatLng(double.parse(r['lat'] as String), double.parse(r['lon'] as String)),
          ),
      ];
      if (!mounted) return;
      setState(() => _results = results);
      if (results.isEmpty) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('No se encontró esa dirección. Prueba con menos detalle o marca el punto en el mapa.')),
        );
      }
    } catch (e, st) {
      debugPrint('LocationPickerScreen search failed: $e\n$st');
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('No se pudo buscar la dirección. Marca el punto directamente en el mapa.')),
        );
      }
    } finally {
      if (mounted) setState(() => _searching = false);
    }
  }

  Future<void> _useMyLocation() async {
    final fix = await DeviceCapture.currentPosition();
    if (!mounted) return;
    if (fix == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('No se pudo obtener la ubicación. Revisa el permiso.')),
      );
      return;
    }
    _setPoint(LatLng(fix.latitude, fix.longitude), zoom: 17);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Elegir ubicación')),
      body: Stack(
        children: [
          FlutterMap(
            mapController: _mapController,
            options: MapOptions(
              initialCenter: widget.initial ?? _colombiaCenter,
              initialZoom: widget.initial != null ? 17 : 6,
              onTap: (_, point) => _setPoint(point),
            ),
            children: [
              TileLayer(
                urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                userAgentPackageName: 'com.tonerdepot.toner_tecnico',
              ),
              if (_point != null)
                MarkerLayer(
                  markers: [
                    Marker(
                      point: _point!,
                      width: 44,
                      height: 44,
                      alignment: Alignment.topCenter,
                      child: const Icon(Icons.location_on, color: AppColors.signalRed, size: 44),
                    ),
                  ],
                ),
              const SimpleAttributionWidget(source: Text('© OpenStreetMap')),
            ],
          ),
          Positioned(
            top: 8,
            left: 8,
            right: 8,
            child: Material(
              elevation: 3,
              borderRadius: BorderRadius.circular(12),
              color: AppColors.claySurfaceRaised,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TextField(
                    controller: _searchController,
                    textInputAction: TextInputAction.search,
                    onSubmitted: (_) => _search(),
                    decoration: InputDecoration(
                      hintText: 'Buscar dirección (ej. Calle 5 #10-20, Pitalito)',
                      border: InputBorder.none,
                      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                      suffixIcon: _searching
                          ? const Padding(padding: EdgeInsets.all(12), child: SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2)))
                          : IconButton(icon: const Icon(Icons.search), onPressed: _search),
                    ),
                  ),
                  if (_results.isNotEmpty)
                    ConstrainedBox(
                      constraints: const BoxConstraints(maxHeight: 200),
                      child: ListView(
                        shrinkWrap: true,
                        children: [
                          for (final r in _results)
                            ListTile(
                              dense: true,
                              title: Text(r.label, maxLines: 2, overflow: TextOverflow.ellipsis),
                              onTap: () => _setPoint(r.point, zoom: 17),
                            ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          ),
          Positioned(
            right: 12,
            bottom: 96,
            child: FloatingActionButton.small(
              heroTag: 'my-location',
              onPressed: _useMyLocation,
              child: const Icon(Icons.my_location),
            ),
          ),
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: FilledButton(
            onPressed: _point == null ? null : () => Navigator.of(context).pop(_point),
            child: Text(_point == null ? 'Toca el mapa para marcar la sede' : 'Usar esta ubicación'),
          ),
        ),
      ),
    );
  }
}
