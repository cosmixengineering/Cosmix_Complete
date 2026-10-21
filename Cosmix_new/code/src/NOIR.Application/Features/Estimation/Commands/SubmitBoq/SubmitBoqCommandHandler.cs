namespace NOIR.Application.Features.Estimation.Commands.SubmitBoq;

/// <summary>
/// Wolverine handler for submitting a BOQ for review.
/// </summary>
public class SubmitBoqCommandHandler
{
    private readonly IRepository<Boq, Guid> _boqRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public SubmitBoqCommandHandler(
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
        SubmitBoqCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new BoqByIdForUpdateSpec(command.Id);
        var boq = await _boqRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (boq == null)
        {
            return Result.Failure<BoqDto>(
                Error.NotFound($"BOQ with ID '{command.Id}' was not found.", "NOIR-EST-001"));
        }

        if (boq.Status != BoqStatus.Draft)
        {
            return Result.Failure<BoqDto>(
                Error.Validation("Status", "Only draft BOQs can be submitted for review.", "NOIR-EST-003"));
        }

        boq.SubmitForReview();

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
