using NOIR.Application.Features.Hr.Commands.ReactivateEmployee;

namespace NOIR.Application.UnitTests.Features.Hr.Commands.ReactivateEmployee;

/// <summary>
/// Unit tests for ReactivateEmployeeCommandValidator.
/// Tests all validation rules for reactivating an employee.
/// </summary>
public class ReactivateEmployeeCommandValidatorTests
{
    private readonly ReactivateEmployeeCommandValidator _validator = new();

    #region Id Validation

    [Fact]
    public async Task Validate_WhenIdIsEmpty_ShouldHaveError()
    {
        // Arrange
        var command = new ReactivateEmployeeCommand(Guid.Empty);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorMessage("Employee ID is required.");
    }

    [Fact]
    public async Task Validate_WhenIdIsValid_ShouldNotHaveError()
    {
        // Arrange
        var command = new ReactivateEmployeeCommand(Guid.NewGuid());

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region Valid Command Tests

    [Fact]
    public async Task Validate_WhenCommandIsValid_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var command = new ReactivateEmployeeCommand(Guid.NewGuid());

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
