using NOIR.Application.Features.Hr.Commands.DeactivateEmployee;

namespace NOIR.Application.UnitTests.Features.Hr.Commands.DeactivateEmployee;

/// <summary>
/// Unit tests for DeactivateEmployeeCommandValidator.
/// Tests all validation rules for deactivating an employee.
/// </summary>
public class DeactivateEmployeeCommandValidatorTests
{
    private readonly DeactivateEmployeeCommandValidator _validator = new();

    #region Id Validation

    [Fact]
    public async Task Validate_WhenIdIsEmpty_ShouldHaveError()
    {
        // Arrange
        var command = new DeactivateEmployeeCommand(Guid.Empty, EmployeeStatus.Resigned);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorMessage("Employee ID is required.");
    }

    [Fact]
    public async Task Validate_WhenIdIsValid_ShouldNotHaveIdError()
    {
        // Arrange
        var command = new DeactivateEmployeeCommand(Guid.NewGuid(), EmployeeStatus.Resigned);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Id);
    }

    #endregion

    #region Status Validation

    [Fact]
    public async Task Validate_WhenStatusIsResigned_ShouldNotHaveError()
    {
        // Arrange
        var command = new DeactivateEmployeeCommand(Guid.NewGuid(), EmployeeStatus.Resigned);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenStatusIsTerminated_ShouldNotHaveError()
    {
        // Arrange
        var command = new DeactivateEmployeeCommand(Guid.NewGuid(), EmployeeStatus.Terminated);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenStatusIsActive_ShouldHaveError()
    {
        // Arrange
        var command = new DeactivateEmployeeCommand(Guid.NewGuid(), EmployeeStatus.Active);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage("Status must be Resigned or Terminated for deactivation.");
    }

    [Fact]
    public async Task Validate_WhenStatusIsSuspended_ShouldHaveError()
    {
        // Arrange
        var command = new DeactivateEmployeeCommand(Guid.NewGuid(), EmployeeStatus.Suspended);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage("Status must be Resigned or Terminated for deactivation.");
    }

    [Fact]
    public async Task Validate_WhenStatusIsInvalidEnumValue_ShouldHaveError()
    {
        // Arrange
        var command = new DeactivateEmployeeCommand(Guid.NewGuid(), (EmployeeStatus)999);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    #endregion
}
