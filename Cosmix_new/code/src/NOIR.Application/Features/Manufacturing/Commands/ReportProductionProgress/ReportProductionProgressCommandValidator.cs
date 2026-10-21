namespace NOIR.Application.Features.Manufacturing.Commands.ReportProductionProgress;

/// <summary>
/// Validator for ReportProductionProgressCommand.
/// Quantities are increments (>= 0), with at least one greater than zero.
/// </summary>
public class ReportProductionProgressCommandValidator : AbstractValidator<ReportProductionProgressCommand>
{
    public ReportProductionProgressCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Production order ID is required.");

        RuleFor(x => x.ProducedQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Produced quantity cannot be negative.");

        RuleFor(x => x.ScrapQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Scrap quantity cannot be negative.");

        RuleFor(x => x)
            .Must(x => x.ProducedQuantity > 0 || x.ScrapQuantity > 0)
            .WithMessage("At least one progress quantity must be greater than zero.");
    }
}
