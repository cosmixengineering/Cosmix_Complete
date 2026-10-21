namespace NOIR.Application.Features.Estimation.Queries.GetBoqById;

/// <summary>
/// Wolverine handler for getting a BOQ by ID.
/// </summary>
public class GetBoqByIdQueryHandler
{
    private readonly IRepository<Boq, Guid> _boqRepository;

    public GetBoqByIdQueryHandler(IRepository<Boq, Guid> boqRepository)
    {
        _boqRepository = boqRepository;
    }

    public async Task<Result<BoqDto>> Handle(
        GetBoqByIdQuery query,
        CancellationToken cancellationToken)
    {
        var spec = new BoqByIdSpec(query.Id);
        var boq = await _boqRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (boq == null)
        {
            return Result.Failure<BoqDto>(
                Error.NotFound($"BOQ with ID '{query.Id}' was not found.", "NOIR-EST-001"));
        }

        return Result.Success(BoqMapper.ToDto(boq));
    }
}
