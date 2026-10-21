using NOIR.Application.Features.Crm.Commands.DeletePipeline;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class DeletePipelineCommandValidatorTests
{
    private readonly DeletePipelineCommandValidator _validator;

    public DeletePipelineCommandValidatorTests()
    {
        _validator = new DeletePipelineCommandValidator();
    }

    [Fact]
    public void Validate_ValidId_ShouldPass()
    {
        // Arrange
        var command = new DeletePipelineCommand(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyId_ShouldFail()
    {
        // Arrange
        var command = new DeletePipelineCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
}
