import 'package:flutter/material.dart';

import '../../models/technician_visit.dart';
import '../../services/api_client.dart';
import '../../services/realtime_service.dart';
import '../../services/technician_management_api.dart';
import '../../theme/app_theme.dart';
import '../../utils/date_only.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';

/// Visitas de un técnico con la ubicación registrada al llegar y al cerrar frente a la sede. Solo se registra
/// y se alerta: el check-in nunca se bloquea por estar lejos.
class TechnicianVisitsScreen extends StatefulWidget {
  const TechnicianVisitsScreen({
    super.key,
    required this.technicianId,
    this.technicianName,
  });

  final String technicianId;
  final String? technicianName;

  @override
  State<TechnicianVisitsScreen> createState() => _TechnicianVisitsScreenState();
}

class _TechnicianVisitsScreenState extends State<TechnicianVisitsScreen> {
  bool _loading = true;
  String? _error;
  List<TechnicianVisit> _visits = [];
  late final VoidCallback _unsubscribe;

  @override
  void initState() {
    super.initState();
    _load();
    // Check-in/out del técnico desde otro dispositivo: recarga silenciosa (sin spinner).
    _unsubscribe = RealtimeService.instance.subscribe([
      'Visit',
    ], (_) => _load(silent: true));
  }

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  Future<void> _load({bool silent = false}) async {
    if (!silent) {
      setState(() {
        _loading = true;
        _error = null;
      });
    }
    try {
      final visits = await TechnicianManagementApi(ApiClient.instance)
          .listVisits(widget.technicianId);
      if (!mounted) return;
      setState(() => _visits = visits);
    } catch (e, st) {
      debugPrint('TechnicianVisitsScreen load failed: $e\n$st');
      if (mounted && !silent) {
        setState(
          () => _error = e is ApiException
              ? e.message
              : 'No se pudieron cargar las visitas.',
        );
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Color _statusColor(String? status) {
    switch (status) {
      case 'EnSitio':
        return AppColors.signalBlue;
      case 'FueraDeSitio':
        return AppColors.signalRed;
      case 'SinUbicacion':
        return AppColors.signalAmber;
      default:
        return AppColors.inkSecondary;
    }
  }

  Widget _locationRow(String label, String? status, double? distance) => Row(
    children: [
      SizedBox(
        width: 78,
        child: Text(
          label,
          style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
        ),
      ),
      Text(
        describeLocation(status, distance),
        style: TextStyle(
          color: _statusColor(status),
          fontWeight: FontWeight.w600,
          fontSize: 13,
        ),
      ),
    ],
  );

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(
          'Visitas${widget.technicianName == null ? '' : ' — ${widget.technicianName}'}',
        ),
      ),
      body: Builder(
        builder: (context) {
          if (_loading) return const Center(child: CircularProgressIndicator());
          if (_error != null) {
            return Center(
              child: Text(
                _error!,
                style: const TextStyle(color: AppColors.signalRed),
              ),
            );
          }
          if (_visits.isEmpty) {
            return const Center(
              child: Text(
                'Sin visitas registradas.',
                style: TextStyle(color: AppColors.inkSecondary),
              ),
            );
          }
          return RefreshIndicator(
            onRefresh: _load,
            child: ListView.builder(
              padding: const EdgeInsets.all(16),
              itemCount: _visits.length,
              itemBuilder: (context, index) {
                final visit = _visits[index];
                return ClayCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          // Color según la ubicación al llegar — el mismo
                          // criterio que ya usan las dos filas de abajo.
                          ClayIconBadge(
                            icon: Icons.location_on_outlined,
                            color: _statusColor(visit.checkInStatus),
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Text(
                              '${visit.kind} · ${formatDateTimeShort(visit.startTime)}'
                              '${visit.endTime == null ? ' (en curso)' : ' → ${formatDateTimeShort(visit.endTime!)}'}',
                              style: const TextStyle(
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 4),
                      _locationRow(
                        'Al llegar',
                        visit.checkInStatus,
                        visit.checkInDistanceMeters,
                      ),
                      _locationRow(
                        'Al cerrar',
                        visit.checkOutStatus,
                        visit.checkOutDistanceMeters,
                      ),
                    ],
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
