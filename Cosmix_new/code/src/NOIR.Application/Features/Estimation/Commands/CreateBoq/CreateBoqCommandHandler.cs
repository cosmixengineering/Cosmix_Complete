namespace NOIR.Application.Features.Estimation.Commands.CreateBoq;

/// <summary>
/// Wolverine handler for creating a new BOQ.
/// </summary>
public class CreateBoqCommandHandler
{
    private readonly IRepository<Boq, Guid> _boqRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public CreateBoqCommandHandler(
        IRepository<Boq, Guid> boqRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IEntityUpdateHubContext entityUpdateHub)
    {
        _boqRepository = boqRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _entityUpdateHub = entityUpdateHub;
    }

    public async Task<Result<BoqDto>> Handle(
        CreateBoqCommand command,
        CancellationToken cancellationToken)
    {
        // Check if BOQ number already exists (unique per tenant)
        var numberSpec = new BoqNumberExistsSpec(command.BoqNumber);
        var numberExists = await _boqRepository.AnyAsync(numberSpec, cancellationToken);
        if (numberExists)
        {
            return Result.Failure<BoqDto>(
                Error.Conflict($"A BOQ with number '{command.BoqNumber}' already exists.", "NOIR-EST-002"));
        }

        var boq = Boq.Create(
            command.BoqNumber,
            command.Title,
            command.CustomerId,
            command.ProjectId,
            command.Currency,
            command.Notes,
            _currentUser.TenantId);

        boq.UpdateHeader(
            command.Title,
            command.Description,
            command.CustomerId,
            command.ProjectId,
            command.MarkupPercent,
            command.Notes);

        boq.RecalculateTotals();

        await _boqRepository.AddAsync(boq, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "Boq",
            entityId: boq.Id,
            operation: EntityOperation.Created,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        return Result.Success(BoqMapper.ToDto(boq));
    }
}
