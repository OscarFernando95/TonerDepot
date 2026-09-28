namespace Toner.Domain.Enums;

// Cobertura de soporte contratada por el cliente. Define cómo se cuenta su SLA y a quién se le puede
// asignar: HorarioOficina respeta el horario laboral del técnico y los festivos; Continuo24x7 cuenta
// horas corridas y no depende del horario del técnico (sí de que no esté "fuera de la oficina").
public enum SupportCoverage
{
    HorarioOficina = 0,
    Continuo24x7 = 1
}
