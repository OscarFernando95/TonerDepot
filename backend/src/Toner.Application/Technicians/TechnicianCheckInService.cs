using Toner.Application.Geo;
using Toner.Application.Evidences;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets;
using Toner.Application.Assets.Dtos;
using Toner.Application.Assignment;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Maintenance;
using Toner.Application.Maintenance.Dtos;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tickets;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Technicians;

public class TechnicianCheckInService : ITechnicianCheckInService
{
    private readonly IApplicationDbContext _db;
    private readonly IServiceTicketService _ticketService;
    private readonly IMaintenanceOrderService _orderService;
    private readonly IAssetService _assetService;
    private readonly IMaintenanceScheduleEngine _scheduleEngine;
    private readonly IAssignmentEngine _assignmentEngine;
    private readonly GeoOptions _geo;
    private readonly EvidenceOptions _evidence;
    private readonly ILogger<TechnicianCheckInService> _logger;

    public TechnicianCheckInService(
        IApplicationDbContext db,
        IServiceTicketService ticketService,
        IMaintenanceOrderService orderService,
        IAssetService assetService,
        IMaintenanceScheduleEngine scheduleEngine,
        IAssignmentEngine assignmentEngine,
        IOptions<GeoOptions> geo,
        IOptions<EvidenceOptions> evidence,
        ILogger<TechnicianCheckInService> logger)
    {
        _geo = geo.Value;
        _evidence = evidence.Value;
        _logger = logger;
        _db = db;
        _ticketService = ticketService;
        _orderService = orderService;
        _assetService = assetService;
        _scheduleEngine = scheduleEngine;
        _assignmentEngine = assignmentEngine;
    }

    public async Task<TechnicianSelfStatusDto> GetMyStatusAsync(Guid technicianId, CancellationToken cancellationToken = default)
    {
        var technician = await _db.Technicians.FirstOrDefaultAsync(t => t.Id == technicianId, cancellationToken)
            ?? throw new NotFoundException(nameof(Technician), technicianId);

        var openLog = await _db.TimeLogs
            .Where(tl => tl.TechnicianId == technicianId && tl.EndTime == null)
            .OrderByDescending(tl => tl.StartTime)
            .FirstOrDefaultAsync(cancellationToken);

        return new TechnicianSelfStatusDto
        {
            TechnicianId = technicianId,
            Status = technician.Status.ToString(),
            ActiveServiceTicketId = openLog?.ServiceTicketId,
            ActiveMaintenanceOrderId = openLog?.MaintenanceOrderId,
            ActiveAssetInstallationId = openLog?.AssetId,
            CheckedInAt = openLog?.StartTime
        };
    }

    public async Task<TechnicianSelfStatusDto> CheckInAsync(Guid technicianId, CheckInRequest request, CancellationToken cancellationToken = default)
    {
        var technician = await _db.Technicians.FirstOrDefaultAsync(t => t.Id == technicianId, cancellationToken)
            ?? throw new NotFoundException(nameof(Technician), technicianId);

        if (technician.Status == TechnicianStatus.Ocupado)
        {
            throw new ConflictException("Ya tienes una visita en curso. Haz check-out antes de iniciar otra.");
        }

        if (request.ServiceTicketId.HasValue)
        {
            var ticket = await _db.ServiceTickets.FirstOrDefaultAsync(t => t.Id == request.ServiceTicketId, cancellationToken)
                ?? throw new NotFoundException(nameof(ServiceTicket), request.ServiceTicketId!.Value);

            if (ticket.TechnicianId != technicianId)
            {
                throw new ForbiddenException("Este ticket no está asignado a ti.");
            }

            if (ticket.Status is not (ServiceTicketStatus.Asignado or ServiceTicketStatus.EnProceso))
            {
                throw new ConflictException($"No puedes hacer check-in en un ticket en estado '{ticket.Status}'.");
            }

            ticket.Status = ServiceTicketStatus.EnProceso;
        }
        else if (request.MaintenanceOrderId.HasValue)
        {
            var order = await _db.MaintenanceOrders.FirstOrDefaultAsync(o => o.Id == request.MaintenanceOrderId, cancellationToken)
                ?? throw new NotFoundException(nameof(MaintenanceOrder), request.MaintenanceOrderId!.Value);

            if (order.TechnicianId != technicianId)
            {
                throw new ForbiddenException("Esta orden no está asignada a ti.");
            }

            if (order.Status is not (MaintenanceOrderStatus.Asignada or MaintenanceOrderStatus.EnProceso))
            {
                throw new ConflictException($"No puedes hacer check-in en una orden en estado '{order.Status}'.");
            }

            order.Status = MaintenanceOrderStatus.EnProceso;
        }
        else
        {
            // Lista abierta: cualquier técnico disponible puede tomar una instalación pendiente, no hay
            // asignación previa que validar — solo que siga pendiente y que nadie más la tenga en curso.
            var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken)
                ?? throw new NotFoundException(nameof(Asset), request.AssetId!.Value);

            if (asset.LifecycleStatus != AssetLifecycleStatus.PendienteInstalacion)
            {
                throw new ConflictException($"No puedes hacer check-in en un activo en estado '{asset.LifecycleStatus}'.");
            }

            var alreadyInProgress = await _db.TimeLogs
                .AnyAsync(tl => tl.AssetId == asset.Id && tl.EndTime == null, cancellationToken);
            if (alreadyInProgress)
            {
                throw new ConflictException("Otro técnico ya está atendiendo esta instalación.");
            }
        }

