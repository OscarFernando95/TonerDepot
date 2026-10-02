import 'dart:typed_data';

import 'package:flutter/material.dart';

import '../models/evidence.dart';
import '../services/api_client.dart';
import '../services/evidence_api.dart';
import '../services/realtime_service.dart';
import '../theme/app_theme.dart';

/// Fotos de evidencia (antes / después) de un ticket u orden, solo para el staff.
class EvidenceGallery extends StatefulWidget {
  const EvidenceGallery({super.key, this.ticketId, this.orderId});

  final String? ticketId;
  final String? orderId;

  @override
  State<EvidenceGallery> createState() => _EvidenceGalleryState();
}

class _EvidenceGalleryState extends State<EvidenceGallery> {
  bool _loading = true;
  String? _error;
  final List<(EvidenceItem, Uint8List)> _photos = [];
  late final VoidCallback _unsubscribe;

  @override
  void initState() {
    super.initState();
    _load();
    // Una foto nueva del técnico aparece sola. No filtramos por ticket/orden porque el aviso no lleva ese
    // dato (solo el id de la evidencia); recargar la galería visible es barato.
    _unsubscribe = RealtimeService.instance.subscribe([
      'Evidence',
    ], (_) => _load());
  }

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  Future<void> _load() async {
    final api = EvidenceApi(ApiClient.instance);
    try {
      final items = await api.list(
        ticketId: widget.ticketId,
        orderId: widget.orderId,
      );
      final photos = await Future.wait(
        items.map((e) async => (e, await api.content(e.id))),
      );
      if (!mounted) return;
      setState(() {
        _photos
          ..clear()
          ..addAll(photos);
        _loading = false;
      });
    } catch (e, st) {
      debugPrint('EvidenceGallery load failed: $e\n$st');
      if (!mounted) return;
      setState(() {
        _error = e is ApiException
            ? e.message
            : 'No se pudo cargar la evidencia.';
        _loading = false;
      });
    }
  }

  void _open(Uint8List bytes) {
    showDialog<void>(
      context: context,
      builder: (dialogContext) => Dialog(
        insetPadding: const EdgeInsets.all(12),
        child: InteractiveViewer(child: Image.memory(bytes)),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Padding(
        padding: EdgeInsets.all(8),
        child: LinearProgressIndicator(),
      );
    }
    if (_error != null) {
      return Text(_error!, style: const TextStyle(color: AppColors.signalRed));
    }
    if (_photos.isEmpty) {
      return const Text(
        'Todavía no hay fotos de evidencia.',
        style: TextStyle(color: AppColors.inkSecondary),
      );
    }
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: [
        for (final (item, bytes) in _photos)
          GestureDetector(
            onTap: () => _open(bytes),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(8),
                  child: Image.memory(
                    bytes,
                    width: 140,
                    height: 105,
                    fit: BoxFit.cover,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  switch (item.kind) {
                    'Antes' => 'Antes',
                    'Contador' => 'Contador',
                    _ => 'Después',
                  },
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  }
}
