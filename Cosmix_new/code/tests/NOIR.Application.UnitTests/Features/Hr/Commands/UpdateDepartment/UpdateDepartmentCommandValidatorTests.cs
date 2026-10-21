using NOIR.Application.Features.Hr.Commands.UpdateDepartment;

namespace NOIR.Application.UnitTests.Features.Hr.Commands.UpdateDepartment;

public class UpdateDepartmentCommandValidatorTests
{
    private readonly UpdateDepartmentCommandValidator _validator;

    public UpdateDepartmentCommandValidatorTests()
    {
        _validator = new UpdateDepartmentCommandValidator();
    }

    private static UpdateDepartmentCommand CreateValidCommand() =>
        new(Id: Guid.NewGuid(), Name: "Engineering", Code: "ENG");

    [Fact]
    public async Task Validate_WithValidCommand_ShouldPass()
    {
        var result = await _validator.TestValidateAsync(CreateValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WithEmptyId_ShouldFail()
    {
        var command = CreateValidCommand() with { Id = Guid.Empty };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
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
    [InlineData("ENG@01")]
    public async Task Validate_WithCodeContainingInvalidChars_ShouldFail(string code)
    {
        var command = CreateValidCommand() with { Code = code };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public async Task Validate_WithDescriptionExceeding1000Chars_ShouldFail()
    {
        var command = CreateValidCommand() with { Description = new string('a', 1001) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }
}