        // Foto "antes" y ubicación. Se validan ANTES de mutar nada (misma regla que el resto del servicio).
        var isVisit = request.ServiceTicketId.HasValue || request.MaintenanceOrderId.HasValue;
        Evidence? beforePhoto = null;
        if (isVisit && (request.BeforeEvidenceId.HasValue || _evidence.RequirePhotos))
        {
            beforePhoto = await RequireEvidenceAsync(
                request.BeforeEvidenceId, EvidenceKind.Antes, request.ServiceTicketId, request.MaintenanceOrderId,
                technician.UserId, "la foto de la falla o del estado del equipo (antes)", cancellationToken);
        }

        var (siteLat, siteLon) = await GetSiteCoordinatesAsync(request.ServiceTicketId, request.MaintenanceOrderId, request.AssetId, cancellationToken);
        var checkInLocation = LocationEvaluator.Evaluate(siteLat, siteLon, request.Latitude, request.Longitude, _geo.SiteRadiusMeters);
        if (checkInLocation.Status == LocationStatus.FueraDeSitio)
        {
            _logger.LogWarning(
                "Check-in fuera de sitio: técnico {TechnicianId} a {Distance} m de la sede (radio {Radius} m)",
                technicianId, checkInLocation.DistanceMeters, _geo.SiteRadiusMeters);
        }

        technician.Status = TechnicianStatus.Ocupado;
        _db.TechnicianAvailabilities.Add(new TechnicianAvailability
        {
            TechnicianId = technicianId,
            Status = TechnicianStatus.Ocupado,
            Reason = "Check-in"
        });

        // Captura al escribir desde el padre presente (exactamente uno de los tres, ver el check
        // constraint de TimeLogs). Nullable: un check-in de instalación sobre un activo que todavía
        // no tiene sede deja el TimeLog sin cliente, y la política no se lo muestra a nadie.
        var timeLogClientId = request.ServiceTicketId.HasValue
            ? await _db.ServiceTickets.Where(t => t.Id == request.ServiceTicketId.Value)
                .Select(t => (Guid?)t.ClientId).FirstOrDefaultAsync(cancellationToken)
            : request.MaintenanceOrderId.HasValue
                ? await _db.MaintenanceOrders.Where(o => o.Id == request.MaintenanceOrderId.Value)
                    .Select(o => (Guid?)o.ClientId).FirstOrDefaultAsync(cancellationToken)
                : request.AssetId.HasValue
                    ? await GetAssetClientIdAsync(request.AssetId.Value, cancellationToken)
                    : null;

        var timeLog = new TimeLog
        {
            TechnicianId = technicianId,
            ClientId = timeLogClientId,
            ServiceTicketId = request.ServiceTicketId,
            MaintenanceOrderId = request.MaintenanceOrderId,
            AssetId = request.AssetId,
            StartTime = DateTime.UtcNow,
            CheckInLatitude = request.Latitude,
            CheckInLongitude = request.Longitude,
            CheckInAccuracyMeters = request.AccuracyMeters,
            CheckInDistanceMeters = checkInLocation.DistanceMeters,
            CheckInLocationStatus = checkInLocation.Status
        };
        _db.TimeLogs.Add(timeLog);
        if (beforePhoto is not null)
        {
            beforePhoto.TimeLogId = timeLog.Id;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetMyStatusAsync(technicianId, cancellationToken);
    }

