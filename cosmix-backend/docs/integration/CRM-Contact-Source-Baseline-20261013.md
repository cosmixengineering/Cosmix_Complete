# CRM Contact Source Baseline

Source review dated 13 October 2026, Asia/Karachi. This starts the baseline and
source-manifest step of the delivery plan. No donor code has been imported, no
Contact workflow has been implemented in Cosmix, and no client acceptance is claimed.

## Current Repositories

| Purpose | Existing checkout | HEAD |
| --- | --- | --- |
| Authoritative backend | `C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend` | `3e4e4244808a651a432c020365a3535c43d0f81a` |
| Donor | `C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\Cosmix_new\code` | `c4500264431523f7db7960bb3a759a15ca60f08e` |
| Existing web client | `C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix_frotned` | `426f2b9cb75ed028b4ed570aaae1017cde2f906d` |

All three checkouts contain local changes. The proposed Contact sources below were
reported unchanged against donor HEAD, but other donor repairs remain uncommitted.
Capture source-file hashes as well as HEAD; recheck both before copying. The plan's
older backend `70adaf7` baseline is superseded by the current source plus preserved
local Attendance work. No synchronization, branch creation or Git write is included.

## Candidate Reuse Manifest

Paths are relative to the donor root above. Destinations remain proposed until the
Contact contract and ownership design are reviewed.

| Existing source | Reuse decision | Proposed destination or dependency boundary |
| --- | --- | --- |
| `src/NOIR.Web/frontend/src/portal-app/crm/features/contacts/ContactsPage.tsx` | Adapt list/search/filter/paging presentation | Existing web Sales/CRM module; deletion outside initial trial |
| `src/NOIR.Web/frontend/src/portal-app/crm/features/contacts/components/ContactDialog.tsx` | Adapt create/edit form after fixing detail initialization | Sales/CRM components using target controls |
| `src/NOIR.Web/frontend/src/portal-app/crm/features/contacts/ContactDetailPage.tsx` | Adapt contact detail only | Defer donor lead/activity dependencies |
| `src/NOIR.Web/frontend/src/services/crm.ts` | Reference only; rewrite transport | Target `src/services/api/http-client.ts` |
| `src/NOIR.Web/frontend/src/types/crm.ts` | Reference fields; replace with validated target DTOs | Sales/CRM types and schemas |
| `src/NOIR.Web/frontend/src/portal-app/crm/queries/useContactQueries.ts` | Reference query behavior | Session-bound target caching/cancellation |
| `src/NOIR.Web/frontend/src/portal-app/crm/queries/useContactMutations.ts` | Reference mutation behavior | Explicit retry keys and cache invalidation |
| `src/NOIR.Domain/Entities/Crm/CrmContact.cs` | Reference entity invariants and fields | Proposed `src/Cosmix.Domain/CRM/Contact.cs` |
| `src/NOIR.Application/Features/Crm/DTOs/ContactDtos.cs` | Reference contracts, not an API contract to import | Proposed target Contact contracts |
| `src/NOIR.Application/Features/Crm/Commands/CreateContact/CreateContactCommandHandler.cs` | Reference use case | Proposed target Contact service and repository |
| `src/NOIR.Application/Features/Crm/Commands/UpdateContact/UpdateContactCommandHandler.cs` | Reference use case | Target authorization, expected-version checks and atomic audit |
| `src/NOIR.Application/Features/Crm/Specifications/ContactSpecs.cs` | Reference filters only | Target module-specific queries |
| `src/NOIR.Infrastructure/Persistence/Configurations/CrmContactConfiguration.cs` | Reference constraints only | New reviewed target configuration/migration, never donor history |
| `src/NOIR.Web/Endpoints/CrmContactEndpoints.cs` | Reference route behavior only | Proposed target controller, existing authentication and CSRF |

## Dependencies And Provenance

Donor root `LICENSE` states Apache 2.0. Repository-level licensing is not a full
third-party provenance inventory; retain attribution and review each actually
ported fragment/dependency before distribution.

The selected UI uses donor aliases, React Hook Form/resolvers, i18next, Sonner,
TanStack Table and Tailwind conventions. Donor table/drawer controls have additional
DnD/responsive-dialog dependencies. Prefer existing target `DataTable.tsx` and
`Dialog.tsx`; do not import the whole donor UI/provider dependency tree.

Backend handlers depend on donor Wolverine, FluentValidation, tenancy, generic
repositories and realtime publication. Keep these outside the port. Map behavior
to target Domain/Application/Infrastructure/Api boundaries instead.

## Verified Target Integration Points

Web: `src/services/api/http-client.ts`, `src/app/router/AppRoutes.tsx`,
`src/components/data-display/DataTable.tsx`, `src/components/ui/Dialog.tsx` and
`src/modules/sales/README.md` already exist. The Sales README owns Sales/CRM and
describes folder boundaries, not implemented workflows. Review that ownership
before creating the delivery plan's proposed separate `src/modules/crm` tree.

