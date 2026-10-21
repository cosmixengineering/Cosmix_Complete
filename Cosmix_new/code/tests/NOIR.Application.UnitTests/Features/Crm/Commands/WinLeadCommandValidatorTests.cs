using NOIR.Application.Features.Crm.Commands.WinLead;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class WinLeadCommandValidatorTests
{
    private readonly WinLeadCommandValidator _validator;

    public WinLeadCommandValidatorTests()
    {
        _validator = new WinLeadCommandValidator();
    }

    [Fact]
    public void Validate_ValidLeadId_ShouldPass()
    {
        // Arrange
        var command = new WinLeadCommand(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyLeadId_ShouldFail()
    {
        // Arrange
        var command = new WinLeadCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LeadId);
    }
}
