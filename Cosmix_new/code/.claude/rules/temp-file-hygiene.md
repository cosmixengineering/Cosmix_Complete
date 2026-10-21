# Temp File Hygiene

## Rule

ALL temporary files (screenshots, test artifacts, debug output, verification images) MUST go in `temp/` at the project root. **Never** write temp files directly to the project root.

```bash
# Screenshots from browser testing
temp/verify-users-page.png
temp/responsive-1440.png

# Debug output
temp/debug-query-result.json
```

## Cleanup

- **After completing a task**: delete any temp files you created in `temp/`
- **At conversation start**: if `temp/` has stale files from prior sessions, clean them up
- **Quick cleanup**: `rm -rf temp/* && touch temp/.gitkeep`

## What Goes in `temp/`

| Type | Example |
|------|---------|
| Playwright MCP screenshots | `temp/page-*.png`, `temp/element-*.png` |
| Verification screenshots | `temp/verify-*.png`, `temp/test-*.png` |
| Responsive test captures | `temp/responsive-*.png` |
| Debug/diagnostic output | `temp/debug-*.json`, `temp/*.log` |
| Temporary test scripts | `temp/*.js`, `temp/*.ts` |

## What Does NOT Go in `temp/`

- UI audit output (goes in `.ui-audit/`, already gitignored)
- Coverage reports (goes in `frontend/coverage/`, already gitignored)
- Build artifacts (goes in `bin/`/`obj/`, already gitignored)
- Uploaded files (goes in `src/NOIR.Web/uploads/`)

## `.gitignore`

`temp/` is gitignored. The directory contains a `.gitkeep` so it exists in fresh clones.
