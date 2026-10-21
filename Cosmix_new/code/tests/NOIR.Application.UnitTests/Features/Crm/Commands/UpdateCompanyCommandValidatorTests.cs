using NOIR.Application.Features.Crm.Commands.UpdateCompany;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class UpdateCompanyCommandValidatorTests
{
    private readonly UpdateCompanyCommandValidator _validator;

    public UpdateCompanyCommandValidatorTests()
    {
        _validator = new UpdateCompanyCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new UpdateCompanyCommand(
            Guid.NewGuid(), "Acme Corp",
            Domain: "acme.com",
            Industry: "Technology");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyId_ShouldFail()
    {
        // Arrange
        var command = new UpdateCompanyCommand(Guid.Empty, "Acme Corp");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Validate_EmptyName_ShouldFail()
    {
        // Arrange
        var command = new UpdateCompanyCommand(Guid.NewGuid(), "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_NameTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateCompanyCommand(Guid.NewGuid(), new string('A', 201));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_DomainTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateCompanyCommand(Guid.NewGuid(), "Acme", Domain: new string('x', 101));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Domain);
    }

    [Fact]
    public void Validate_IndustryTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateCompanyCommand(Guid.NewGuid(), "Acme", Industry: new string('x', 101));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Industry);
    }

    [Fact]
    public void Validate_AddressTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateCompanyCommand(Guid.NewGuid(), "Acme", Address: new string('x', 501));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Address);
    }

    [Fact]
    public void Validate_PhoneTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateCompanyCommand(Guid.NewGuid(), "Acme", Phone: new string('5', 21));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void Validate_WebsiteTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateCompanyCommand(Guid.NewGuid(), "Acme", Website: new string('x', 257));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Website);
    }

    [Fact]
    public void Validate_TaxIdTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateCompanyCommand(Guid.NewGuid(), "Acme", TaxId: new string('x', 51));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TaxId);
    }

    [Fact]
    public void Validate_NegativeEmployeeCount_ShouldFail()
    {
        // Arrange
        var command = new UpdateCompanyCommand(Guid.NewGuid(), "Acme", EmployeeCount: -1);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EmployeeCount);
    }

    [Fact]
    public void Validate_NotesTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateCompanyCommand(Guid.NewGuid(), "Acme", Notes: new string('x', 2001));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }
}