    public async Task<TechnicianSelfStatusDto> CheckOutAsync(Guid technicianId, CheckOutRequest request, CancellationToken cancellationToken = default)
    {
        var technician = await _db.Technicians.FirstOrDefaultAsync(t => t.Id == technicianId, cancellationToken)
            ?? throw new NotFoundException(nameof(Technician), technicianId);

        if (technician.Status != TechnicianStatus.Ocupado)
        {
            throw new ConflictException("No tienes una visita en curso para cerrar.");
        }

        var openLog = await _db.TimeLogs
            .Where(tl => tl.TechnicianId == technicianId && tl.EndTime == null)
            .OrderByDescending(tl => tl.StartTime)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException("No se encontró un registro de tiempo abierto.");

        var isInstallationCheckout = openLog.AssetId.HasValue && request.Resolved;
        var isOrderCheckout = openLog.MaintenanceOrderId.HasValue && request.Resolved;

        // Se carga el ticket completo (no solo su AssetId) porque, además del contador (opcional — no
        // toda visita correctiva implica estar frente al equipo), también puede recibir los datos
        // opcionales del equipo externo cuando no tiene Asset asociado (cliente sin contrato).
        ServiceTicket? openTicket = null;
        if (openLog.ServiceTicketId.HasValue)
        {
            openTicket = await _db.ServiceTickets.FirstOrDefaultAsync(t => t.Id == openLog.ServiceTicketId.Value, cancellationToken);
        }
        Guid? ticketAssetId = openTicket?.AssetId;

        // Se valida todo antes de mutar nada, para no dejar el check-out cerrado con datos a medias.
        Guid? assetForCounterCheck = isInstallationCheckout ? openLog.AssetId
            : isOrderCheckout ? await _db.MaintenanceOrders.Where(o => o.Id == openLog.MaintenanceOrderId!.Value).Select(o => (Guid?)o.AssetId).FirstAsync(cancellationToken)
            : ticketAssetId;

        if (isInstallationCheckout)
        {
            if (string.IsNullOrWhiteSpace(request.Area))
            {
                throw new ConflictException("El área es obligatoria para completar la instalación.");
            }
            if (!request.InitialCounterValue.HasValue)
            {
                throw new ConflictException("Debes ingresar el contador inicial para completar la instalación.");
            }
            if (!request.GeneralMaintenanceDone.HasValue)
            {
                throw new ConflictException("Debes indicar si el activo tiene mantenimiento general realizado.");
            }
            if (!request.UnitsMaintenanceDone.HasValue)
            {
                throw new ConflictException("Debes indicar si el activo tiene mantenimiento de unidades realizado o insumos nuevos.");
            }
        }
        else if (isOrderCheckout && !request.InitialCounterValue.HasValue)
        {
            throw new ConflictException("Debes ingresar el contador para completar la orden de mantenimiento.");
        }

        if (assetForCounterCheck.HasValue && request.InitialCounterValue.HasValue)
        {
            var lastReading = await _db.MeterReadings
                .Where(m => m.AssetId == assetForCounterCheck.Value)
                .OrderByDescending(m => m.ReadingDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastReading is not null && request.InitialCounterValue.Value < lastReading.CounterValue)
            {
                throw new ConflictException(
                    $"La lectura ({request.InitialCounterValue.Value}) no puede ser menor a la última registrada ({lastReading.CounterValue}).");
            }

            // Misma fecha efectiva que se va a guardar en el MeterReading más abajo (request.InitialCounterDate
            // ?? DateTime.UtcNow) — si se valida solo cuando el técnico manda fecha explícita, dejar el campo
            // vacío se cuela sin chequeo aunque igual quede fechado hoy.
            await EnsureCounterDateWithinContractAsync(
                assetForCounterCheck.Value, request.InitialCounterDate ?? DateTime.UtcNow, cancellationToken);
        }

        // Foto "después": obligatoria al resolver un ticket u orden. Se valida antes de mutar nada.
        var isVisitCheckout = openLog.ServiceTicketId.HasValue || openLog.MaintenanceOrderId.HasValue;
        Evidence? afterPhoto = null;
        if (isVisitCheckout && ((request.Resolved && _evidence.RequirePhotos) || request.AfterEvidenceId.HasValue))
        {
            afterPhoto = await RequireEvidenceAsync(
                request.AfterEvidenceId, EvidenceKind.Despues, openLog.ServiceTicketId, openLog.MaintenanceOrderId,
                technician.UserId, "la foto del resultado (después)", cancellationToken);
        }

        // Foto del contador: si la visita (ticket u orden) registra una lectura, debe venir con su foto. Las
        // instalaciones no entran: no tienen ticket/orden al que atar evidencia (ver CheckIn).
        var recordsReading = request.InitialCounterValue.HasValue && (isOrderCheckout || ticketAssetId.HasValue);
        Evidence? counterPhoto = null;
        if (isVisitCheckout && ((recordsReading && _evidence.RequirePhotos) || request.CounterEvidenceId.HasValue))
        {
            counterPhoto = await RequireEvidenceAsync(
                request.CounterEvidenceId, EvidenceKind.Contador, openLog.ServiceTicketId, openLog.MaintenanceOrderId,
                technician.UserId, "la foto del contador", cancellationToken);
        }

        var (siteLat, siteLon) = await GetSiteCoordinatesAsync(openLog.ServiceTicketId, openLog.MaintenanceOrderId, openLog.AssetId, cancellationToken);
        var checkOutLocation = LocationEvaluator.Evaluate(siteLat, siteLon, request.Latitude, request.Longitude, _geo.SiteRadiusMeters);
        if (checkOutLocation.Status == LocationStatus.FueraDeSitio)
        {
            _logger.LogWarning(
                "Check-out fuera de sitio: técnico {TechnicianId} a {Distance} m de la sede (radio {Radius} m)",
                technicianId, checkOutLocation.DistanceMeters, _geo.SiteRadiusMeters);
        }

        openLog.EndTime = DateTime.UtcNow;
        openLog.Notes = request.Notes?.Trim();
        openLog.CheckOutLatitude = request.Latitude;
        openLog.CheckOutLongitude = request.Longitude;
        openLog.CheckOutAccuracyMeters = request.AccuracyMeters;
        openLog.CheckOutDistanceMeters = checkOutLocation.DistanceMeters;
        openLog.CheckOutLocationStatus = checkOutLocation.Status;
        if (afterPhoto is not null)
        {
            afterPhoto.TimeLogId = openLog.Id;
        }
        if (counterPhoto is not null)
        {
            counterPhoto.TimeLogId = openLog.Id;
        }

        technician.Status = TechnicianStatus.Disponible;
        _db.TechnicianAvailabilities.Add(new TechnicianAvailability
        {
            TechnicianId = technicianId,
            Status = TechnicianStatus.Disponible,
            Reason = "Check-out"
        });

        if (isInstallationCheckout)
        {
            await _assetService.PrepareStatusChangeAsync(
                openLog.AssetId!.Value,
                new ChangeAssetStatusRequest
                {
                    NewStatus = nameof(AssetLifecycleStatus.Instalado),
                    Area = request.Area,
                    Notes = $"Instalación confirmada por técnico. General: {(request.GeneralMaintenanceDone!.Value ? "realizado" : "pendiente")}. " +
                        $"Unidades: {(request.UnitsMaintenanceDone!.Value ? "realizado" : "insumos nuevos")}."
                },
                technician.UserId,
                cancellationToken);

            // El técnico que instala un activo queda vinculado a él: es lo que gobierna qué ve en
            // "Lectura de contadores". Idempotente (índice único TechnicianId+AssetId) y dentro del
            // único SaveChanges de abajo, así que no queda el check-out cerrado sin el vínculo.
            var alreadyLinked = await _db.TechnicianAssets
                .AnyAsync(ta => ta.TechnicianId == technicianId && ta.AssetId == openLog.AssetId.Value, cancellationToken);
            if (!alreadyLinked)
            {
                _db.TechnicianAssets.Add(new TechnicianAsset { TechnicianId = technicianId, AssetId = openLog.AssetId.Value });
            }

            var counterDate = request.InitialCounterDate ?? DateTime.UtcNow;
            _db.MeterReadings.Add(new MeterReading
            {
                AssetId = openLog.AssetId.Value,
                ClientId = await GetAssetClientIdAsync(openLog.AssetId.Value, cancellationToken),
                ReadingDate = counterDate,
                CounterValue = request.InitialCounterValue!.Value,
                RegisteredByUserId = technician.UserId
            });

            var activeContractId = await _db.ContractAssets
                .Where(ca => ca.AssetId == openLog.AssetId.Value && ca.EndDate == null)
                .Select(ca => (Guid?)ca.ContractId)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeContractId.HasValue)
            {
                await _scheduleEngine.UpsertForInstallationAsync(
                    openLog.AssetId.Value,
                    activeContractId.Value,
                    request.InitialCounterValue.Value,
                    counterDate,
                    request.GeneralMaintenanceDone!.Value,
                    request.UnitsMaintenanceDone!.Value,
                    request.ExistingConsumablesPrints,
                    cancellationToken);
            }
        }
        MaintenanceOrder? orderFromTicketReading = null;
        if (!isInstallationCheckout && ticketAssetId.HasValue && request.InitialCounterValue.HasValue)
        {
            var counterDate = request.InitialCounterDate ?? DateTime.UtcNow;
            _db.MeterReadings.Add(new MeterReading
            {
                AssetId = ticketAssetId.Value,
                ClientId = await GetAssetClientIdAsync(ticketAssetId.Value, cancellationToken),
                ReadingDate = counterDate,
                CounterValue = request.InitialCounterValue!.Value,
                RegisteredByUserId = technician.UserId
            });

            orderFromTicketReading = await _scheduleEngine.EvaluateAsync(
                ticketAssetId.Value, request.InitialCounterValue.Value, counterDate, cancellationToken);
        }

