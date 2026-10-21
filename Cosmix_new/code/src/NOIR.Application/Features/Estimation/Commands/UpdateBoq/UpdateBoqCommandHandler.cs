namespace NOIR.Application.Features.Estimation.Commands.UpdateBoq;

/// <summary>
/// Wolverine handler for updating a BOQ header.
/// </summary>
public class UpdateBoqCommandHandler
{
    private readonly IRepository<Boq, Guid> _boqRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public UpdateBoqCommandHandler(
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
        UpdateBoqCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new BoqByIdForUpdateSpec(command.Id);
        var boq = await _boqRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (boq == null)
        {
            return Result.Failure<BoqDto>(
                Error.NotFound($"BOQ with ID '{command.Id}' was not found.", "NOIR-EST-001"));
        }

        boq.UpdateHeader(
            command.Title,
            command.Description,
            command.CustomerId,
            command.ProjectId,
            command.MarkupPercent,
            command.Notes);

        boq.RecalculateTotals();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _entityUpdateHub.PublishEntityUpdatedAsync(
            entityType: "Boq",
            entityId: boq.Id,
            operation: EntityOperation.Updated,
            tenantId: _currentUser.TenantId!,
            cancellationToken);

        return Result.Success(BoqMapper.ToDto(boq));
    }
}
