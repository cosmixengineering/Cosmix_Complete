using NOIR.Application.Features.Crm.Commands.CreateCompany;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class CreateCompanyCommandValidatorTests
{
    private readonly CreateCompanyCommandValidator _validator;

    public CreateCompanyCommandValidatorTests()
    {
        _validator = new CreateCompanyCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new CreateCompanyCommand(
            "Acme Corp",
            Domain: "acme.com",
            Industry: "Technology",
            Phone: "555-0100");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        // Arrange
        var command = new CreateCompanyCommand("");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateCompanyCommand(new string('A', 201));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_DomainTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateCompanyCommand("Acme Corp", Domain: new string('x', 101));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Domain);
    }

    [Fact]
    public void Validate_IndustryTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateCompanyCommand("Acme Corp", Industry: new string('x', 101));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Industry);
    }

    [Fact]
    public void Validate_AddressTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateCompanyCommand("Acme Corp", Address: new string('x', 501));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Address);
    }

    [Fact]
    public void Validate_PhoneTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateCompanyCommand("Acme Corp", Phone: new string('5', 21));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void Validate_WebsiteTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateCompanyCommand("Acme Corp", Website: new string('x', 257));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Website);
    }

    [Fact]
    public void Validate_TaxIdTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateCompanyCommand("Acme Corp", TaxId: new string('x', 51));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TaxId);
    }

    [Fact]
    public void Validate_NegativeEmployeeCount_ShouldFail()
    {
        // Arrange
        var command = new CreateCompanyCommand("Acme Corp", EmployeeCount: -1);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EmployeeCount);
    }

    [Fact]
    public void Validate_ZeroEmployeeCount_ShouldPass()
    {
        // Arrange
        var command = new CreateCompanyCommand("Acme Corp", EmployeeCount: 0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.EmployeeCount);
    }

    [Fact]
    public void Validate_NotesTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateCompanyCommand("Acme Corp", Notes: new string('x', 2001));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }
}
