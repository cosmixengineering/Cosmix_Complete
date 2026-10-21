# Employee field checklist

Source: SRS v1.2 section 5. This is the full required implementation checklist.
The initial Employee draft model and related tables now cover part of this list; see
Initial-Models.md. The checklist is not a claim that the employee workflow or every field is complete.
An incomplete draft is allowed; activation validates the complete required record.

| Group | Fields to implement | Important constraint |
|---|---|---|
| Identity | Server employee number, full legal name, father/spouse name, CNIC/passport number and type, document expiry, date of birth, gender, marital status | Immutable unique employee number; identity document uniqueness/format |
| Photos and face | Private profile photo; separate encrypted face enrollment reference, state/date, authorized enroller, re-enrollment history | Profile photo is not a face template; no capture/template in normal DTOs |
| Contact | Personal phone, company phone/SIM, SIM issue date/status/credit tier, personal/company email | Normalize phone; unique official email; validate email |
| Address | Current and permanent address, city, province | Separate addresses |
| Emergency | Name, relationship, phone, alternate phone | Required emergency contact |
| Employment | Department, designation, employment category, staff category, reporting manager, branch/base city, assigned site | References to controlled records and effective assignment history |
| Dates and status | Joining date, probation period, draft/pending approval/active/suspended/resigned/terminated/retired status | Effective lifecycle dates and audited transitions |
| Schedule | Shift, work hours and roster assignment | Effective-dated; coordinate with Attendance |
| Compensation | Base/gross salary, monthly/daily/hourly wage basis, effective date, payment method | Restricted DTOs and permissions; retain history |
| Bank | Bank, account title/number, IBAN, verification status | Restricted and masked; separate from ordinary profile |
| Previous employment | Employer, role, dates, last salary, reason for leaving | Optional, sensitive |
| Documents | Offer, contract, identity document, degrees, certificates, licenses, signature/thumb file and evidence | Private metadata/file references, uploader, time and verification |
| Certifications | Type, number, issuer, issue/expiry date, attachment | Expiry tracking and notifications |
| Assets | Asset references, issue/return condition, custody | References; do not duplicate inventory administration |
| Medical | Authorized medical/emergency record | Fine-grained permission; no automated diagnosis |
| Mobile access | Enabled state, username/phone, linked user account and registered devices | Separate credential store; session revocation on lifecycle changes |

APIs must distinguish editable draft fields, protected HR-only fields, approved employee
self-service fields and effective-dated sensitive changes. Do not create one all-fields DTO
that returns CNIC, bank, medical and salary data to every employee or HR list consumer.
