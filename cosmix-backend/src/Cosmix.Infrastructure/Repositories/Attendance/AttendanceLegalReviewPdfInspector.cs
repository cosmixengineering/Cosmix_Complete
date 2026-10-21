using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.LegalReview;
using Cosmix.Application.Authentication;
using UglyToad.PdfPig;

namespace Cosmix.Infrastructure.Repositories.Attendance;

/// <summary>Structural PDF parsing only. Signatures, legal conclusions and appointments are never verified.</summary>
public sealed class AttendanceLegalReviewPdfInspector : IAttendanceLegalReviewPdfInspector
{
    public LegalReviewPdfInspection Inspect(byte[] content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (content is null || content.Length is < 8 or > AttendanceLegalReviewService.MaximumPdfBytes ||
            !content.AsSpan().StartsWith("%PDF-"u8)) throw AttendanceOperation.Invalid("invalid_legal_review_pdf");
        try
        {
            using var document = PdfDocument.Open(content, new ParsingOptions { UseLenientParsing = false, MaxStackDepth = 64 });
            if (document.IsEncrypted || document.NumberOfPages is < 1 or > AttendanceLegalReviewService.MaximumPdfPages)
                throw AttendanceOperation.Invalid("invalid_legal_review_pdf");
            for (var number = 1; number <= document.NumberOfPages; number++)
            {
                ct.ThrowIfCancellationRequested();
                var page = document.GetPage(number);
                if (page.Width <= 0 || page.Height <= 0) throw AttendanceOperation.Invalid("invalid_legal_review_pdf");
            }
            return new(document.NumberOfPages, "PdfPig/0.1.16");
        }
        catch (OperationCanceledException) { throw; }
        catch (AuthenticationFailure) { throw; }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Parser messages can contain document content and must never reach ProblemDetails.
            throw AttendanceOperation.Invalid("invalid_legal_review_pdf");
        }
    }
}
