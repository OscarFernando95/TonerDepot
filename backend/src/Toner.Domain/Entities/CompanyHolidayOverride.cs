using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Ajuste manual sobre los festivos legales de Colombia (que se calculan por algoritmo, ver
// ColombiaHolidays): IsWorkingDay = false agrega un día no laborable propio (cierre de la empresa);
// true fuerza un día laborable aunque sea festivo legal.
public class CompanyHolidayOverride : BaseEntity
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsWorkingDay { get; set; }
}
