using NOIR.Application.Features.Hr.Commands.UpdateEmployee;

namespace NOIR.Application.UnitTests.Features.Hr.Commands.UpdateEmployee;

public class UpdateEmployeeCommandValidatorTests
{
    private readonly UpdateEmployeeCommandValidator _validator;

    public UpdateEmployeeCommandValidatorTests()
    {
        _validator = new UpdateEmployeeCommandValidator();
    }

    private static UpdateEmployeeCommand CreateValidCommand() =>
        new(
            Id: Guid.NewGuid(),
            FirstName: "John",
            LastName: "Doe",
            Email: "john@example.com",
            DepartmentId: Guid.NewGuid(),
            EmploymentType: EmploymentType.FullTime);

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
    public async Task Validate_WithInvalidFirstName_ShouldFail(string? firstName)
    {
        var command = CreateValidCommand() with { FirstName = firstName! };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public async Task Validate_WithFirstNameExceeding100Chars_ShouldFail()
    {
        var command = CreateValidCommand() with { FirstName = new string('a', 101) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Validate_WithInvalidLastName_ShouldFail(string? lastName)
    {
        var command = CreateValidCommand() with { LastName = lastName! };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Validate_WithInvalidEmail_ShouldFail(string? email)
    {
        var command = CreateValidCommand() with { Email = email! };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public async Task Validate_WithEmptyDepartmentId_ShouldFail()
    {
        var command = CreateValidCommand() with { DepartmentId = Guid.Empty };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.DepartmentId);
    }

    [Fact]
    public async Task Validate_WithPhoneExceeding20Chars_ShouldFail()
    {
        var command = CreateValidCommand() with { Phone = new string('1', 21) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public async Task Validate_WithNullOptionalFields_ShouldPass()
    {
        var command = CreateValidCommand() with { Phone = null, Position = null, Notes = null };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
