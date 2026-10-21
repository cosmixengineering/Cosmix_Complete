using NOIR.Application.Features.Crm.Commands.CreateLead;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class CreateLeadCommandValidatorTests
{
    private readonly CreateLeadCommandValidator _validator;

    public CreateLeadCommandValidatorTests()
    {
        _validator = new CreateLeadCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new CreateLeadCommand(
            "Enterprise Deal", Guid.NewGuid(), Guid.NewGuid(),
            Value: 50000m, Currency: "USD");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_MissingTitle_ShouldFail()
    {
        // Arrange
        var command = new CreateLeadCommand(
            "", Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_NegativeValue_ShouldFail()
    {
        // Arrange
        var command = new CreateLeadCommand(
            "Deal", Guid.NewGuid(), Guid.NewGuid(), Value: -100m);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Value);
    }

    [Fact]
    public void Validate_EmptyContactId_ShouldFail()
    {
        // Arrange
        var command = new CreateLeadCommand(
            "Deal", Guid.Empty, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ContactId);
    }

    [Fact]
    public void Validate_EmptyPipelineId_ShouldFail()
    {
        // Arrange
        var command = new CreateLeadCommand(
            "Deal", Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PipelineId);
    }

    [Fact]
    public void Validate_TitleTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateLeadCommand(
            new string('A', 201), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_ZeroValue_ShouldPass()
    {
        // Arrange
        var command = new CreateLeadCommand(
            "Deal", Guid.NewGuid(), Guid.NewGuid(), Value: 0m);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Value);
    }

    [Fact]
    public void Validate_TitleAtMaxLength_ShouldPass()
    {
        // Arrange
        var command = new CreateLeadCommand(
            new string('A', 200), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_NullCompanyId_ShouldPass()
    {
        // Arrange
        var command = new CreateLeadCommand(
            "Deal", Guid.NewGuid(), Guid.NewGuid(), CompanyId: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.CompanyId);
    }

    [Fact]
    public void Validate_EmptyCurrency_ShouldFail()
    {
        // Arrange
        var command = new CreateLeadCommand(
            "Deal", Guid.NewGuid(), Guid.NewGuid(), Currency: "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Validate_CurrencyTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateLeadCommand(
            "Deal", Guid.NewGuid(), Guid.NewGuid(), Currency: "ABCD");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Validate_CurrencyAtMaxLength_ShouldPass()
    {
        // Arrange
        var command = new CreateLeadCommand(
            "Deal", Guid.NewGuid(), Guid.NewGuid(), Currency: "USD");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Validate_NotesTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateLeadCommand(
            "Deal", Guid.NewGuid(), Guid.NewGuid(), Notes: new string('x', 2001));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void Validate_NotesAtMaxLength_ShouldPass()
    {
        // Arrange
        var command = new CreateLeadCommand(
            "Deal", Guid.NewGuid(), Guid.NewGuid(), Notes: new string('x', 2000));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }
}
