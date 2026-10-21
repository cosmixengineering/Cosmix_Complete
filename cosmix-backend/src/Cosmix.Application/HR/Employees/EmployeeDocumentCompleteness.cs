using Cosmix.Domain.HR.EmployeeDocuments;

namespace Cosmix.Application.HR.Employees;

/// <summary>Combines the established hiring minimum with explicitly published requirements.</summary>
public static class EmployeeDocumentCompleteness
{
    public static EmployeeDocumentCompletenessResponse Evaluate(Guid employeeId, IReadOnlyList<EmployeeDocument> documents,
        IReadOnlyCollection<Guid> selectedIds, IReadOnlyList<EmployeeCertificationRequirement> certifications,
        DateOnly today, int expiringWithinDays, bool approvedCertificationEvidence = false,
        IReadOnlyCollection<EmployeeDocumentType>? publishedRequiredTypes = null)
    {
        if (expiringWithinDays is < 1 or > 365) throw EmployeeOperation.Invalid("invalid_document_expiry_window");
        if (publishedRequiredTypes?.Any(type => !Enum.IsDefined(type)) == true)
            throw EmployeePublishedPolicyOptions.Unavailable();
        var through = today.AddDays(expiringWithinDays);
        var selected = documents.Where(d => d.EmployeeId == employeeId && d.Privacy == EmployeeDocumentPrivacy.Standard && selectedIds.Contains(d.Id)).ToArray();
        var available = selected.Where(d => d.IsAvailableOn(today)).ToArray();
        var certificateEvidence = approvedCertificationEvidence
            ? documents.Where(d => d.EmployeeId == employeeId && d.Privacy == EmployeeDocumentPrivacy.Standard && d.IsAvailableOn(today)).ToArray()
            : available;
        var required = new[] { EmployeeDocumentType.ProfilePhoto, EmployeeDocumentType.Identity }
            .Concat(publishedRequiredTypes ?? []).Distinct()
            .Select(type => new RequiredEmployeeDocumentMetadata(type, !available.Any(d => d.DocumentType == type),
                available.Where(d => d.DocumentType == type).Select(d => d.Id).ToArray())).ToArray();
        var unavailable = selectedIds.Where(id => !available.Any(d => d.Id == id)).Distinct().ToArray();
        var blockers = required.Where(r => r.IsMissing).Select(r => r.Type switch {
            EmployeeDocumentType.Identity => "documents.identity",
            EmployeeDocumentType.ProfilePhoto => "documents.profilePhoto",
            _ => "documents.required." + r.Type
        }).ToList();
        blockers.AddRange(unavailable.Select(id => $"documents.{id:N}.unavailable"));
        var certificates = certifications.Select(c =>
        {
            // Drafts use the selected manifest. An explicitly approved current certification owns its exact evidence link.
            var missing = !c.DocumentId.HasValue || !certificateEvidence.Any(d => d.Id == c.DocumentId);
            var expired = c.ExpiresOn < today;
            var notYetIssued = c.IssuedOn > today;
            if (!c.IsComplete) blockers.Add(c.Reference + ".incomplete");
            if (missing) blockers.Add(c.Reference + ".evidence");
            if (expired) blockers.Add(c.Reference + ".expired");
            if (notYetIssued) blockers.Add(c.Reference + ".notYetIssued");
            return new EmployeeCertificationExpiryMetadata(c.Reference, c.Type, c.ExpiresOn, c.DocumentId,
                missing, expired, c.ExpiresOn >= today && c.ExpiresOn <= through, c.IsComplete, c.Status);
        }).ToArray();
        // Naturally expiring valid evidence is a reminder, not an activation blocker until it expires.
        var expiring = available.Where(d => d.ExpiresOn >= today && d.ExpiresOn <= through)
            .Select(d => new ExpiringEmployeeDocumentMetadata(d.Id, d.DocumentType, d.ExpiresOn!.Value)).ToArray();
        return new(employeeId, today, through, required, unavailable, expiring, certificates, blockers.Distinct().ToArray());
    }
}
