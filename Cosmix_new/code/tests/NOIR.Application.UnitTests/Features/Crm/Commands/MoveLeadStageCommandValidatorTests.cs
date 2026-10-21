using NOIR.Application.Features.Crm.Commands.MoveLeadStage;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class MoveLeadStageCommandValidatorTests
{
    private readonly MoveLeadStageCommandValidator _validator;

    public MoveLeadStageCommandValidatorTests()
    {
        _validator = new MoveLeadStageCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new MoveLeadStageCommand(Guid.NewGuid(), Guid.NewGuid(), 1.0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyLeadId_ShouldFail()
    {
        // Arrange
        var command = new MoveLeadStageCommand(Guid.Empty, Guid.NewGuid(), 1.0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LeadId);
    }

    [Fact]
    public void Validate_EmptyNewStageId_ShouldFail()
    {
        // Arrange
        var command = new MoveLeadStageCommand(Guid.NewGuid(), Guid.Empty, 1.0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewStageId);
    }
}
