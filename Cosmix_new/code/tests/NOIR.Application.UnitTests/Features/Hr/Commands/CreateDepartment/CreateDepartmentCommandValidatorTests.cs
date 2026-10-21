using NOIR.Application.Features.Hr.Commands.CreateDepartment;

namespace NOIR.Application.UnitTests.Features.Hr.Commands.CreateDepartment;

public class CreateDepartmentCommandValidatorTests
{
    private readonly CreateDepartmentCommandValidator _validator;

    public CreateDepartmentCommandValidatorTests()
    {
        _validator = new CreateDepartmentCommandValidator();
    }

    private static CreateDepartmentCommand CreateValidCommand() =>
        new(Name: "Engineering", Code: "ENG", Description: "Engineering Department");

    [Fact]
    public async Task Validate_WithValidCommand_ShouldPass()
    {
        var result = await _validator.TestValidateAsync(CreateValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithInvalidName_ShouldFail(string? name)
    {
        var command = CreateValidCommand() with { Name = name! };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Validate_WithNameExceeding200Chars_ShouldFail()
    {
        var command = CreateValidCommand() with { Name = new string('a', 201) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithInvalidCode_ShouldFail(string? code)
    {
        var command = CreateValidCommand() with { Code = code! };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public async Task Validate_WithCodeExceeding20Chars_ShouldFail()
    {
        var command = CreateValidCommand() with { Code = new string('A', 21) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Theory]
    [InlineData("ENG 01")]
    [InlineData("ENG.01")]
    [InlineData("ENG@01")]
    public async Task Validate_WithCodeContainingInvalidChars_ShouldFail(string code)
    {
        var command = CreateValidCommand() with { Code = code };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Theory]
    [InlineData("ENG")]
    [InlineData("ENG-01")]
    [InlineData("eng01")]
    [InlineData("Eng-Sub-01")]
    public async Task Validate_WithValidCode_ShouldPass(string code)
    {
        var command = CreateValidCommand() with { Code = code };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public async Task Validate_WithDescriptionExceeding1000Chars_ShouldFail()
    {
        var command = CreateValidCommand() with { Description = new string('a', 1001) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public async Task Validate_WithNullDescription_ShouldPass()
    {
        var command = CreateValidCommand() with { Description = null };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
