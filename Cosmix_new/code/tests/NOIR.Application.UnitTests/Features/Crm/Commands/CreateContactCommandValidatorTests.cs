using NOIR.Application.Features.Crm.Commands.CreateContact;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class CreateContactCommandValidatorTests
{
    private readonly CreateContactCommandValidator _validator;

    public CreateContactCommandValidatorTests()
    {
        _validator = new CreateContactCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new CreateContactCommand(
            "John", "Doe", "john@example.com", ContactSource.Web,
            Phone: "555-0100", JobTitle: "CTO");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_MissingFirstName_ShouldFail()
    {
        // Arrange
        var command = new CreateContactCommand(
            "", "Doe", "john@example.com", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_MissingLastName_ShouldFail()
    {
        // Arrange
        var command = new CreateContactCommand(
            "John", "", "john@example.com", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Validate_MissingEmail_ShouldFail()
    {
        // Arrange
        var command = new CreateContactCommand(
            "John", "Doe", "", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_InvalidEmail_ShouldFail()
    {
        // Arrange
        var command = new CreateContactCommand(
            "John", "Doe", "not-an-email", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_FirstNameTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateContactCommand(
            new string('A', 101), "Doe", "john@example.com", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_NotesTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateContactCommand(
            "John", "Doe", "john@example.com", ContactSource.Web,
            Notes: new string('X', 2001));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }
}
