import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

/// Espejo de ServiceTicketStatusLabels / MaintenanceOrderStatusLabels en
/// frontend-web/src/api/types.ts — mismos textos en español. Los colores
/// vienen de la paleta del tablero (AppColors, ver DESIGN.md): azul para
/// "asignado/en curso", ámbar para "en proceso", rojo para "cancelado",
/// gris-seam para estados inactivos/cerrados.
class StatusLabels {
  StatusLabels._();

  static const Map<String, String> ticket = {
    'Abierto': 'Abierto',
    'SinAsignar': 'Sin asignar',
    'Asignado': 'Asignado',
    'EnProceso': 'En proceso',
    'Resuelto': 'Resuelto',
    'Cerrado': 'Cerrado',
    'Cancelado': 'Cancelado',
  };

  static const Map<String, String> order = {
    'Pendiente': 'Pendiente',
    'Asignada': 'Asignada',
    'EnProceso': 'En proceso',
    'Completada': 'Completada',
    'Cancelada': 'Cancelada',
  };

  static const Map<String, String> priority = {
    'Baja': 'Baja',
    'Media': 'Media',
    'Alta': 'Alta',
    'Critica': 'Crítica',
  };

  static Color colorFor(String status) {
    switch (status) {
      case 'Abierto':
      case 'SinAsignar':
      case 'Pendiente':
        return AppColors.boardSeam;
      case 'Asignado':
      case 'Asignada':
        return AppColors.signalBlueBright;
      case 'EnProceso':
        return AppColors.signalAmber;
      case 'Resuelto':
      case 'Completada':
        return AppColors.signalBlue;
      case 'Cerrado':
        return AppColors.flapInkDim;
      case 'Cancelado':
      case 'Cancelada':
        return AppColors.signalRed;
      default:
        return AppColors.boardSeam;
    }
  }

  static Color priorityColor(String value) {
    switch (value) {
      case 'Critica':
        return AppColors.signalRed;
      case 'Alta':
        return AppColors.signalAmber;
      case 'Media':
        return AppColors.signalBlueBright;
      default:
        return AppColors.boardSeam;
    }
  }
}
