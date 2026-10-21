using NOIR.Application.Features.Media.Commands.RenameMediaFile;

namespace NOIR.Application.UnitTests.Features.Media.Commands;

/// <summary>
/// Unit tests for RenameMediaFileCommandValidator.
/// Tests all validation rules for renaming a media file.
/// </summary>
public class RenameMediaFileCommandValidatorTests
{
    private readonly RenameMediaFileCommandValidator _validator = new();

    #region Id Validation

    [Fact]
    public async Task Validate_WhenIdIsEmpty_ShouldHaveError()
    {
        // Arrange
        var command = new RenameMediaFileCommand(Guid.Empty, "new-name.jpg");

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorMessage("Media file ID is required.");
    }

    [Fact]
    public async Task Validate_WhenIdIsValid_ShouldNotHaveError()
    {
        // Arrange
        var command = new RenameMediaFileCommand(Guid.NewGuid(), "new-name.jpg");

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Id);
    }

    #endregion

    #region NewFileName Validation

    [Fact]
    public async Task Validate_WhenNewFileNameIsEmpty_ShouldHaveError()
    {
        // Arrange
        var command = new RenameMediaFileCommand(Guid.NewGuid(), string.Empty);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewFileName)
            .WithErrorMessage("New file name is required.");
    }

    [Fact]
    public async Task Validate_WhenNewFileNameExceedsMaxLength_ShouldHaveError()
    {
        // Arrange
        var longName = new string('a', 501);
        var command = new RenameMediaFileCommand(Guid.NewGuid(), longName);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewFileName)
            .WithErrorMessage("File name must not exceed 500 characters.");
    }

    [Fact]
    public async Task Validate_WhenNewFileNameIs500Chars_ShouldNotHaveError()
    {
        // Arrange
        var exactName = new string('a', 500);
        var command = new RenameMediaFileCommand(Guid.NewGuid(), exactName);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.NewFileName);
    }

    #endregion

    #region Valid Command Tests

    [Fact]
    public async Task Validate_WhenCommandIsValid_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var command = new RenameMediaFileCommand(Guid.NewGuid(), "new-photo-name.jpg");

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
