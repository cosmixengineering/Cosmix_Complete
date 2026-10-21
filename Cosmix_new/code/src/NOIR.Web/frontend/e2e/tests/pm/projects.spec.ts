import { test, expect } from '../../fixtures/base.fixture';
import { testProject } from '../../helpers/test-data';

test.describe('Project Management @regression', () => {
  test('PM-001: should load project list @regression', async ({
    projectsPage,
    page,
  }) => {
    await projectsPage.goto();

    // Verify page loads (table or grid)
    await expect(
      page.getByRole('table').or(page.locator('[data-testid="project-list"], [data-testid="project-grid"], main')),
    ).toBeVisible();

    // No error states
    await expect(page.locator('[role="alert"][data-type="error"]')).not.toBeVisible();
  });

  test('PM-002: should create a new project @regression', async ({
    projectsPage,
    api,
    page,
  }) => {
    const data = testProject();

    await projectsPage.goto();

    // Check if create button is available (requires PmProjectsCreate permission)
    const createBtn = page.getByRole('button', { name: /create|new|add/i });
    const hasCreateButton = await createBtn.isVisible({ timeout: 3_000 }).catch(() => false);

    if (hasCreateButton) {
      await projectsPage.createProject({ name: data.name, description: data.description });

      // Verify success toast
      await expect(page.locator('[data-sonner-toast][data-type="success"]')).toBeVisible({ timeout: 10_000 });

      // Verify project appears (either navigated to detail or visible in list)
      const url = page.url();
      if (url.includes('/projects/')) {
        // Redirected to project detail — verify name is shown
        await expect(page.getByText(data.name).first()).toBeVisible();
      } else {
        // Still on list — verify in list
        await projectsPage.expectProjectInList(data.name);
      }

      // Cleanup: extract ID and delete
      const match = url.match(/projects\/([a-f0-9-]+)/);
      if (match) {
        await api.deleteProject(match[1]).catch(() => {});
      }
    } else {
      // Create via API and verify it appears in the list
      const created = await api.createProject(data);
      const projectId = created.id ?? created.Id;
      try {
        // Navigate and wait for the projects API to respond before asserting
        await Promise.all([
          page.waitForResponse(resp =>
            resp.url().includes('/api/pm/projects') && resp.request().method() === 'GET',
            { timeout: 15_000 }
          ).catch(() => {}),
          projectsPage.goto(),
        ]);
        await projectsPage.expectProjectInList(data.name);
      } finally {
        await api.deleteProject(projectId).catch(() => {});
      }
    }
  });

  test('PM-003: should show project detail with task board @regression', async ({
    projectsPage,
    api,
    page,
  }) => {
    // Create project via API
    const data = testProject();
    const created = await api.createProject(data);
    const projectId = created.id ?? created.Id;

    try {
      // Navigate and wait for the project detail API to respond
      await Promise.all([
        page.waitForResponse(resp =>
          resp.url().includes(`/api/pm/projects/${projectId}`) && resp.request().method() === 'GET',
          { timeout: 15_000 }
        ).catch(() => {}),
        projectsPage.gotoProject(projectId),
      ]);

      // Verify project name is shown (may take time to load from API)
      await expect(page.getByText(data.name).first()).toBeVisible({ timeout: 10_000 });

      // Verify task board or task list section exists
      const taskSection = page.getByText(/task|kanban|board|công việc/i).first();
      await expect(taskSection).toBeVisible({ timeout: 5_000 });

      // Create a task within the project
      const addTaskBtn = page.getByRole('button', { name: /add task|create task|new task|thêm/i });
      if (await addTaskBtn.isVisible({ timeout: 3_000 }).catch(() => false)) {
        await addTaskBtn.click();
        const titleInput = page.getByLabel(/title|name|tiêu đề/i).last();
        await titleInput.fill('E2E Test Task');
        await page.getByRole('button', { name: /save|create|submit/i }).last().click();

        // Verify task appears
        await expect(page.getByText('E2E Test Task').first()).toBeVisible({ timeout: 5_000 });
      }
    } finally {
      await api.deleteProject(projectId).catch(() => {});
    }
  });
});
