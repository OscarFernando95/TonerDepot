import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../models/evidence.dart';
import 'api_client.dart';

/// Staff (y el técnico dueño): lista y contenido de las fotos de evidencia.
class EvidenceApi {
  EvidenceApi(this._client);
  final ApiClient _client;

  Future<List<EvidenceItem>> list({String? ticketId, String? orderId}) async {
    try {
      final response = await _client.dio.get(
        '/evidence',
        queryParameters: {'ticketId': ?ticketId, 'orderId': ?orderId},
      );
      return (response.data as List<dynamic>)
          .map((e) => EvidenceItem.fromJson(e as Map<String, dynamic>))
          .toList();
    } catch (e, st) {
      debugPrint('EvidenceApi.list failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// El contenedor de blobs es privado: la imagen se baja con el token y se muestra desde memoria.
  Future<Uint8List> content(String id) async {
    try {
      final response = await _client.dio.get<List<int>>(
        '/evidence/$id/content',
        options: Options(responseType: ResponseType.bytes),
      );
      return Uint8List.fromList(response.data!);
    } catch (e, st) {
      debugPrint('EvidenceApi.content failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
