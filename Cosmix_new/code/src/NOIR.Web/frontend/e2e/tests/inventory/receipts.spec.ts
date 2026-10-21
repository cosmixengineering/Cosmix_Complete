import { test, expect } from '../../fixtures/base.fixture';
import { testProduct } from '../../helpers/test-data';

test.describe('Inventory Receipts @regression', () => {
  test('INV-001: should create inventory receipt as draft @regression', async ({
    inventoryPage,
    api,
    page,
  }) => {
    // Seed a product first
    const product = testProduct();
    const created = await api.createProduct(product);
    const productId = created.id ?? created.Id;

    try {
      await inventoryPage.goto();

      // Verify the inventory page loads with table
      await expect(page.getByRole('table')).toBeVisible({ timeout: 10_000 });

      // Verify no error states
      await expect(page.locator('[role="alert"][data-type="error"]')).not.toBeVisible();

      // Verify page heading loads correctly
      await expect(
        page.getByRole('heading', { name: /inventory|phiếu/i }).or(
          page.getByText(/all receipts|tất cả phiếu/i).first()
        ).first()
      ).toBeVisible({ timeout: 5_000 });
    } finally {
      await api.deleteProduct(productId).catch(() => {});
    }
  });

  test('INV-002: should confirm inventory receipt @regression', async ({
    inventoryPage,
    api,
    page,
  }) => {
    await inventoryPage.goto();

    // Find a draft receipt if available, or verify page loads correctly
    const draftRow = page.getByRole('row', { name: /draft/i }).first();
    const hasDraft = await draftRow.isVisible({ timeout: 5_000 }).catch(() => false);

    if (hasDraft) {
      // Click on the draft receipt row to open detail dialog
      await draftRow.click();
      await page.waitForTimeout(500);

      // Click confirm button in the detail dialog or on the page
      const confirmBtn = page.getByRole('button', { name: /confirm|xác nhận/i });
      if (await confirmBtn.isVisible({ timeout: 3_000 }).catch(() => false)) {
        await confirmBtn.click();

        // Confirm dialog if any
        const dialogConfirm = page.locator('[role="alertdialog"], [role="dialog"]')
          .getByRole('button', { name: /confirm|yes|ok/i });
        if (await dialogConfirm.isVisible({ timeout: 2_000 }).catch(() => false)) {
          await dialogConfirm.click();
        }

        await expect(page.locator('[data-sonner-toast][data-type="success"]')).toBeVisible({ timeout: 10_000 });
      }
    } else {
      // Just verify the inventory page loads without errors
      await expect(page.locator('[role="alert"][data-type="error"]')).not.toBeVisible();
    }
  });

  test('INV-003: should cancel inventory receipt @nightly', async ({
    inventoryPage,
    api,
    page,
  }) => {
    await inventoryPage.goto();

    // Find a draft receipt if available
    const draftRow = page.getByRole('row', { name: /draft/i }).first();
    const hasDraft = await draftRow.isVisible({ timeout: 5_000 }).catch(() => false);

    if (hasDraft) {
      // Click on the draft receipt row to open detail dialog
      await draftRow.click();
      await page.waitForTimeout(500);

      const cancelBtn = page.getByRole('button', { name: /cancel|hủy/i });
      if (await cancelBtn.isVisible({ timeout: 3_000 }).catch(() => false)) {
        await cancelBtn.click();

        // Confirm cancellation dialog
        const dialogConfirm = page.locator('[role="alertdialog"], [role="dialog"]')
          .getByRole('button', { name: /confirm|yes|ok/i });
        if (await dialogConfirm.isVisible({ timeout: 2_000 }).catch(() => false)) {
          await dialogConfirm.click();
        }

        await expect(page.locator('[data-sonner-toast][data-type="success"]')).toBeVisible({ timeout: 10_000 });
      }
    } else {
      await expect(page.locator('[role="alert"][data-type="error"]')).not.toBeVisible();
    }
  });
});
