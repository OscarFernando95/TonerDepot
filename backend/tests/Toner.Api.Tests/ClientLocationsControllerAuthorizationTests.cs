using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Toner.Api.Controllers;
using Toner.Application.Clients;
using Toner.Application.Clients.Dtos;
using Toner.Application.Common.Paging;
using Toner.Application.Common.Exceptions;
using Toner.Domain.Common;

namespace Toner.Api.Tests;

// AUDIT.md H-25: un usuario Cliente sin claim client_id hacía Guid.Parse(null!) -> excepción no
// controlada -> 500. Debe ser un 403 (ForbiddenException).
public class ClientLocationsControllerAuthorizationTests
{
    private static ClientLocationsController BuildController(string role, string? clientId, out FakeLocationService service)
    {
        service = new FakeLocationService();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };
        if (clientId is not null)
        {
            claims.Add(new Claim("client_id", clientId));
        }

        return new ClientLocationsController(service, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
    }

    [Theory]
    [InlineData(null)]
    [InlineData("no-es-un-guid")]
    public async Task List_ClienteSinClaimClientIdValido_LanzaForbidden_NoUn500(string? clientIdClaim)
    {
        var controller = BuildController(RoleNames.Cliente, clientIdClaim, out var service);

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.List(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task List_ClienteDeOtroCliente_LanzaForbidden()
    {
        var controller = BuildController(RoleNames.Cliente, Guid.NewGuid().ToString(), out var service);

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.List(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task List_ClienteDeSuPropioCliente_Pasa()
    {
        var clientId = Guid.NewGuid();
        var controller = BuildController(RoleNames.Cliente, clientId.ToString(), out var service);

        await controller.List(clientId, CancellationToken.None);

        Assert.Equal(1, service.Calls);
    }

    [Fact]
    public async Task List_Staff_NoNecesitaClaimClientId()
    {
        var controller = BuildController(RoleNames.Coordinador, clientId: null, out var service);

        await controller.List(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(1, service.Calls);
    }

    private sealed class FakeLocationService : IClientLocationService
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<ClientLocationDto>> ListByClientAsync(Guid clientId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<ClientLocationDto>>(Array.Empty<ClientLocationDto>());
        }

        public Task<ClientLocationDto> CreateAsync(Guid clientId, CreateClientLocationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<ClientLocationDto>> ListAllAsync(int? page, int? pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ClientLocationDto> UpdateAsync(Guid clientId, Guid id, UpdateClientLocationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ClientLocationDto> SetActiveStatusAsync(Guid clientId, Guid id, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
