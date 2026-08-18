using Toner.Domain.Entities;

namespace Toner.Application.Maintenance;

// Motor central de cronogramas: crea/reinicia el cronograma de un activo al instalarse bajo un contrato,
// evalúa si corresponde generar una orden (con anticipación, no solo cuando ya venció), y recalcula las
// sub-reglas involucradas al completar una orden. Ninguno de estos métodos hace SaveChangesAsync — el
// caller decide cuándo persistir, para poder combinarlo con su propia transacción (mismo patrón que
// AssetService.PrepareStatusChangeAsync). Los umbrales se leen de Asset.AssetModel (marca+modelo del
// activo), no de un catálogo por AssetType.
public interface IMaintenanceScheduleEngine
{
    Task<MaintenanceSchedule> UpsertForInstallationAsync(
        Guid assetId,
        Guid contractId,
        long counterValue,
        DateTime counterDate,
        bool generalMaintenanceDone,
        bool unitsMaintenanceDone,
        long? existingConsumablesPrints,
        CancellationToken cancellationToken = default);

    // Devuelve la MaintenanceOrder recién creada (tracked, sin guardar) si alguna regla está por vencer
    // (dentro de la ventana de anticipación) y no hay ya una orden abierta; null si no corresponde generar nada.
    Task<MaintenanceOrder?> EvaluateAsync(
        Guid assetId,
        long currentCounter,
        DateTime asOf,
        CancellationToken cancellationToken = default);

    // Igual que EvaluateAsync, pero para TODOS los cronogramas activos a la vez — pensado para el
    // barrido diario (MaintenanceScheduleEvaluationJob), que evaluado un activo a la vez hacía ~3
    // consultas por cronograma (CODE_QUALITY_AUDIT.md hallazgo #6). Precarga cronogramas, órdenes
    // abiertas y últimas lecturas en un puñado de consultas, y aplica la MISMA lógica de decisión que
    // EvaluateAsync sobre esos datos ya en memoria — no hay una segunda implementación de la regla de
    // negocio. Igual que el resto de la interfaz, no hace SaveChangesAsync.
    Task<IReadOnlyList<MaintenanceOrder>> EvaluateAllDueAsync(
        DateTime asOf,
        CancellationToken cancellationToken = default);

    Task RecalculateAfterMaintenanceAsync(
        Guid scheduleId,
        long counterValue,
        DateTime counterDate,
        bool includesGeneral,
        bool includesUnits,
        bool includesConsumables,
        CancellationToken cancellationToken = default);
}
