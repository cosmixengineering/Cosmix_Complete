using NOIR.Application.Features.Pm.Commands.CreateProject;

namespace NOIR.Application.UnitTests.Features.Pm.Commands;

public class CreateProjectCommandValidatorTests
{
    private readonly CreateProjectCommandValidator _validator;

    public CreateProjectCommandValidatorTests()
    {
        _validator = new CreateProjectCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new CreateProjectCommand("My Project", Description: "Test");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        // Arrange
        var command = new CreateProjectCommand("");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameExceedsMaxLength_ShouldFail()
    {
        // Arrange
        var command = new CreateProjectCommand(new string('A', 201));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_DescriptionExceedsMaxLength_ShouldFail()
    {
        // Arrange
        var command = new CreateProjectCommand("Valid Name", Description: new string('A', 2001));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_NegativeBudget_ShouldFail()
    {
        // Arrange
        var command = new CreateProjectCommand("Valid Name", Budget: -100m);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Budget);
    }

    [Fact]
    public void Validate_ZeroBudget_ShouldPass()
    {
        // Arrange
        var command = new CreateProjectCommand("Valid Name", Budget: 0m);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Budget);
    }
}
