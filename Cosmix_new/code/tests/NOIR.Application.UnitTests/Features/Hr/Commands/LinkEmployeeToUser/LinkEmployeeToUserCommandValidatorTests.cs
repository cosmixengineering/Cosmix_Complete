using NOIR.Application.Features.Hr.Commands.LinkEmployeeToUser;

namespace NOIR.Application.UnitTests.Features.Hr.Commands.LinkEmployeeToUser;

/// <summary>
/// Unit tests for LinkEmployeeToUserCommandValidator.
/// Tests all validation rules for linking an employee to a user account.
/// </summary>
public class LinkEmployeeToUserCommandValidatorTests
{
    private readonly LinkEmployeeToUserCommandValidator _validator = new();

    #region EmployeeId Validation

    [Fact]
    public async Task Validate_WhenEmployeeIdIsEmpty_ShouldHaveError()
    {
        // Arrange
        var command = new LinkEmployeeToUserCommand(Guid.Empty, "user-123");

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EmployeeId)
            .WithErrorMessage("Employee ID is required.");
    }

    [Fact]
    public async Task Validate_WhenEmployeeIdIsValid_ShouldNotHaveError()
    {
        // Arrange
        var command = new LinkEmployeeToUserCommand(Guid.NewGuid(), "user-123");

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.EmployeeId);
    }

    #endregion

    #region TargetUserId Validation

    [Fact]
    public async Task Validate_WhenTargetUserIdIsEmpty_ShouldHaveError()
    {
        // Arrange
        var command = new LinkEmployeeToUserCommand(Guid.NewGuid(), string.Empty);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TargetUserId)
            .WithErrorMessage("User ID is required.");
    }

    [Fact]
    public async Task Validate_WhenTargetUserIdIsValid_ShouldNotHaveError()
    {
        // Arrange
        var command = new LinkEmployeeToUserCommand(Guid.NewGuid(), "user-abc-123");

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TargetUserId);
    }

    #endregion

    #region Valid Command Tests

    [Fact]
    public async Task Validate_WhenCommandIsValid_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var command = new LinkEmployeeToUserCommand(Guid.NewGuid(), "user-123");

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
