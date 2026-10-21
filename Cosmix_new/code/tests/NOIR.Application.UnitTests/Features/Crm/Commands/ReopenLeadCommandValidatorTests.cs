using NOIR.Application.Features.Crm.Commands.ReopenLead;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class ReopenLeadCommandValidatorTests
{
    private readonly ReopenLeadCommandValidator _validator;

    public ReopenLeadCommandValidatorTests()
    {
        _validator = new ReopenLeadCommandValidator();
    }

    [Fact]
    public void Validate_ValidLeadId_ShouldPass()
    {
        // Arrange
        var command = new ReopenLeadCommand(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyLeadId_ShouldFail()
    {
        // Arrange
        var command = new ReopenLeadCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LeadId);
    }
}
