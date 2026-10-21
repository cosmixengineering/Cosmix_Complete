using NOIR.Application.Features.Crm.Commands.DeleteCompany;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class DeleteCompanyCommandValidatorTests
{
    private readonly DeleteCompanyCommandValidator _validator;

    public DeleteCompanyCommandValidatorTests()
    {
        _validator = new DeleteCompanyCommandValidator();
    }

    [Fact]
    public void Validate_ValidId_ShouldPass()
    {
        // Arrange
        var command = new DeleteCompanyCommand(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyId_ShouldFail()
    {
        // Arrange
        var command = new DeleteCompanyCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
}
