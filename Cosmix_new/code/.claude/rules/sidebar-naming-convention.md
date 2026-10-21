# Sidebar Navigation Naming Convention

## Rule

ALL sidebar navigation labels MUST follow these conventions for both English and Vietnamese.

## English (en)

- **Title Case** for all items: "Customer Groups", "Blog Posts", "Activity Timeline"
- Section headers: noun groups, not verbs ("Orders", not "Manage Orders")
- Concise: 1-3 words preferred, max 4

## Vietnamese (vi)

- **Sentence case** only: capitalize first word only. "Khách hàng", not "Khách Hàng"
- Exception: acronyms keep English form (CRM, SMTP, API)
- **No English words** in item labels except universally understood acronyms
  - BAD: "Bài viết Blog", "Thư viện Media", "Pipeline", "Báo cáo HR"
  - GOOD: "Bài viết", "Thư viện tài nguyên", "Quy trình bán hàng", "Báo cáo nhân sự"
- Items under a labeled section don't repeat the section context (e.g. no "Blog" under "Nội dung")

## Allowed English in Vietnamese

| Term | Where | Rule |
|------|-------|------|
| CRM | Section header | Keep (universally understood acronym) |
| API, SMTP | Settings/technical | Keep |
| Blog | `modules.content.blog` value only | Keep (it IS the feature name). Never in sidebar item labels |

## Capitalization in `modules` Section

The `modules` section in `vi/common.json` follows the same sentence case rule. These values appear in the Feature Management settings page.

## i18n File Ownership

| File | Used By | Capitalization |
|------|---------|---------------|
| `common.json > nav.*` | Sidebar section headers (CSS `uppercase`) | Stored: sentence case |
| `common.json > {domain}.*` | Sidebar item labels | Stored: sentence case (VI), Title Case (EN) |
| `nav.json > menu.*` | Header/mobile menu | Stored: sentence case (VI), Title Case (EN) |
| `common.json > modules.*` | Feature management UI | Stored: sentence case (VI), Title Case (EN) |

## When Adding a New Sidebar Item

1. Add `titleKey` in `Sidebar.tsx` referencing `{domain}.{item}` or `{domain}.{sub}.title`
2. Add English value in Title Case to `en/common.json`
3. Add Vietnamese value in sentence case, pure Vietnamese (no English mixing) to `vi/common.json`
4. Align `nav.json` if the item appears in header/mobile menu
5. Align `breadcrumbs.*` if breadcrumbs reference the same label

## Bug This Prevents

- "Bài viết Blog" mixing Vietnamese + English in sidebar
- "Cài đặt Nền tảng" with inconsistent mid-phrase capitalization
- "Pipeline" left untranslated in CRM section
- "Báo cáo HR" mixing English acronym in item label
- Module names in Feature Management using Vietnamese Title Case
