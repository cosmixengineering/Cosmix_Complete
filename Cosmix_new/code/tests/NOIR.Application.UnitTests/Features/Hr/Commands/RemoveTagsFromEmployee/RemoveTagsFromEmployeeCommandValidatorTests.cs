using NOIR.Application.Features.Hr.Commands.RemoveTagsFromEmployee;

namespace NOIR.Application.UnitTests.Features.Hr.Commands.RemoveTagsFromEmployee;

/// <summary>
/// Unit tests for RemoveTagsFromEmployeeCommandValidator.
/// Tests all validation rules for removing tags from an employee.
/// </summary>
public class RemoveTagsFromEmployeeCommandValidatorTests
{
    private readonly RemoveTagsFromEmployeeCommandValidator _validator = new();

    #region EmployeeId Validation

    [Fact]
    public async Task Validate_WhenEmployeeIdIsEmpty_ShouldHaveError()
    {
        // Arrange
        var command = new RemoveTagsFromEmployeeCommand(Guid.Empty, new List<Guid> { Guid.NewGuid() });

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
        var command = new RemoveTagsFromEmployeeCommand(Guid.NewGuid(), new List<Guid> { Guid.NewGuid() });

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.EmployeeId);
    }

    #endregion

    #region TagIds Validation

    [Fact]
    public async Task Validate_WhenTagIdsIsEmpty_ShouldHaveError()
    {
        // Arrange
        var command = new RemoveTagsFromEmployeeCommand(Guid.NewGuid(), new List<Guid>());

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TagIds)
            .WithErrorMessage("At least one tag ID is required.");
    }

    [Fact]
    public async Task Validate_WhenSingleTagId_ShouldNotHaveError()
    {
        // Arrange
        var command = new RemoveTagsFromEmployeeCommand(Guid.NewGuid(), new List<Guid> { Guid.NewGuid() });

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
        var command = new RemoveTagsFromEmployeeCommand(
            Guid.NewGuid(),
            new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WhenMultipleValidTagIds_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var command = new RemoveTagsFromEmployeeCommand(
            Guid.NewGuid(),
            new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() });

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
