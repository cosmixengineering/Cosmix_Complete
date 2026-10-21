using NOIR.Application.Features.Pm.Commands.UpdateTaskLabel;

namespace NOIR.Application.UnitTests.Features.Pm.Commands;

public class UpdateTaskLabelCommandValidatorTests
{
    private readonly UpdateTaskLabelCommandValidator _validator;

    public UpdateTaskLabelCommandValidatorTests()
    {
        _validator = new UpdateTaskLabelCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new UpdateTaskLabelCommand(Guid.NewGuid(), Guid.NewGuid(), "Bug", "#EF4444");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyProjectId_ShouldFail()
    {
        // Arrange
        var command = new UpdateTaskLabelCommand(Guid.Empty, Guid.NewGuid(), "Bug", "#EF4444");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ProjectId);
    }

    [Fact]
    public void Validate_EmptyLabelId_ShouldFail()
    {
        // Arrange
        var command = new UpdateTaskLabelCommand(Guid.NewGuid(), Guid.Empty, "Bug", "#EF4444");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LabelId);
    }

    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        // Arrange
        var command = new UpdateTaskLabelCommand(Guid.NewGuid(), Guid.NewGuid(), "", "#EF4444");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameExceedsMaxLength_ShouldFail()
    {
        // Arrange
        var command = new UpdateTaskLabelCommand(Guid.NewGuid(), Guid.NewGuid(), new string('A', 51), "#EF4444");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_EmptyColor_ShouldFail()
    {
        // Arrange
        var command = new UpdateTaskLabelCommand(Guid.NewGuid(), Guid.NewGuid(), "Bug", "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Color);
    }
}
