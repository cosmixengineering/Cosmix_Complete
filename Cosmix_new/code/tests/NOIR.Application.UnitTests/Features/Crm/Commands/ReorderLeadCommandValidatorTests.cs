using NOIR.Application.Features.Crm.Commands.ReorderLead;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class ReorderLeadCommandValidatorTests
{
    private readonly ReorderLeadCommandValidator _validator;

    public ReorderLeadCommandValidatorTests()
    {
        _validator = new ReorderLeadCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new ReorderLeadCommand(Guid.NewGuid(), 1.5);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyLeadId_ShouldFail()
    {
        // Arrange
        var command = new ReorderLeadCommand(Guid.Empty, 1.0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LeadId);
    }
}
