using NOIR.Application.Features.Crm.Commands.UpdateActivity;
using NOIR.Domain.Entities.Crm;

namespace NOIR.Application.UnitTests.Features.Crm.Commands;

public class UpdateActivityCommandValidatorTests
{
    private readonly UpdateActivityCommandValidator _validator;

    public UpdateActivityCommandValidatorTests()
    {
        _validator = new UpdateActivityCommandValidator();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        // Arrange
        var command = new UpdateActivityCommand(
            Guid.NewGuid(), ActivityType.Call, "Updated subject",
            DateTimeOffset.UtcNow.AddHours(-1),
            Description: "Updated description",
            ContactId: Guid.NewGuid(),
            DurationMinutes: 45);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyId_ShouldFail()
    {
        // Arrange
        var command = new UpdateActivityCommand(
            Guid.Empty, ActivityType.Call, "Subject",
            DateTimeOffset.UtcNow.AddHours(-1));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Validate_EmptySubject_ShouldFail()
    {
        // Arrange
        var command = new UpdateActivityCommand(
            Guid.NewGuid(), ActivityType.Call, "",
            DateTimeOffset.UtcNow.AddHours(-1));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Validate_SubjectTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateActivityCommand(
            Guid.NewGuid(), ActivityType.Call, new string('A', 201),
            DateTimeOffset.UtcNow.AddHours(-1));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Validate_DescriptionTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateActivityCommand(
            Guid.NewGuid(), ActivityType.Call, "Subject",
            DateTimeOffset.UtcNow.AddHours(-1),
            Description: new string('X', 2001));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_ZeroDuration_ShouldFail()
    {
        // Arrange
        var command = new UpdateActivityCommand(
            Guid.NewGuid(), ActivityType.Call, "Subject",
            DateTimeOffset.UtcNow.AddHours(-1),
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
        var command = new UpdateActivityCommand(
            Guid.NewGuid(), ActivityType.Call, "Subject",
            DateTimeOffset.UtcNow.AddHours(-1),
            DurationMinutes: -10);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DurationMinutes);
    }

    [Fact]
    public void Validate_NullOptionalFields_ShouldPass()
    {
        // Arrange
        var command = new UpdateActivityCommand(
            Guid.NewGuid(), ActivityType.Note, "A note",
            DateTimeOffset.UtcNow.AddHours(-1));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
