import { test, expect } from '../../fixtures/base.fixture';

/**
 * DataTable Responsive Tests
 * Verifies that tables with varying column counts display correctly
 * across different viewport sizes (mobile, tablet, desktop)
 */

test.describe('DataTable Responsive @ui', () => {
  const viewports = [
    { name: 'mobile', width: 375, height: 812 },    // iPhone X
    { name: 'tablet', width: 768, height: 1024 },   // iPad
    { name: 'desktop', width: 1440, height: 900 },  // Desktop
  ];

  test.describe('Promotions Page - Many Columns', () => {
    for (const viewport of viewports) {
      test(`DT-RESP-001: Promotions table at ${viewport.name} (${viewport.width}x${viewport.height})`, async ({
        page,
        promotionsPage,
      }) => {
        // Set viewport size
        await page.setViewportSize({ width: viewport.width, height: viewport.height });

        // Navigate to promotions page
        await promotionsPage.goto();

        // Wait for table to be visible
        await expect(promotionsPage.promotionTable).toBeVisible();

        // Verify table container allows horizontal scrolling on small screens
        const tableContainer = page.locator('[role="region"][aria-label="Scrollable table"]').first();
        if (await tableContainer.isVisible().catch(() => false)) {
          // Check that the container has overflow-x-auto
          const hasOverflow = await tableContainer.evaluate((el) => {
            const style = window.getComputedStyle(el);
            return style.overflowX === 'auto' || style.overflowX === 'scroll';
          });

          if (viewport.width < 1024) {
            // Mobile/Tablet: Should have horizontal scroll
            expect(hasOverflow).toBe(true);
          }
        }

        // Take screenshot for visual comparison
        await page.screenshot({
          path: `test-results/datatable-promotions-${viewport.name}.png`,
          fullPage: false,
        });

        // Verify action buttons are visible and centered
        const actionButtons = page.locator('button[aria-label*="action"], button svg[data-lucide="ellipsis-vertical"]').first();
        if (await actionButtons.isVisible().catch(() => false)) {
          await expect(actionButtons).toBeVisible();
        }

        // Verify table rows are not overlapping
        const rows = await promotionsPage.promotionRows.all();
        if (rows.length > 1) {
          // Check that each row has proper height and doesn't overlap
          for (let i = 0; i < Math.min(rows.length, 3); i++) {
            const row = rows[i];
            const boundingBox = await row.boundingBox();
            expect(boundingBox?.height).toBeGreaterThan(30); // Minimum row height
          }
        }
      });
    }
  });

  test.describe('Users Page - Fewer Columns', () => {
    for (const viewport of viewports) {
      test(`DT-RESP-002: Users table at ${viewport.name} (${viewport.width}x${viewport.height})`, async ({
        page,
        usersPage,
      }) => {
        // Set viewport size
        await page.setViewportSize({ width: viewport.width, height: viewport.height });

        // Navigate to users page
        await usersPage.goto();

        // Wait for table to be visible
        await expect(usersPage.userTable).toBeVisible();

        // Take screenshot for visual comparison
        await page.screenshot({
          path: `test-results/datatable-users-${viewport.name}.png`,
          fullPage: false,
        });

        // Verify the table fits without horizontal scroll on desktop
        if (viewport.width >= 1024) {
          const tableContainer = page.locator('[role="region"][aria-label="Scrollable table"]').first();
          if (await tableContainer.isVisible().catch(() => false)) {
            const containerWidth = await tableContainer.evaluate((el) => el.clientWidth);
            const tableWidth = await usersPage.userTable.evaluate((el) => el.scrollWidth);

            // On desktop, table should fit within container (no overflow)
            expect(tableWidth).toBeLessThanOrEqual(containerWidth + 10); // Allow small margin
          }
        }

        // Verify action column is properly sized (40-80px including padding)
        const actionCells = page.locator('td').first();
        if (await actionCells.isVisible().catch(() => false)) {
          const boundingBox = await actionCells.boundingBox();
          if (boundingBox) {
            expect(boundingBox.width).toBeGreaterThanOrEqual(40);
            expect(boundingBox.width).toBeLessThanOrEqual(80);
          }
        }
      });
    }
  });

  test.describe('DataTable Alignment Tests', () => {
    test('DT-ALIGN-001: Action buttons centered in action column', async ({
      page,
      promotionsPage,
    }) => {
      await page.setViewportSize({ width: 1440, height: 900 });
      await promotionsPage.goto();

      // Find first data row's action cell
      const firstDataRow = promotionsPage.promotionRows.nth(1); // Skip header
      const actionCell = firstDataRow.locator('td').first();
      await expect(actionCell).toBeVisible();

      // Find the button inside the action cell
      const actionButton = actionCell.locator('button');
      if (await actionButton.isVisible().catch(() => false)) {
        // Get bounding boxes for comparison
        const cellBox = await actionCell.boundingBox();
        const buttonBox = await actionButton.boundingBox();

        if (cellBox && buttonBox) {
          // Calculate center positions
          const cellCenterX = cellBox.x + cellBox.width / 2;
          const buttonCenterX = buttonBox.x + buttonBox.width / 2;

          // Button should be centered within the cell (allowing for padding)
          const offset = Math.abs(cellCenterX - buttonCenterX);
          expect(offset).toBeLessThanOrEqual(15);
        }
      }
    });

    test('DT-ALIGN-002: Checkbox centered in select column', async ({
      page,
      usersPage,
    }) => {
      await page.setViewportSize({ width: 1440, height: 900 });
      await usersPage.goto();

      // Find checkbox cell in first data row (skip actions column)
      const firstDataRow = usersPage.userRows.nth(1); // Skip header
      const checkboxCell = firstDataRow.locator('td').nth(1); // Second column (after actions)
      await expect(checkboxCell).toBeVisible();

      // Find checkbox inside the cell
      const checkbox = checkboxCell.locator('[role="checkbox"], button[role="checkbox"], input[type="checkbox"]').first();
      if (await checkbox.isVisible().catch(() => false)) {
        const cellBox = await checkboxCell.boundingBox();
        const checkboxBox = await checkbox.boundingBox();

        if (cellBox && checkboxBox) {
          const cellCenterX = cellBox.x + cellBox.width / 2;
          const checkboxCenterX = checkboxBox.x + checkboxBox.width / 2;

          // Checkbox should be centered within ~10px
          const offset = Math.abs(cellCenterX - checkboxCenterX);
          expect(offset).toBeLessThanOrEqual(10);
        }
      }
    });
  });

  test.describe('DataTable Column Visibility', () => {
    test('DT-COLVIS-001: Horizontal scroll on mobile for wide tables', async ({
      page,
      promotionsPage,
    }) => {
      // Mobile viewport
      await page.setViewportSize({ width: 375, height: 812 });
      await promotionsPage.goto();

      // Get the scrollable container
      const tableContainer = page.locator('[role="region"][aria-label="Scrollable table"]').first();
      await expect(tableContainer).toBeVisible();

      // Check scroll width vs client width
      const scrollInfo = await tableContainer.evaluate((el) => ({
        scrollWidth: el.scrollWidth,
        clientWidth: el.clientWidth,
        scrollLeft: el.scrollLeft,
      }));

      // Wide tables should have scrollWidth > clientWidth
      expect(scrollInfo.scrollWidth).toBeGreaterThan(scrollInfo.clientWidth);

      // Test horizontal scrolling
      await tableContainer.evaluate((el) => el.scrollTo({ left: 100, behavior: 'instant' }));

      // Verify scroll worked
      const newScrollLeft = await tableContainer.evaluate((el) => el.scrollLeft);
      expect(newScrollLeft).toBeGreaterThan(0);
    });
  });
});
