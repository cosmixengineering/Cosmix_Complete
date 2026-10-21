using NOIR.Application.Features.Pm.Commands.ChangeProjectStatus;

namespace NOIR.Application.UnitTests.Features.Pm.Commands;

public class ChangeProjectStatusCommandValidatorTests
{
    private readonly ChangeProjectStatusCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        var command = new ChangeProjectStatusCommand(Guid.NewGuid(), ProjectStatus.OnHold);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyProjectId_ShouldFail()
    {
        var command = new ChangeProjectStatusCommand(Guid.Empty, ProjectStatus.Active);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ProjectId);
    }

    [Fact]
    public void Validate_InvalidStatus_ShouldFail()
    {
        var command = new ChangeProjectStatusCommand(Guid.NewGuid(), (ProjectStatus)999);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.NewStatus);
    }

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.OnHold)]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Archived)]
    public void Validate_AllValidStatuses_ShouldPass(ProjectStatus status)
    {
        var command = new ChangeProjectStatusCommand(Guid.NewGuid(), status);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.NewStatus);
    }
}
