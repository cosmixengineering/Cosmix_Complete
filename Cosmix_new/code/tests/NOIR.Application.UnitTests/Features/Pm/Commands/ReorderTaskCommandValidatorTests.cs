using NOIR.Application.Features.Pm.Commands.ReorderTask;

namespace NOIR.Application.UnitTests.Features.Pm.Commands;

public class ReorderTaskCommandValidatorTests
{
    private readonly ReorderTaskCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        var command = new ReorderTaskCommand(Guid.NewGuid(), 5.0);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyTaskId_ShouldFail()
    {
        var command = new ReorderTaskCommand(Guid.Empty, 1.0);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.TaskId);
    }

    [Fact]
    public void Validate_ValidTaskId_ShouldPass()
    {
        var command = new ReorderTaskCommand(Guid.NewGuid(), 0.0);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.TaskId);
    }

    [Fact]
    public void Validate_NegativeSortOrder_ShouldFail()
    {
        var command = new ReorderTaskCommand(Guid.NewGuid(), -1.0);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.NewSortOrder);
    }

    [Fact]
    public void Validate_ZeroSortOrder_ShouldPass()
    {
        var command = new ReorderTaskCommand(Guid.NewGuid(), 0.0);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.NewSortOrder);
    }

    [Fact]
    public void Validate_LargeSortOrder_ShouldPass()
    {
        var command = new ReorderTaskCommand(Guid.NewGuid(), 999999.0);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
