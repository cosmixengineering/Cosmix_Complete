namespace NOIR.Application.Features.Estimation.Commands.ApproveBoq;

/// <summary>
/// Wolverine handler for approving a BOQ. Captures a revision snapshot on approval.
/// </summary>
public class ApproveBoqCommandHandler
{
    private readonly IRepository<Boq, Guid> _boqRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IEntityUpdateHubContext _entityUpdateHub;

    public ApproveBoqCommandHandler(
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
        ApproveBoqCommand command,
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
                Error.Validation("Status", "Only BOQs under review can be approved.", "NOIR-EST-003"));
        }

        var previousTotal = boq.Revisions.Count == 0
            ? 0
            : boq.Revisions.Max(r => r.NewTotal);

        boq.Approve();

        var revision = BoqRevision.Create(
            boq.Id,
            boq.Revisions.Count + 1,
            previousTotal,
            boq.TotalAmount,
            reason: null,
            createdByUserId: _currentUser.UserId);

        boq.AddRevision(revision);

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
