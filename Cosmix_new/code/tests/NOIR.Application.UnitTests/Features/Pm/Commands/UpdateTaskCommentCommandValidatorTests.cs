using NOIR.Application.Features.Pm.Commands.UpdateTaskComment;

namespace NOIR.Application.UnitTests.Features.Pm.Commands;

public class UpdateTaskCommentCommandValidatorTests
{
    private readonly UpdateTaskCommentCommandValidator _validator;

    public UpdateTaskCommentCommandValidatorTests()
    {
        _validator = new UpdateTaskCommentCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new UpdateTaskCommentCommand(Guid.NewGuid(), Guid.NewGuid(), "Updated comment");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyTaskId_ShouldFail()
    {
        // Arrange
        var command = new UpdateTaskCommentCommand(Guid.Empty, Guid.NewGuid(), "Updated comment");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TaskId);
    }

    [Fact]
    public void Validate_EmptyCommentId_ShouldFail()
    {
        // Arrange
        var command = new UpdateTaskCommentCommand(Guid.NewGuid(), Guid.Empty, "Updated comment");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CommentId);
    }

    [Fact]
    public void Validate_EmptyContent_ShouldFail()
    {
        // Arrange
        var command = new UpdateTaskCommentCommand(Guid.NewGuid(), Guid.NewGuid(), "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }
}
