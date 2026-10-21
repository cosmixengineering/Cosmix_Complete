namespace NOIR.Application.Features.Crm.Commands.DeletePipeline;

public sealed class DeletePipelineCommandValidator : AbstractValidator<DeletePipelineCommand>
{
    public DeletePipelineCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Pipeline ID is required.");
    }
}
