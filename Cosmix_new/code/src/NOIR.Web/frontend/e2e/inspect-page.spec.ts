import { test } from '@playwright/test';

test('inspect roles page', async ({ page, context }) => {
  await context.storageState({ path: '.auth/admin.json' });
  
  await page.goto('/portal/admin/roles');
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(2000);
  
  const buttons = await page.evaluate(() => {
    const rows = document.querySelectorAll('table tbody tr');
    let result = '';
    rows.forEach((row, i) => {
      if (i < 3) {
        const btns = row.querySelectorAll('button, a[role="button"]');
        result += `\nRow ${i} interactive elements: ${btns.length}\n`;
        btns.forEach(btn => {
          result += `  - tag="${btn.tagName}" text="${btn.textContent?.trim().substring(0, 50)}" aria-label="${btn.getAttribute('aria-label')}" title="${btn.getAttribute('title')}"\n`;
        });
        
        // Also check for dropdown menu items
        const allInteractable = row.querySelectorAll('[role="menuitem"], [data-testid]');
        if (allInteractable.length > 0) {
          result += `  Role/testid elements: ${allInteractable.length}\n`;
        }
      }
    });
    return result;
  });
  
  console.log('BUTTONS IN ROWS:');
  console.log(buttons);
  
  // Take screenshot
  await page.screenshot({ path: 'inspect-roles-screenshot.png', fullPage: true });
  console.log('Screenshot saved');
});
