namespace NOIR.Application.Features.Crm.Commands.UpdatePipeline;

public sealed class UpdatePipelineCommandValidator : AbstractValidator<UpdatePipelineCommand>
{
    public UpdatePipelineCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Pipeline ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Pipeline name is required.")
            .MaximumLength(100).WithMessage("Pipeline name cannot exceed 100 characters.");

        RuleFor(x => x.Stages)
            .NotEmpty().WithMessage("At least one stage is required.")
            .Must(s => s.Count >= 1).WithMessage("At least one stage is required.");

        RuleForEach(x => x.Stages).ChildRules(stage =>
        {
            stage.RuleFor(s => s.Name)
                .NotEmpty().WithMessage("Stage name is required.")
                .MaximumLength(100).WithMessage("Stage name cannot exceed 100 characters.");

            stage.RuleFor(s => s.Color)
                .MaximumLength(7).WithMessage("Color must be a valid hex color (e.g. #3B82F6).");
        });
    }
}