        // Totalmente opcional y solo aplica a tickets sin Asset (cliente sin contrato) — no se exige
        // ningún dato, el técnico deja constancia de lo que alcanzó a identificar del equipo.
        if (openTicket is not null && openTicket.AssetId is null)
        {
            if (request.ExternalAssetBrand is not null) openTicket.ExternalAssetBrand = request.ExternalAssetBrand.Trim();
            if (request.ExternalAssetModel is not null) openTicket.ExternalAssetModel = request.ExternalAssetModel.Trim();
            if (request.ExternalAssetCounter is not null) openTicket.ExternalAssetCounter = request.ExternalAssetCounter;
        }

        if (orderFromTicketReading is not null)
        {
            await _assignmentEngine.AssignMaintenanceOrderAsync(orderFromTicketReading, cancellationToken);
        }

        // El check-out solo cierra la visita; marcar Resuelto/Completada reutiliza la misma lógica
        // (y efectos secundarios, como el recálculo del cronograma) que usan los endpoints manuales de
        // Staff — pero en su variante que NO guarda, para que todo caiga en el único SaveChanges de
        // abajo.
        if (request.Resolved)
        {
            if (openLog.ServiceTicketId.HasValue)
            {
                await _ticketService.PrepareStatusChangeAsync(openLog.ServiceTicketId.Value, nameof(ServiceTicketStatus.Resuelto), cancellationToken);
            }
            else if (openLog.MaintenanceOrderId.HasValue)
            {
                await _orderService.PrepareCompleteAsync(
                    openLog.MaintenanceOrderId.Value,
                    new CompleteMaintenanceOrderRequest
                    {
                        CounterValue = request.InitialCounterValue!.Value,
                        ReadingDate = request.InitialCounterDate
                    },
                    technician.UserId,
                    cancellationToken);
            }
        }

