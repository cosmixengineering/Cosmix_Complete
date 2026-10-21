using NOIR.Application.Features.Pm.Commands.AddSubtask;

namespace NOIR.Application.UnitTests.Features.Pm.Commands;

public class AddSubtaskCommandValidatorTests
{
    private readonly AddSubtaskCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        var command = new AddSubtaskCommand(Guid.NewGuid(), "Subtask Title", "Description", TaskPriority.Medium, null);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyParentTaskId_ShouldFail()
    {
        var command = new AddSubtaskCommand(Guid.Empty, "Subtask Title", null, TaskPriority.Low, null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ParentTaskId);
    }

    [Fact]
    public void Validate_EmptyTitle_ShouldFail()
    {
        var command = new AddSubtaskCommand(Guid.NewGuid(), "", null, TaskPriority.Low, null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_TitleExceedsMaxLength_ShouldFail()
    {
        var command = new AddSubtaskCommand(Guid.NewGuid(), new string('A', 201), null, TaskPriority.Low, null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_TitleAtMaxLength_ShouldPass()
    {
        var command = new AddSubtaskCommand(Guid.NewGuid(), new string('A', 200), null, TaskPriority.Low, null);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_WhitespaceTitle_ShouldFail()
    {
        var command = new AddSubtaskCommand(Guid.NewGuid(), "   ", null, TaskPriority.Low, null);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }
}
