using NOIR.Application.Features.Hr.Commands.CreateEmployee;

namespace NOIR.Application.UnitTests.Features.Hr.Commands.CreateEmployee;

public class CreateEmployeeCommandValidatorTests
{
    private readonly CreateEmployeeCommandValidator _validator;

    public CreateEmployeeCommandValidatorTests()
    {
        _validator = new CreateEmployeeCommandValidator();
    }

    private static CreateEmployeeCommand CreateValidCommand() =>
        new(
            FirstName: "John",
            LastName: "Doe",
            Email: "john@example.com",
            DepartmentId: Guid.NewGuid(),
            JoinDate: DateTimeOffset.UtcNow,
            EmploymentType: EmploymentType.FullTime);

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
    [InlineData("   ")]
    public async Task Validate_WithInvalidLastName_ShouldFail(string? lastName)
    {
        var command = CreateValidCommand() with { LastName = lastName! };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public async Task Validate_WithLastNameExceeding100Chars_ShouldFail()
    {
        var command = CreateValidCommand() with { LastName = new string('a', 101) };
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
    public async Task Validate_WithEmailExceeding256Chars_ShouldFail()
    {
        var command = CreateValidCommand() with { Email = new string('a', 251) + "@b.com" };
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
    public async Task Validate_WithPositionExceeding200Chars_ShouldFail()
    {
        var command = CreateValidCommand() with { Position = new string('a', 201) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Position);
    }

    [Fact]
    public async Task Validate_WithNotesExceeding2000Chars_ShouldFail()
    {
        var command = CreateValidCommand() with { Notes = new string('a', 2001) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public async Task Validate_WithNullOptionalFields_ShouldPass()
    {
        var command = CreateValidCommand() with { Phone = null, Position = null, Notes = null, AvatarUrl = null };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WithFirstNameAtMaxLength_ShouldPass()
    {
        var command = CreateValidCommand() with { FirstName = new string('a', 100) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public async Task Validate_WithLastNameAtMaxLength_ShouldPass()
    {
        var command = CreateValidCommand() with { LastName = new string('a', 100) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public async Task Validate_WithEmailAtMaxLength_ShouldPass()
    {
        var command = CreateValidCommand() with { Email = new string('a', 250) + "@b.com" };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public async Task Validate_WithPhoneAtMaxLength_ShouldPass()
    {
        var command = CreateValidCommand() with { Phone = new string('1', 20) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public async Task Validate_WithPositionAtMaxLength_ShouldPass()
    {
        var command = CreateValidCommand() with { Position = new string('a', 200) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Position);
    }

    [Fact]
    public async Task Validate_WithAvatarUrlExceeding2000Chars_ShouldFail()
    {
        var command = CreateValidCommand() with { AvatarUrl = new string('a', 2001) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldHaveValidationErrorFor(x => x.AvatarUrl);
    }

    [Fact]
    public async Task Validate_WithAvatarUrlAtMaxLength_ShouldPass()
    {
        var command = CreateValidCommand() with { AvatarUrl = new string('a', 2000) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveValidationErrorFor(x => x.AvatarUrl);
    }

    [Fact]
    public async Task Validate_WithNotesAtMaxLength_ShouldPass()
    {
        var command = CreateValidCommand() with { Notes = new string('a', 2000) };
        var result = await _validator.TestValidateAsync(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }
}
