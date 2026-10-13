using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Toner.Api.Controllers;
using Toner.Application.Common.Exceptions;
using Toner.Application.Evidences;
using Toner.Application.Evidences.Dtos;
using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Api.Tests;

// AUDIT.md H-09: en EvidenceController, "null" significa "staff: ve todo las evidencias de todos los
// clientes". Un usuario con rol Tecnico SIN claim technician_id válido caía en ese null (falla
// abierto). Debe fallar cerrado, igual que CurrentTechnicianId en TechnicianSelfServiceController.
public class EvidenceControllerAuthorizationTests
{
    private static EvidenceController BuildController(FakeEvidenceService service, string role, string? technicianId)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (technicianId is not null)
        {
            claims.Add(new Claim("technician_id", technicianId));
        }

        return new EvidenceController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task List_TecnicoSinClaimTechnicianIdValido_LanzaForbidden_YNoLlegaAlServicio(string? technicianId)
    {
        var service = new FakeEvidenceService();
        var controller = BuildController(service, RoleNames.Tecnico, technicianId);

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.List(null, null, CancellationToken.None));

        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task GetContent_TecnicoSinClaim_LanzaForbidden_YNoLlegaAlServicio()
    {
        var service = new FakeEvidenceService();
        var controller = BuildController(service, RoleNames.Tecnico, technicianId: null);

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetContent(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task List_TecnicoConClaim_PasaSuIdAlServicio()
    {
        var service = new FakeEvidenceService();
        var technicianId = Guid.NewGuid();
        var controller = BuildController(service, RoleNames.Tecnico, technicianId.ToString());

        await controller.List(null, null, CancellationToken.None);

        Assert.Equal(technicianId, service.LastRequesterTechnicianId);
    }

    [Fact]
    public async Task List_Staff_PasaNullAlServicio_ParaVerTodo()
    {
        var service = new FakeEvidenceService();
        var controller = BuildController(service, RoleNames.Coordinador, technicianId: null);

        await controller.List(null, null, CancellationToken.None);

        Assert.Equal(1, service.Calls);
        Assert.Null(service.LastRequesterTechnicianId);
    }

    private sealed class FakeEvidenceService : IEvidenceService
    {
        public int Calls { get; private set; }
        public Guid? LastRequesterTechnicianId { get; private set; }

        public Task<EvidenceDto> UploadAsync(Guid technicianId, EvidenceKind kind, Guid? serviceTicketId, Guid? maintenanceOrderId, Stream content, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<EvidenceDto>> ListAsync(Guid? serviceTicketId, Guid? maintenanceOrderId, Guid? requesterTechnicianId, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequesterTechnicianId = requesterTechnicianId;
            return Task.FromResult<IReadOnlyList<EvidenceDto>>(Array.Empty<EvidenceDto>());
        }

        public Task<EvidenceContent> GetContentAsync(Guid evidenceId, Guid? requesterTechnicianId, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequesterTechnicianId = requesterTechnicianId;
            return Task.FromResult(new EvidenceContent(Stream.Null, "image/jpeg", "x.jpg"));
        }
    }
}
