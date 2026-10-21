using NOIR.Application.Features.Crm.Commands.DeleteActivity;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class DeleteActivityCommandValidatorTests
{
    private readonly DeleteActivityCommandValidator _validator;

    public DeleteActivityCommandValidatorTests()
    {
        _validator = new DeleteActivityCommandValidator();
    }

    [Fact]
    public void Validate_ValidId_ShouldPass()
    {
        // Arrange
        var command = new DeleteActivityCommand(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyId_ShouldFail()
    {
        // Arrange
        var command = new DeleteActivityCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
}
