import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:image_picker/image_picker.dart';

import '../models/inventory.dart';
import '../models/paged_result.dart';
import '../models/pending_installation.dart';
import '../models/technician_status.dart';
import 'api_client.dart';
import 'device_capture.dart';

class CheckInRequest {
  final String? serviceTicketId;
  final String? maintenanceOrderId;
  final String? assetId;

  /// Ubicación del técnico y foto "antes" ya subida (obligatoria para tickets y órdenes).
  final PositionFix? position;
  final String? beforeEvidenceId;

  CheckInRequest({
    this.serviceTicketId,
    this.maintenanceOrderId,
    this.assetId,
    this.position,
    this.beforeEvidenceId,
  });

  Map<String, dynamic> toJson() => {
    if (serviceTicketId != null) 'serviceTicketId': serviceTicketId,
    if (maintenanceOrderId != null) 'maintenanceOrderId': maintenanceOrderId,
    if (assetId != null) 'assetId': assetId,
    if (position != null) ...{
      'latitude': position!.latitude,
      'longitude': position!.longitude,
      'accuracyMeters': position!.accuracyMeters,
    },
    if (beforeEvidenceId != null) 'beforeEvidenceId': beforeEvidenceId,
  };
}

/// Ver frontend-web/src/api/technicianSelf.ts (CheckOutRequest) para el
/// contrato completo. Cubre ticket, orden de mantenimiento e instalación de
/// activo — mismos campos condicionales que checkoutFormValid en
/// MyWorkView.vue (ver CheckoutSheet para el detalle de cuáles son
/// obligatorios en cada caso).
class CheckOutRequest {
  final bool resolved;
  final String? notes;
  final String? area;
  final int? initialCounterValue;
  final DateTime? initialCounterDate;
  final bool? generalMaintenanceDone;
  final bool? unitsMaintenanceDone;
  final int? existingConsumablesPrints;
  final String? externalAssetBrand;
  final String? externalAssetModel;
  final int? externalAssetCounter;

  /// Piezas usadas (kit marcado + repuestos). Solo tickets y órdenes: el servidor responde 409 en instalaciones.
  final List<UsedPart> parts;

  /// Ubicación al cerrar y foto "después" ya subida (obligatoria al resolver un ticket u orden).
  // No son final: MyWorkState los completa (ubicación y foto ya subida) justo antes de enviar.
  PositionFix? position;
  String? afterEvidenceId;

  /// Foto del contador ya subida: obligatoria al completar una orden (respalda la lectura).
  String? counterEvidenceId;

  CheckOutRequest({
    required this.resolved,
    this.notes,
    this.area,
    this.initialCounterValue,
    this.initialCounterDate,
    this.generalMaintenanceDone,
    this.unitsMaintenanceDone,
    this.existingConsumablesPrints,
    this.externalAssetBrand,
    this.externalAssetModel,
    this.externalAssetCounter,
    this.parts = const [],
    this.position,
    this.afterEvidenceId,
    this.counterEvidenceId,
  });

  Map<String, dynamic> toJson() => {
    'resolved': resolved,
    'notes': notes,
    'area': area,
    'initialCounterValue': initialCounterValue,
    'initialCounterDate': initialCounterDate?.toUtc().toIso8601String(),
    'generalMaintenanceDone': generalMaintenanceDone,
    'unitsMaintenanceDone': unitsMaintenanceDone,
    'existingConsumablesPrints': existingConsumablesPrints,
    'externalAssetBrand': externalAssetBrand,
    'externalAssetModel': externalAssetModel,
    'externalAssetCounter': externalAssetCounter,
    if (parts.isNotEmpty) 'parts': parts.map((p) => p.toJson()).toList(),
    if (position != null) ...{
      'latitude': position!.latitude,
      'longitude': position!.longitude,
      'accuracyMeters': position!.accuracyMeters,
    },
    if (afterEvidenceId != null) 'afterEvidenceId': afterEvidenceId,
    if (counterEvidenceId != null) 'counterEvidenceId': counterEvidenceId,
  };
}

class TechnicianApi {
  TechnicianApi(this._client);
  final ApiClient _client;

  Future<TechnicianSelfStatus> getMyStatus() async {
    try {
      final response = await _client.dio.get('/technicians/me/status');
      return TechnicianSelfStatus.fromJson(
        response.data as Map<String, dynamic>,
      );
    } catch (e, st) {
      debugPrint('TechnicianApi.getMyStatus failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<List<PendingInstallation>> listPendingInstallations() async {
    try {
      final response = await _client.dio.get(
        '/technicians/me/pending-installations',
      );
      final page = PagedResult<PendingInstallation>.fromJson(
        response.data as Map<String, dynamic>,
        PendingInstallation.fromJson,
      );
      return page.items;
    } catch (e, st) {
      debugPrint('TechnicianApi.listPendingInstallations failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Sube la foto de evidencia (Antes | Despues) de un ticket u orden asignado y devuelve su id, que viaja en
  /// el check-in/out. multipart/form-data: el servidor valida el tipo por el contenido real, no por el nombre.
  Future<String> uploadEvidence(
    XFile photo, {
    required String kind,
    String? ticketId,
    String? orderId,
  }) async {
    try {
      final form = FormData.fromMap({
        'file': await MultipartFile.fromFile(
          photo.path,
          filename: 'evidencia.jpg',
        ),
        'kind': kind,
        'ticketId': ?ticketId,
        'orderId': ?orderId,
      });
      final response = await _client.dio.post(
        '/technicians/me/evidence',
        data: form,
        options: Options(
          sendTimeout: const Duration(seconds: 60),
          receiveTimeout: const Duration(seconds: 60),
        ),
      );
      return (response.data as Map<String, dynamic>)['id'] as String;
    } catch (e, st) {
      debugPrint('TechnicianApi.uploadEvidence failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<TechnicianSelfStatus> checkIn(CheckInRequest request) async {
    try {
      final response = await _client.dio.post(
        '/technicians/me/check-in',
        data: request.toJson(),
      );
      return TechnicianSelfStatus.fromJson(
        response.data as Map<String, dynamic>,
      );
    } catch (e, st) {
      debugPrint('TechnicianApi.checkIn failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<TechnicianSelfStatus> checkOut(CheckOutRequest request) async {
    try {
      final response = await _client.dio.post(
        '/technicians/me/check-out',
        data: request.toJson(),
      );
      return TechnicianSelfStatus.fromJson(
        response.data as Map<String, dynamic>,
      );
    } catch (e, st) {
      debugPrint('TechnicianApi.checkOut failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
