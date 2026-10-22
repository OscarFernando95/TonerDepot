using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Api.Controllers;
using Toner.Domain.Common;

namespace Toner.Api.Tests;

// La superficie exacta del rol Cliente: cualquier endpoint nuevo que lo admita (a propósito o por olvidar un
// [Authorize(Roles=...)]) hace fallar esta prueba y obliga a decidirlo. Un cliente solo reporta y sigue sus servicios
// (tickets, mantenimientos programados, instalaciones en sus sedes); de contratos no ve nada.
public class ClientRoleSurfaceTests
{
    private static readonly string[] HttpVerbs = { "HttpGet", "HttpPost", "HttpPut", "HttpPatch", "HttpDelete" };

    private static bool IsAction(MethodInfo m) =>
        m.IsPublic && !m.IsStatic && m.GetCustomAttributes().Any(a => HttpVerbs.Contains(a.GetType().Name.Replace("Attribute", "")));

    // Roles efectivos de un endpoint: el [Authorize(Roles)] del método pisa al de la clase en esta API (ver los
    // comentarios de ServiceTicketsController); sin Roles en ninguno, entra cualquier usuario autenticado.
    private static string[]? EffectiveRoles(Type controller, MethodInfo action)
    {
        var rolesOf = (MemberInfo member) => member.GetCustomAttributes<AuthorizeAttribute>()
            .Where(a => !string.IsNullOrWhiteSpace(a.Roles))
            .Select(a => a.Roles!.Split(',', StringSplitOptions.TrimEntries))
            .FirstOrDefault();

        return rolesOf(action) ?? rolesOf(controller);
    }

    private static List<string> EndpointsOpenTo(string role) =>
        typeof(AuthController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(c => c.GetMethods().Where(IsAction).Select(m => (Controller: c, Method: m)))
            .Where(x => x.Controller.GetCustomAttribute<AllowAnonymousAttribute>() is null && x.Method.GetCustomAttribute<AllowAnonymousAttribute>() is null)
            .Where(x => EffectiveRoles(x.Controller, x.Method) is { } roles ? roles.Contains(role) : true)
            .Select(x => $"{x.Controller.Name.Replace("Controller", "")}.{x.Method.Name}")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void ElCliente_SoloAlcanzaLosEndpointsDeServicioQueSeDecidieron()
    {
        var expected = new[]
        {
            // Cuenta propia
            "Auth.ChangePassword", "Auth.Logout", "Auth.Me",
            // Devices (push) y demás endpoints de cualquier usuario autenticado
            "Devices.Register", "Devices.Unregister",
            // Sedes de su empresa, para elegir dónde reportar
            "ClientLocations.List",
            // Sus equipos (incluye las instalaciones pendientes en sus sedes)
            "Assets.GetById", "Assets.List",
            // Su inicio: el resumen de su servicio
            "ClientPortal.GetHome",
            // Tickets: reportar y seguir
            "ServiceTickets.Create", "ServiceTickets.GetById", "ServiceTickets.List",
            // Mantenimientos programados: solo lectura
            "MaintenanceOrders.GetById", "MaintenanceOrders.List",
            // ...y el hilo de cada una: el cliente avisa si la fecha programada no le sirve
            "MaintenanceOrders.AddComment", "MaintenanceOrders.ListComments",
        };

        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), EndpointsOpenTo(RoleNames.Cliente));
    }

    [Fact]
    public void ElCliente_NoAlcanzaNingunEndpointDeContratos()
    {
        Assert.DoesNotContain(EndpointsOpenTo(RoleNames.Cliente), n => n.StartsWith("Contracts.") || n.StartsWith("ContractAssets."));
    }
}
