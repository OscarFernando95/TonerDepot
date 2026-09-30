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

  /// Espejo de ServiceTicketAllowedTransitions (frontend-web/src/api/types.ts)
  /// — qué estados puede elegir Staff desde el detalle de un ticket.
  static const Map<String, List<String>> ticketAllowedTransitions = {
    'Abierto': ['Cancelado'],
    'SinAsignar': ['Cancelado'],
    'Asignado': ['EnProceso', 'Cancelado'],
    'EnProceso': ['Resuelto', 'Cancelado'],
    'Resuelto': ['Cerrado', 'EnProceso'],
    'Cerrado': [],
    'Cancelado': [],
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

  static const Map<String, String> assetLifecycle = {
    'EnBodega': 'En bodega',
    'Instalado': 'Instalado',
    'EnMantenimiento': 'En mantenimiento',
    'PendienteInstalacion': 'Pendiente de instalación',
    'DadoDeBaja': 'Dado de baja',
  };

  static const Map<String, String> contract = {
    'Activo': 'Activo',
    'Vencido': 'Vencido',
    'Cancelado': 'Cancelado',
  };

  static const Map<String, String> technicianStatus = {
    'Disponible': 'Disponible',
    'Ocupado': 'Ocupado',
    'EnTransito': 'En tránsito',
    'Inactivo': 'Inactivo',
  };

  static Color colorFor(String status) {
    switch (status) {
      case 'Abierto':
      case 'SinAsignar':
      case 'Pendiente':
        return AppColors.neutral;
      case 'Asignado':
      case 'Asignada':
        return AppColors.signalBlueBright;
      case 'EnProceso':
        return AppColors.signalAmber;
      case 'Resuelto':
      case 'Completada':
        return AppColors.signalBlue;
      case 'Cerrado':
        return AppColors.inkSecondary;
      case 'Cancelado':
      case 'Cancelada':
        return AppColors.signalRed;
      default:
        return AppColors.neutral;
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
        return AppColors.neutral;
    }
  }

  static Color assetLifecycleColor(String value) {
    switch (value) {
      case 'Instalado':
        return AppColors.signalBlue;
      case 'EnMantenimiento':
        return AppColors.signalAmber;
      case 'PendienteInstalacion':
        return AppColors.signalBlueBright;
      case 'DadoDeBaja':
        return AppColors.signalRed;
      case 'EnBodega':
      default:
        return AppColors.neutral;
    }
  }

  static Color contractColor(String value) {
    switch (value) {
      case 'Activo':
        return AppColors.signalBlue;
      case 'Vencido':
        return AppColors.signalAmber;
      case 'Cancelado':
        return AppColors.signalRed;
      default:
        return AppColors.neutral;
    }
  }

  static Color technicianStatusColor(String value) {
    switch (value) {
      case 'Disponible':
        return AppColors.signalBlue;
      case 'Ocupado':
        return AppColors.signalAmber;
      case 'EnTransito':
        return AppColors.signalBlueBright;
      case 'Inactivo':
        return AppColors.neutral;
      default:
        return AppColors.neutral;
    }
  }
}
