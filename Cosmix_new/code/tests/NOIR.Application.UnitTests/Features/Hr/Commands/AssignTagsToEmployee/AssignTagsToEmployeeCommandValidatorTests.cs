using NOIR.Application.Features.Hr.Commands.AssignTagsToEmployee;

namespace NOIR.Application.UnitTests.Features.Hr.Commands.AssignTagsToEmployee;

/// <summary>
/// Unit tests for AssignTagsToEmployeeCommandValidator.
/// Tests all validation rules for assigning tags to an employee.
/// </summary>
public class AssignTagsToEmployeeCommandValidatorTests
{
    private readonly AssignTagsToEmployeeCommandValidator _validator = new();

    #region EmployeeId Validation

    [Fact]
    public async Task Validate_WhenEmployeeIdIsEmpty_ShouldHaveError()
    {
        // Arrange
        var command = new AssignTagsToEmployeeCommand(Guid.Empty, new List<Guid> { Guid.NewGuid() });

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
        var command = new AssignTagsToEmployeeCommand(Guid.NewGuid(), new List<Guid> { Guid.NewGuid() });

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
        var command = new AssignTagsToEmployeeCommand(Guid.NewGuid(), new List<Guid>());

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TagIds)
            .WithErrorMessage("At least one tag ID is required.");
    }

    [Fact]
    public async Task Validate_WhenTagIdsExceedsMaximum_ShouldHaveError()
    {
        // Arrange
        var tagIds = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToList();
        var command = new AssignTagsToEmployeeCommand(Guid.NewGuid(), tagIds);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TagIds)
            .WithErrorMessage("Cannot assign more than 50 tags at once.");
    }

    [Fact]
    public async Task Validate_WhenExactly50Tags_ShouldNotHaveError()
    {
        // Arrange
        var tagIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();
        var command = new AssignTagsToEmployeeCommand(Guid.NewGuid(), tagIds);

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
        var command = new AssignTagsToEmployeeCommand(
            Guid.NewGuid(),
            new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
