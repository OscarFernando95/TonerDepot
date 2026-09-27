/// Formatea una fecha-calendario pura (sin hora) como "YYYY-MM-DD" — el
/// shape que esperan los campos `date`/`date?` del backend en Contratos
/// (startDate/endDate), a diferencia de los timestamps reales que sí llevan
/// hora y van con `.toUtc().toIso8601String()` (ver meter_reading_api.dart).
String formatDateOnly(DateTime date) =>
    '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
