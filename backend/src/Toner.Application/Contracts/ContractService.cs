using Microsoft.EntityFrameworkCore;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Contracts.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Contracts;

public class ContractService : IContractService
{
    private readonly IApplicationDbContext _db;

    public ContractService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ContractDto> CreateAsync(CreateContractRequest request, CancellationToken cancellationToken = default)
    {
        var contract = new Contract
        {
            ClientId = request.ClientId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = ContractStatus.Activo,
            IncludedPrintsPerMonth = request.IncludedPrintsPerMonth,
            PricePerExtraPage = request.PricePerExtraPage,
            Notes = request.Notes?.Trim()
        };

        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(contract.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<ContractDto>> ListAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default)
    {
        var query = Projected(_db);

        if (!requestingUser.IsStaff)
        {
            var clientId = requestingUser.RequireClientId();
            query = query.Where(c => c.ClientId == clientId);
        }

        return await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<ContractDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default)
    {
        var contract = await Projected(_db).FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Contract), id);

        if (!requestingUser.IsStaff && contract.ClientId != requestingUser.RequireClientId())
        {
            throw new ForbiddenException("Este contrato no pertenece a tu cliente.");
        }

        return contract;
    }

    public async Task<ContractDto> UpdateAsync(Guid id, UpdateContractRequest request, CancellationToken cancellationToken = default)
    {
        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Contract), id);

        contract.StartDate = request.StartDate;
        contract.EndDate = request.EndDate;
        contract.IncludedPrintsPerMonth = request.IncludedPrintsPerMonth;
        contract.PricePerExtraPage = request.PricePerExtraPage;
        contract.Notes = request.Notes?.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    public async Task<ContractDto> SetStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Contract), id);

        contract.Status = Enum.Parse<ContractStatus>(status);
        await _db.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(id, cancellationToken);
    }

    private async Task<ContractDto> ToDtoAsync(Guid id, CancellationToken cancellationToken) =>
        await Projected(_db).FirstAsync(c => c.Id == id, cancellationToken);

    private static IQueryable<ContractDto> Projected(IApplicationDbContext db) =>
        db.Contracts.Select(c => new ContractDto
        {
            Id = c.Id,
            ClientId = c.ClientId,
            ClientName = c.Client.Name,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            Status = c.Status.ToString(),
            IncludedPrintsPerMonth = c.IncludedPrintsPerMonth,
            PricePerExtraPage = c.PricePerExtraPage,
            Notes = c.Notes,
            AssetCount = c.ContractAssets.Count(ca => ca.EndDate == null),
            CityNames = c.Client.Locations.Select(l => l.City.Name).Distinct().OrderBy(n => n).ToList(),
            CreatedAt = c.CreatedAt
        });
}
