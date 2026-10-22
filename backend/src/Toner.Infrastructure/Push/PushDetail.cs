using Toner.Application.Push;
using Toner.Domain.Enums;

namespace Toner.Infrastructure.Push;

// Resumen operativo que acompaña a un aviso: lo justo para que el técnico (o el staff) decida a primera vista. Cada
// campo es opcional — un equipo externo sin catalogar no tiene área, una orden de mantenimiento no tiene criticidad.
// Nunca lleva dirección, contacto ni descripción del servicio (ver IPushNotifier).
public sealed record PushDetail(string? Client, string? City, ServiceTicketPriority? Priority, string? Equipment, string? Area)
{
    public string Format()
    {
        var lines = new List<string>();

        var place = string.Join(" · ", new[] { Client, City }.Where(v => !string.IsNullOrWhiteSpace(v)));
        if (place.Length > 0)
        {
            lines.Add(place);
        }

        var service = new List<string>();
        if (Priority.HasValue)
        {
            service.Add($"Criticidad {PriorityLabel(Priority.Value)}");
        }
        if (!string.IsNullOrWhiteSpace(Equipment))
        {
            service.Add(Equipment!);
        }
        if (!string.IsNullOrWhiteSpace(Area))
        {
            service.Add($"Área {Area}");
        }
        if (service.Count > 0)
        {
            lines.Add(string.Join(" · ", service));
        }

        return string.Join("\n", lines);
    }

    // Etiqueta con tilde; el enum no la lleva (ServiceTicketPriority.Critica).
    public static string PriorityLabel(ServiceTicketPriority priority) =>
        priority == ServiceTicketPriority.Critica ? "Crítica" : priority.ToString();

    // En los avisos de "hay trabajo" (asignado, sin asignar, instalación disponible) el resumen ES el cuerpo: el texto
    // genérico ("Abre Toner para ver los detalles") sobra. En los de "ya no es tuyo" (reasignado, cancelado) el resumen
    // acompaña a la frase para saber de cuál servicio se habla. Sin nada que agregar, el mensaje queda igual.
    public PushMessage ApplyTo(PushMessage message)
    {
        var detail = Format();
        if (detail.Length == 0)
        {
            return message;
        }

        var event_ = message.Data.GetValueOrDefault("event");
        var replacesBody = event_ is PushChangeInterceptor.EventAssigned
            or PushChangeInterceptor.EventUnassigned or PushChangeInterceptor.EventAvailable;
        return message with { Body = replacesBody ? detail : $"{message.Body}\n{detail}" };
    }

    public static string? JoinEquipment(string? brand, string? model) =>
        string.IsNullOrWhiteSpace(model) ? null : string.IsNullOrWhiteSpace(brand) ? model : $"{brand} {model}";
}