Backend: `AccessPermissionEvaluator`, `AccessResource`, `CosmixDbContext` and
`OrganizationOperation` are existing references. `OrganizationOperation` is not
a ready-made record-scoped CRM authorization contract; its All-scope behavior
must not be inherited as blanket Contact access.

The plan's proposed Contact entity, contracts, service, repository, configuration
and controller files are absent. That is a missing target workflow, not a finished
module waiting for a menu entry.

## Decisions Before The Contact Trial

1. Keep owners linked to the existing employee master; derive actors from the
   authenticated account. Define record scope and reassignment authority. Current
   access resources have employee/department/team/site/project scopes, not an
   established customer-company scope.
2. Keep CRM customer companies distinct from employer/Head Office settings. A
   contact does not become an employee, account or customer login automatically.
3. Approve the exact create/detail/update fields, duplicate policy, expected
   versions and immutable audit/retry behavior. Load authoritative detail before
   editing: donor `ContactDialog` accepts a reduced list DTO and defaults missing
   owner/company/notes to empty values. This presents a clearing risk that needs
   a target regression, not blind copying. Donor TypeScript/backend DTOs also differ.
4. Confirm Sales/CRM frontend ownership and review the small backend/contract
   design before implementation. Preserve existing MFA/CSRF and HR clients.

The first trial remains list/create/detail/edit only. The plan's five working days
is a measurement budget, not a promise of complete CRM. Email, WhatsApp/social
connectors, leads, activities, quotations and customer accounts are separate flows.
Accept and measure the trial before broader CRM, Inventory and Projects.

## Recorded SHA256

These hashes bind the reviewed local source, not accepted target functionality.

| Source | SHA256 |
| --- | --- |
| `LICENSE` | `1EB85FC97224598DAD1852B5D6483BBCF0AA8608790DCC657A5A2A761AE9C8C6` |
| `src/NOIR.Web/frontend/package.json` | `45E5EA89259DE357A08585DD3D09C184DBCD21D8C25F49C1A75FE8CB8E512204` |
| `ContactsPage.tsx` at the manifest path | `C3C8779AE4DABBEAB67D3277E34A6CA77C86CE8D6F3EBF61AB770848A4614C93` |
| `ContactDialog.tsx` at the manifest path | `47C44DB427489E56C44DD37A7A64DFB8492CE745E0FCC34078FEAB11AD11AED2` |
| `ContactDetailPage.tsx` at the manifest path | `3D3BDEB69DBF976C6CF7EA13F6CE01B64FB9B6A7086F495D9D0CE80D5874F6BD` |
| `src/NOIR.Web/frontend/src/services/crm.ts` | `982D6A1B21AF3DD21EE9980460736A2B4CECFF7597DBB56CE5161C18D0FA7CFA` |
| `src/NOIR.Web/frontend/src/types/crm.ts` | `557D26CDADE9D8907C7D2D92EF1C0EECD2964AB1E0CD32DA583176FAFB1437DD` |
| `useContactQueries.ts` at the manifest path | `7A03DB6AE0882C661E11A574F8177EF75317F9934405C8152FF6E3878EBFA0F1` |
| `useContactMutations.ts` at the manifest path | `12194EB9666E878FA281A6B3A2D730846F912067733433AB42BF3ED63767626F` |
| `CrmContact.cs` at the manifest path | `178B8FF5D3E52B9F138CEEA976FF3541CCF3E930763A3329CDEE5F1FE716DB33` |
| `ContactDtos.cs` at the manifest path | `C5673F0DB7F267F0021FCD9B55E417325DF2BF30A65AE69E45423359A799B5E6` |
| `CreateContactCommandHandler.cs` at the manifest path | `AE117C028A358DFF4A11A114D23AADFE771226C5828FC110713D183C6BB80E0A` |
| `UpdateContactCommandHandler.cs` at the manifest path | `B86F474328BEA7F8E3B0B383BBCCF47A95B461DFFC93656B141451ECD465164F` |
| `ContactSpecs.cs` at the manifest path | `F56E032D6D0F85B0324E195632D2359E10674ACD90A0EEE4F63E0DC05C77DFF8` |
| `CrmContactConfiguration.cs` at the manifest path | `43C2560E4F8539FC3AB3AFECA18ECABBD3D588E9DA6E0E7783ED6F173747530A` |
| `CrmContactEndpoints.cs` at the manifest path | `5B8A0A93B5549810DFE571B5F9DE37DF0F339BAD03AB257BD31A4AC21CDF2DBC` |

No donor build/runtime result was rerun here. No separate React/React Native client
was implemented or tested. See [continuation order](Continuation-Order-20261013.md).
