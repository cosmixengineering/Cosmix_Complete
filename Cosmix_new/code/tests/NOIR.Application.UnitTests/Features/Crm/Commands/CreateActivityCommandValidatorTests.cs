using NOIR.Application.Features.Crm.Commands.CreateActivity;
using NOIR.Domain.Entities.Crm;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class CreateActivityCommandValidatorTests
{
    private readonly CreateActivityCommandValidator _validator;

    public CreateActivityCommandValidatorTests()
    {
        _validator = new CreateActivityCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new CreateActivityCommand(
            ActivityType.Call, "Follow-up call", Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(-1),
            Description: "Discussed pricing",
            ContactId: Guid.NewGuid(),
            DurationMinutes: 30);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptySubject_ShouldFail()
    {
        // Arrange
        var command = new CreateActivityCommand(
            ActivityType.Call, "", Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(-1),
            ContactId: Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Validate_SubjectTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateActivityCommand(
            ActivityType.Call, new string('A', 201), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(-1),
            ContactId: Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Validate_EmptyPerformedById_ShouldFail()
    {
        // Arrange
        var command = new CreateActivityCommand(
            ActivityType.Call, "Follow-up", Guid.Empty,
            DateTimeOffset.UtcNow.AddHours(-1),
            ContactId: Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PerformedById);
    }

    [Fact]
    public void Validate_DescriptionTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateActivityCommand(
            ActivityType.Call, "Follow-up", Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(-1),
            Description: new string('X', 2001),
            ContactId: Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_ZeroDuration_ShouldFail()
    {
        // Arrange
        var command = new CreateActivityCommand(
            ActivityType.Call, "Follow-up", Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(-1),
            ContactId: Guid.NewGuid(),
            DurationMinutes: 0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DurationMinutes);
    }

    [Fact]
    public void Validate_NegativeDuration_ShouldFail()
    {
        // Arrange
        var command = new CreateActivityCommand(
            ActivityType.Call, "Follow-up", Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(-1),
            ContactId: Guid.NewGuid(),
            DurationMinutes: -5);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DurationMinutes);
    }

    [Fact]
    public void Validate_NullDescription_ShouldPass()
    {
        // Arrange
        var command = new CreateActivityCommand(
            ActivityType.Call, "Follow-up", Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(-1),
            ContactId: Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_NullDuration_ShouldPass()
    {
        // Arrange
        var command = new CreateActivityCommand(
            ActivityType.Call, "Follow-up", Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(-1),
            ContactId: Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.DurationMinutes);
    }
}
