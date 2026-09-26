import '../models/paged_result.dart';
import '../models/pending_installation.dart';
import '../models/technician_status.dart';
import 'api_client.dart';

class CheckInRequest {
  final String? serviceTicketId;
  final String? maintenanceOrderId;
  final String? assetId;

  CheckInRequest({this.serviceTicketId, this.maintenanceOrderId, this.assetId});

  Map<String, dynamic> toJson() => {
        if (serviceTicketId != null) 'serviceTicketId': serviceTicketId,
        if (maintenanceOrderId != null) 'maintenanceOrderId': maintenanceOrderId,
        if (assetId != null) 'assetId': assetId,
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
      };
}

class TechnicianApi {
  TechnicianApi(this._client);
  final ApiClient _client;

  Future<TechnicianSelfStatus> getMyStatus() async {
    try {
      final response = await _client.dio.get('/technicians/me/status');
      return TechnicianSelfStatus.fromJson(response.data as Map<String, dynamic>);
    } catch (e) {
      throw ApiClient.translate(e);
    }
  }

  Future<List<PendingInstallation>> listPendingInstallations() async {
    try {
      final response = await _client.dio.get('/technicians/me/pending-installations');
      final page = PagedResult<PendingInstallation>.fromJson(
        response.data as Map<String, dynamic>,
        PendingInstallation.fromJson,
      );
      return page.items;
    } catch (e) {
      throw ApiClient.translate(e);
    }
  }

  Future<TechnicianSelfStatus> checkIn(CheckInRequest request) async {
    try {
      final response = await _client.dio.post('/technicians/me/check-in', data: request.toJson());
      return TechnicianSelfStatus.fromJson(response.data as Map<String, dynamic>);
    } catch (e) {
      throw ApiClient.translate(e);
    }
  }

  Future<TechnicianSelfStatus> checkOut(CheckOutRequest request) async {
    try {
      final response = await _client.dio.post('/technicians/me/check-out', data: request.toJson());
      return TechnicianSelfStatus.fromJson(response.data as Map<String, dynamic>);
    } catch (e) {
      throw ApiClient.translate(e);
    }
  }
}
