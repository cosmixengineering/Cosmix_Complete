using NOIR.Application.Features.Pm.Commands.CreateTask;

namespace NOIR.Application.UnitTests.Features.Pm.Commands;

public class CreateTaskCommandValidatorTests
{
    private readonly CreateTaskCommandValidator _validator;

    public CreateTaskCommandValidatorTests()
    {
        _validator = new CreateTaskCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new CreateTaskCommand(Guid.NewGuid(), "Implement feature");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyTitle_ShouldFail()
    {
        // Arrange
        var command = new CreateTaskCommand(Guid.NewGuid(), "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_TitleExceedsMaxLength_ShouldFail()
    {
        // Arrange
        var command = new CreateTaskCommand(Guid.NewGuid(), new string('A', 501));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_EmptyProjectId_ShouldFail()
    {
        // Arrange
        var command = new CreateTaskCommand(Guid.Empty, "Valid Title");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ProjectId);
    }

    [Fact]
    public void Validate_DescriptionExceedsMaxLength_ShouldFail()
    {
        // Arrange
        var command = new CreateTaskCommand(Guid.NewGuid(), "Title", Description: new string('A', 5001));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_NegativeEstimatedHours_ShouldFail()
    {
        // Arrange
        var command = new CreateTaskCommand(Guid.NewGuid(), "Title", EstimatedHours: -1m);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EstimatedHours);
    }
}
