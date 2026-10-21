using NOIR.Application.Features.Crm.Commands.UpdateLead;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class UpdateLeadCommandValidatorTests
{
    private readonly UpdateLeadCommandValidator _validator;

    public UpdateLeadCommandValidatorTests()
    {
        _validator = new UpdateLeadCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), "Enterprise Deal", Guid.NewGuid(),
            Value: 50000, Currency: "USD");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyId_ShouldFail()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.Empty, "Deal", Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Validate_EmptyTitle_ShouldFail()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), "", Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_TitleTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), new string('A', 201), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), "Deal", Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_NegativeValue_ShouldFail()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), "Deal", Guid.NewGuid(), Value: -100);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Value);
    }

    [Fact]
    public void Validate_EmptyCurrency_ShouldFail()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), "Deal", Guid.NewGuid(), Currency: "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Validate_CurrencyTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), "Deal", Guid.NewGuid(), Currency: "ABCD");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Validate_NotesTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), "Deal", Guid.NewGuid(), Notes: new string('x', 2001));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void Validate_ZeroValue_ShouldPass()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), "Deal", Guid.NewGuid(), Value: 0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Value);
    }

    [Fact]
    public void Validate_TitleAtMaxLength_ShouldPass()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), new string('A', 200), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_CurrencyAtMaxLength_ShouldPass()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), "Deal", Guid.NewGuid(), Currency: "USD");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Validate_NotesAtMaxLength_ShouldPass()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), "Deal", Guid.NewGuid(), Notes: new string('x', 2000));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void Validate_NullCompanyId_ShouldPass()
    {
        // Arrange
        var command = new UpdateLeadCommand(
            Guid.NewGuid(), "Deal", Guid.NewGuid(), CompanyId: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.CompanyId);
    }
}
