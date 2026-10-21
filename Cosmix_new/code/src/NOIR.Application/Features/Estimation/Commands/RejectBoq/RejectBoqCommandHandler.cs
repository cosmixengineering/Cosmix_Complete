namespace NOIR.Application.Features.Estimation.Commands.RejectBoq;

/// <summary>
/// Wolverine handler for rejecting a BOQ under review.
/// </summary>
public class RejectBoqCommandHandler
{
    private readonly IRepository<Boq, Guid> _boqRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public RejectBoqCommandHandler(
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
        RejectBoqCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new BoqByIdForUpdateSpec(command.Id);
        var boq = await _boqRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (boq == null)
        {
            return Result.Failure<BoqDto>(
                Error.NotFound($"BOQ with ID '{command.Id}' was not found.", "NOIR-EST-001"));
        }

        if (boq.Status != BoqStatus.UnderReview)
        {
            return Result.Failure<BoqDto>(
                Error.Validation("Status", "Only BOQs under review can be rejected.", "NOIR-EST-003"));
        }

        boq.Reject(command.Reason);

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
