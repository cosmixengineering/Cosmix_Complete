using NOIR.Application.Features.Crm.Commands.LoseLead;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class LoseLeadCommandValidatorTests
{
    private readonly LoseLeadCommandValidator _validator;

    public LoseLeadCommandValidatorTests()
    {
        _validator = new LoseLeadCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new LoseLeadCommand(Guid.NewGuid(), "Budget constraints");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyLeadId_ShouldFail()
    {
        // Arrange
        var command = new LoseLeadCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LeadId);
    }

    [Fact]
    public void Validate_NullReason_ShouldPass()
    {
        // Arrange
        var command = new LoseLeadCommand(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ReasonTooLong_ShouldFail()
    {
        // Arrange
        var command = new LoseLeadCommand(Guid.NewGuid(), new string('x', 501));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }
}
