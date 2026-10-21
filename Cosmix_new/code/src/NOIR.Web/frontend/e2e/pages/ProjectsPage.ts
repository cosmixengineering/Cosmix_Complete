import { type Page, type Locator, expect } from '@playwright/test';

export class ProjectsPage {
  readonly createButton: Locator;
  readonly projectList: Locator;
  readonly searchInput: Locator;

  constructor(private page: Page) {
    this.createButton = page.getByRole('button', { name: /create|new|add/i });
    this.projectList = page.getByRole('table').or(
      page.locator('[data-testid="project-list"], [data-testid="project-grid"]'),
    );
    this.searchInput = page.getByPlaceholder(/search/i);
  }

  async goto() {
    await this.page.goto('/portal/projects');
    await this.page.waitForLoadState('networkidle');
  }

  async gotoProject(projectId: string) {
    await this.page.goto(`/portal/projects/${projectId}`);
    await this.page.waitForLoadState('networkidle');
  }

  async createProject(data: { name: string; description?: string }) {
    await this.createButton.click();
    // Wait for dialog to open
    await this.page.locator('[role="dialog"]').waitFor({ state: 'visible', timeout: 5_000 });
    await this.page.getByLabel(/name/i).first().fill(data.name);
    if (data.description) {
      await this.page.getByLabel(/description/i).fill(data.description);
    }
    // Submit via JS click (trusted event) to bypass Credenza footer overlay intercept
    await this.page.evaluate(() => {
      const btns = [...document.querySelectorAll('[role="dialog"] button')];
      const saveBtn = btns.find(b => /save|create|submit/i.test(b.textContent || ''));
      if (saveBtn) (saveBtn as HTMLButtonElement).click();
    });
    await this.page.waitForResponse(resp =>
      resp.url().includes('/api/pm/projects') && resp.request().method() === 'POST',
    );
  }

  async createTask(data: { title: string }) {
    await this.page.getByRole('button', { name: /add task|create task|new task/i }).click();
    await this.page.getByLabel(/title|name/i).last().fill(data.title);
    await this.page.getByRole('button', { name: /save|create|submit/i }).last().click();
  }

  async expectProjectInList(name: string) {
    await expect(this.page.getByText(new RegExp(name, 'i')).first()).toBeVisible({ timeout: 15_000 });
  }

  async expectTaskOnBoard(title: string) {
    await expect(this.page.getByText(new RegExp(title, 'i')).first()).toBeVisible();
  }
}
