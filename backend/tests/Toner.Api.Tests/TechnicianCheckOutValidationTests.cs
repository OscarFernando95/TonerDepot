using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Toner.Api.Controllers;
using Toner.Application.Assets.Dtos;
using Toner.Application.Common.Paging;
using Toner.Application.Technicians;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Technicians.Validators;
using Toner.Domain.Common;

namespace Toner.Api.Tests;

// AUDIT.md H-10: CheckOutRequest era el único body complejo sin validador. Una nota de más de 1000
// caracteres (columna varchar(1000)) llegaba a Postgres y salía como 500 en vez de 400.
public class TechnicianCheckOutValidationTests
{
    [Fact]
    public async Task CheckOut_NotasDeMasDe1000Caracteres_LanzaValidationException_YNoLlegaAlServicio()
    {
        var service = new FakeCheckInService();
        var controller = BuildController(service);

        await Assert.ThrowsAsync<ValidationException>(() => controller.CheckOut(
            new CheckOutRequest { Notes = new string('x', 1001) }, CancellationToken.None));

        Assert.Equal(0, service.CheckOutCalls);
    }

    [Fact]
    public async Task CheckOut_RequestValido_LlegaAlServicio()
    {
        var service = new FakeCheckInService();
        var controller = BuildController(service);

        await controller.CheckOut(new CheckOutRequest { Notes = "ok" }, CancellationToken.None);

        Assert.Equal(1, service.CheckOutCalls);
    }

    private static TechnicianSelfServiceController BuildController(FakeCheckInService service)
    {
        var claims = new[] { new Claim(ClaimTypes.Role, RoleNames.Tecnico), new Claim("technician_id", Guid.NewGuid().ToString()) };
        return new TechnicianSelfServiceController(
            service, null!, new CheckOutRequestValidator(), null!, null!, null!, null!, null!, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
            }
        };
    }

    private sealed class FakeCheckInService : ITechnicianCheckInService
    {
        public int CheckOutCalls { get; private set; }

        public Task<TechnicianSelfStatusDto> CheckOutAsync(Guid technicianId, CheckOutRequest request, CancellationToken cancellationToken = default)
        {
            CheckOutCalls++;
            return Task.FromResult(new TechnicianSelfStatusDto());
        }

        public Task<TechnicianSelfStatusDto> GetMyStatusAsync(Guid technicianId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TechnicianSelfStatusDto> CheckInAsync(Guid technicianId, CheckInRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<PendingInstallationDto>> ListPendingInstallationsAsync(Guid technicianId, int? page, int? pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
