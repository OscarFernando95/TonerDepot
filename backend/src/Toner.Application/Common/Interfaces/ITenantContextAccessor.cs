namespace Toner.Application.Common.Interfaces;

// Contexto de tenencia que se propaga a Postgres como variables de sesión (app.is_staff /
// app.current_client_id) para que las políticas RLS lo usen — ver SECURITY_AUDIT.md hallazgo #5.
//
// RLS es una SEGUNDA capa: el filtrado por cliente/técnico de la capa de aplicación
// (RequestingUser + los Where de cada servicio) sigue intacto y sigue siendo la primera línea.
public sealed record TenantContext(bool IsStaff, Guid? ClientId)
{
    // Staff (Administrador/Coordinador) y Técnico son confiables a nivel de BD: ven datos de
    // varios clientes por diseño (ej. un técnico atiende tickets de cualquier cliente en sus
    // ciudades de cobertura), así que su filtrado fino vive en C#, no en las políticas.
    public static readonly TenantContext Staff = new(true, null);

    // Sin rol reconocido y sin cliente: las políticas RLS no dejan pasar ninguna fila de las
    // tablas protegidas. Es el contexto de una request sin autenticar (ej. el login, que consulta
    // Users — tabla deliberadamente fuera de RLS, ver la migración AddRowLevelSecurity).
    public static readonly TenantContext Anonymous = new(false, null);

    public static TenantContext ForClient(Guid clientId) => new(false, clientId);
}

// El contexto se resuelve de forma perezosa (Func<TenantContext>) a propósito: en una request HTTP,
// HttpContext.User todavía no está poblado cuando corre la validación del token (que consulta
// Users), pero sí lo está cuando corre el controller. Un valor capturado al inicio del pipeline
// daría el contexto equivocado en una de las dos fases.
public interface ITenantContextAccessor
{
    TenantContext? Current { get; }

    IDisposable Push(Func<TenantContext> contextFactory);
}
