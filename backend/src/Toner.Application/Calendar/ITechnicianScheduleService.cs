using Toner.Application.Calendar.Dtos;

namespace Toner.Application.Calendar;

public interface ITechnicianScheduleService
{
    Task<TechnicianScheduleDto> GetScheduleAsync(Guid technicianId, CancellationToken cancellationToken = default);

    // Reemplaza todos los tramos del técnico por los recibidos.
    Task<TechnicianScheduleDto> SetScheduleAsync(Guid technicianId, SetTechnicianScheduleRequest request, CancellationToken cancellationToken = default);

    // Borra el horario propio: vuelve a aplicar el de la empresa.
    Task<TechnicianScheduleDto> ResetScheduleAsync(Guid technicianId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeOffDto>> ListTimeOffAsync(Guid technicianId, CancellationToken cancellationToken = default);

    Task<TimeOffDto> AddTimeOffAsync(Guid technicianId, CreateTimeOffRequest request, Guid performedByUserId, CancellationToken cancellationToken = default);

    // Termina antes de tiempo un "fuera de la oficina" (o anula uno que aún no empezó).
    Task<TimeOffDto> CancelTimeOffAsync(Guid technicianId, Guid timeOffId, CancellationToken cancellationToken = default);
}

public interface IHolidayService
{
    Task<IReadOnlyList<HolidayDto>> ListAsync(int year, CancellationToken cancellationToken = default);
    Task<HolidayDto> SetOverrideAsync(SetHolidayOverrideRequest request, CancellationToken cancellationToken = default);
    Task RemoveOverrideAsync(DateOnly date, CancellationToken cancellationToken = default);
}