        // ÚNICO punto de persistencia de todo el check-out: cierre del TimeLog, técnico a Disponible,
        // lectura de contador, cronograma, asignación de la orden generada y el cierre del
        // ticket/orden. Antes eran hasta tres commits sucesivos; si el último fallaba, la visita
        // quedaba cerrada y el técnico Disponible pero el ticket seguía EnProceso — y el reintento
        // natural (volver a hacer check-in, que EnProceso permite) grababa una MeterReading
        // DUPLICADA, que corrompe tanto los umbrales de mantenimiento como el promedio de impresiones
        // por mes del contrato (CODE_QUALITY_AUDIT.md hallazgo #8).
        await _db.SaveChangesAsync(cancellationToken);

        return await GetMyStatusAsync(technicianId, cancellationToken);
    }

    // El ClientId denormalizado de las tablas de fase 3b se captura al escribir. En este servicio el
    // origen es siempre el activo, del que solo se tiene el id — de ahí esta consulta puntual por PK.
    // Devuelve null si el activo está en bodega: esa fila no pertenece a ningún cliente y la política
    // RLS no se la muestra a nadie (fail-closed).
    // Coordenadas de la sede de la visita: la del ticket, o la sede actual del activo (órdenes e instalaciones).
    private async Task<(double? Latitude, double? Longitude)> GetSiteCoordinatesAsync(
        Guid? serviceTicketId, Guid? maintenanceOrderId, Guid? assetId, CancellationToken cancellationToken)
    {
        if (serviceTicketId.HasValue)
        {
            var site = await _db.ServiceTickets.Where(t => t.Id == serviceTicketId.Value)
                .Select(t => new { t.ClientLocation.Latitude, t.ClientLocation.Longitude }).FirstOrDefaultAsync(cancellationToken);
            return (site?.Latitude, site?.Longitude);
        }

        var resolvedAssetId = assetId;
        if (maintenanceOrderId.HasValue)
        {
            resolvedAssetId = await _db.MaintenanceOrders.Where(o => o.Id == maintenanceOrderId.Value)
                .Select(o => (Guid?)o.AssetId).FirstOrDefaultAsync(cancellationToken);
        }

        if (!resolvedAssetId.HasValue)
        {
            return (null, null);
        }

        var assetSite = await _db.Assets.Where(a => a.Id == resolvedAssetId.Value && a.CurrentClientLocation != null)
            .Select(a => new { a.CurrentClientLocation!.Latitude, a.CurrentClientLocation.Longitude }).FirstOrDefaultAsync(cancellationToken);
        return (assetSite?.Latitude, assetSite?.Longitude);
    }

    // La foto debe existir, ser de este técnico, del tipo esperado, del mismo ticket/orden y no estar ya
    // atada a otro check-in/out (una foto no se reutiliza).
    private async Task<Evidence> RequireEvidenceAsync(
        Guid? evidenceId, EvidenceKind kind, Guid? serviceTicketId, Guid? maintenanceOrderId, Guid uploaderUserId,
        string description, CancellationToken cancellationToken)
    {
        if (!evidenceId.HasValue)
        {
            throw new ConflictException($"Debes tomar {description} antes de continuar.");
        }

        var evidence = await _db.Evidences.FirstOrDefaultAsync(e => e.Id == evidenceId.Value, cancellationToken)
            ?? throw new ConflictException($"No se encontró {description}. Vuelve a tomarla.");

        if (evidence.UploadedByUserId != uploaderUserId
            || evidence.Kind != kind
            || evidence.ServiceTicketId != serviceTicketId
            || evidence.MaintenanceOrderId != maintenanceOrderId)
        {
            throw new ConflictException($"La foto enviada no corresponde a {description} de esta visita.");
        }

        if (evidence.TimeLogId.HasValue)
        {
            throw new ConflictException("Esa foto ya se usó en otra visita. Toma una nueva.");
        }

        return evidence;
    }

    private Task<Guid?> GetAssetClientIdAsync(Guid assetId, CancellationToken cancellationToken) =>
        _db.Assets.Where(a => a.Id == assetId).Select(a => a.ClientId).FirstOrDefaultAsync(cancellationToken);

    // La fecha de lectura de un check-out (instalación, orden o ticket) no puede caer fuera de la
    // vigencia del contrato que cubre ese activo — de lo contrario queda un registro que contradice el
    // contrato mismo (ej. una "instalación" fechada antes de que el contrato exista). El caller siempre
    // pasa la fecha EFECTIVA (request.InitialCounterDate ?? DateTime.UtcNow), así que dejar el campo de
    // fecha vacío no evade esta regla. Si el activo no tiene un contrato activo vinculado, no hay nada
    // que validar aquí.
    private async Task EnsureCounterDateWithinContractAsync(Guid assetId, DateTime counterDate, CancellationToken cancellationToken)
    {
        var contractId = await _db.ContractAssets
            .Where(ca => ca.AssetId == assetId && ca.EndDate == null)
            .Select(ca => (Guid?)ca.ContractId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!contractId.HasValue) return;

        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId.Value, cancellationToken);
        if (contract is null) return;

        var date = counterDate.Date;
        if (date < contract.StartDate.Date || (contract.EndDate.HasValue && date > contract.EndDate.Value.Date))
        {
            var rango = contract.EndDate.HasValue
                ? $"entre el {contract.StartDate:dd/MM/yyyy} y el {contract.EndDate.Value:dd/MM/yyyy}"
                : $"a partir del {contract.StartDate:dd/MM/yyyy}";
            throw new ConflictException($"La fecha de la lectura debe estar {rango}, según la vigencia del contrato de este cliente.");
        }
    }

}
