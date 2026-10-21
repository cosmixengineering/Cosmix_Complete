using NOIR.Application.Features.Crm.Commands.UpdateContact;
using NOIR.Domain.Entities.Crm;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class UpdateContactCommandValidatorTests
{
    private readonly UpdateContactCommandValidator _validator;

    public UpdateContactCommandValidatorTests()
    {
        _validator = new UpdateContactCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new UpdateContactCommand(
            Guid.NewGuid(), "John", "Doe", "john@example.com", ContactSource.Web,
            Phone: "555-0100", JobTitle: "CTO");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyId_ShouldFail()
    {
        // Arrange
        var command = new UpdateContactCommand(
            Guid.Empty, "John", "Doe", "john@example.com", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Validate_EmptyFirstName_ShouldFail()
    {
        // Arrange
        var command = new UpdateContactCommand(
            Guid.NewGuid(), "", "Doe", "john@example.com", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_EmptyLastName_ShouldFail()
    {
        // Arrange
        var command = new UpdateContactCommand(
            Guid.NewGuid(), "John", "", "john@example.com", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Validate_EmptyEmail_ShouldFail()
    {
        // Arrange
        var command = new UpdateContactCommand(
            Guid.NewGuid(), "John", "Doe", "", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_InvalidEmail_ShouldFail()
    {
        // Arrange
        var command = new UpdateContactCommand(
            Guid.NewGuid(), "John", "Doe", "not-an-email", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_FirstNameTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateContactCommand(
            Guid.NewGuid(), new string('A', 101), "Doe", "john@example.com", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_LastNameTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateContactCommand(
            Guid.NewGuid(), "John", new string('A', 101), "john@example.com", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Validate_EmailTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateContactCommand(
            Guid.NewGuid(), "John", "Doe", new string('a', 251) + "@x.com", ContactSource.Web);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_PhoneTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateContactCommand(
            Guid.NewGuid(), "John", "Doe", "john@example.com", ContactSource.Web,
            Phone: new string('5', 21));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void Validate_JobTitleTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateContactCommand(
            Guid.NewGuid(), "John", "Doe", "john@example.com", ContactSource.Web,
            JobTitle: new string('x', 101));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.JobTitle);
    }

    [Fact]
    public void Validate_NotesTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateContactCommand(
            Guid.NewGuid(), "John", "Doe", "john@example.com", ContactSource.Web,
            Notes: new string('x', 2001));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }
}
