using NOIR.Application.Features.Crm.Commands.DeleteContact;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class DeleteContactCommandValidatorTests
{
    private readonly DeleteContactCommandValidator _validator;

    public DeleteContactCommandValidatorTests()
    {
        _validator = new DeleteContactCommandValidator();
    }

    [Fact]
    public void Validate_ValidId_ShouldPass()
    {
        // Arrange
        var command = new DeleteContactCommand(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyId_ShouldFail()
    {
        // Arrange
        var command = new DeleteContactCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
}
