/**
 * Test tagging strategy for E2E test categorization.
 *
 * Tags are used with Playwright's --grep filter to run subsets of tests:
 *   npx playwright test --grep @smoke        # Critical path only (~5 min)
 *   npx playwright test --grep @regression   # Full regression (~10 min)
 *   npx playwright test --grep @nightly      # Cross-browser + edge cases
 *
 * Usage in tests:
 *   test('should login successfully @smoke', async ({ page }) => { ... });
 *   test('should handle edge case @nightly', async ({ page }) => { ... });
 */

/**
 * Tag constants for consistent test categorization.
 */
export const Tags = {
  /**
   * Smoke tests: Critical user journeys that must always pass.
   * Run on every PR. Target: <5 minutes.
   * Examples: login, create product, place order, create contact.
   */
  SMOKE: '@smoke',

  /**
   * Regression tests: Comprehensive coverage of all features.
   * Run on merge to main. Target: <10 minutes.
   * Examples: all CRUD operations, filtering, search, pagination.
   */
  REGRESSION: '@regression',

  /**
   * Nightly tests: Edge cases, cross-browser, performance.
   * Run on nightly schedule only. Target: <30 minutes.
   * Examples: error handling, concurrent sessions, mobile viewports.
   */
  NIGHTLY: '@nightly',
} as const;

export type TestTag = (typeof Tags)[keyof typeof Tags];

/**
 * Append a tag to a test title for grep filtering.
 *
 * @example
 *   test(tagged('should create product', Tags.SMOKE), async ({ page }) => { ... });
 */
export function tagged(title: string, ...tags: TestTag[]): string {
  return `${title} ${tags.join(' ')}`;
}

/**
 * Performance budget thresholds (in milliseconds).
 * Used for performance-aware assertions in nightly tests.
 */
export const PerformanceBudgets = {
  /** Page navigation should complete within this budget */
  PAGE_LOAD_MS: 3_000,
  /** API responses should return within this budget */
  API_RESPONSE_MS: 2_000,
  /** Table render with 50+ rows should complete within this budget */
  TABLE_RENDER_MS: 1_000,
} as const;
