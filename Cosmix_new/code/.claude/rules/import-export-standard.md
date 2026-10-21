# Import/Export Standard

## MANDATORY: Use ImportExportDropdown for All Import/Export UI

All list pages with data import/export MUST use `ImportExportDropdown` from `@uikit`. Never build custom import/export buttons or dropdowns.

## Component

```tsx
import { ImportExportDropdown, type ImportResult } from '@uikit'

// Full import/export (Products, Customers, Employees)
<ImportExportDropdown
  onExportCsv={handleExportCsv}
  onExportExcel={handleExportExcel}
  onImport={handleImport}
  onDownloadTemplate={handleDownloadTemplate}
  totalCount={data?.totalCount}
  entityLabel={t('domain.title')}
  onImportComplete={() => refetch()}
/>

// Export-only (Orders, Reports)
<ImportExportDropdown
  onExportCsv={() => exportEntity({ format: 'CSV' })}
  onExportExcel={() => exportEntity({ format: 'Excel' })}
/>
```

## Architecture Pattern

Domain-specific wrappers (e.g., `ProductImportExport`, `CustomerImportExport`, `EmployeeImportExport`) encapsulate CSV parsing and service calls, then delegate UI to the shared component:

```
Page → DomainImportExport → ImportExportDropdown (shared UI)
                          ↳ ImportProgressDialog (shared dialog)
```

## Dropdown Menu Items (Standard Order)

1. **Export CSV** — with optional item count `<Badge>`
2. **Export Excel**
3. *(separator)*
4. **Import CSV**
5. **Download Template**

Omit items by not passing the corresponding prop. Separator auto-hides when no import items.

## Import Handler Contract

```tsx
onImport: (file: File) => Promise<ImportResult>

interface ImportResult {
  success: number
  errors: { row: number; message: string }[]
}
```

The handler receives the raw `File`, does CSV parsing + API call, and returns `ImportResult`. The shared component handles progress dialog, toasts, and error display.

## i18n Keys

Shared keys in `importExport.*` namespace (both EN and VI). Domain-specific success/error messages can override via toast in the handler.

## Placement

Import/Export button goes in `PageHeader` `action` slot, left of the "Create" button:

```tsx
<PageHeader action={
  <div className="flex items-center gap-2">
    <DomainImportExport ... />
    <Button>+ Create Item</Button>
  </div>
} />
```

## Storybook

Stories in `src/uikit/import-export-dropdown/`:
- `ImportExportDropdown.stories.tsx` — FullImportExport, ExportOnly, ImportWithErrors, Disabled
- `ImportProgressDialog.stories.tsx` — InProgress, AllSuccess, WithErrors, AllErrors

## Reference Implementations

- **Full import/export**: `ProductImportExport.tsx`, `CustomerImportExport.tsx`, `EmployeeImportExport.tsx`
- **Export-only**: `OrdersPage.tsx` (uses `ImportExportDropdown` directly with export props only)
- **Storybook**: `UIKit > ImportExportDropdown`, `UIKit > ImportProgressDialog`

## Bug Prevention

- Always reset file input after selection: `event.target.value = ''`
- Employee import uses `FormData` (backend parses file) — adapt `ImportResultDto` → `ImportResult`
- Products/Customers parse CSV on frontend — keep parsing logic in domain wrapper, not shared component
