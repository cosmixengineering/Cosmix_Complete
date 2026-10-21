import { test as base, expect } from '@playwright/test';
import { ApiHelper } from './api.fixture';

// Import page objects
import { LoginPage } from '../pages/LoginPage';
import { DashboardPage } from '../pages/DashboardPage';
import { ProductsPage } from '../pages/ProductsPage';
import { OrdersPage } from '../pages/OrdersPage';
import { OrderDetailPage } from '../pages/OrderDetailPage';
import { CrmContactsPage } from '../pages/CrmContactsPage';
import { CrmPipelinePage } from '../pages/CrmPipelinePage';
import { EmployeesPage } from '../pages/EmployeesPage';
import { UsersPage } from '../pages/UsersPage';
import { RolesPage } from '../pages/RolesPage';
import { BlogPostsPage } from '../pages/BlogPostsPage';
import { InventoryPage } from '../pages/InventoryPage';
import { ProjectsPage } from '../pages/ProjectsPage';
import { CustomersPage } from '../pages/CustomersPage';
import { PromotionsPage } from '../pages/PromotionsPage';
import { ReviewsPage } from '../pages/ReviewsPage';
import { CustomerGroupsPage } from '../pages/CustomerGroupsPage';
import { NotificationsPage } from '../pages/NotificationsPage';

/**
 * Extended test fixture with:
 * - API helper for data setup/cleanup
 * - Pre-built page objects (auto-injected)
 * - Cleanup tracking
 *
 * Usage:
 *   import { test, expect } from '../fixtures/base.fixture';
 *   test('my test', async ({ productsPage, api }) => { ... });
 */

type CleanupFn = () => Promise<void>;

type Fixtures = {
  api: ApiHelper;
  loginPage: LoginPage;
  dashboardPage: DashboardPage;
  productsPage: ProductsPage;
  ordersPage: OrdersPage;
  orderDetailPage: OrderDetailPage;
  crmContactsPage: CrmContactsPage;
  crmPipelinePage: CrmPipelinePage;
  employeesPage: EmployeesPage;
  usersPage: UsersPage;
  rolesPage: RolesPage;
  blogPostsPage: BlogPostsPage;
  inventoryPage: InventoryPage;
  projectsPage: ProjectsPage;
  customersPage: CustomersPage;
  promotionsPage: PromotionsPage;
  reviewsPage: ReviewsPage;
  customerGroupsPage: CustomerGroupsPage;
  notificationsPage: NotificationsPage;
  trackCleanup: (fn: CleanupFn) => void;
};

export const test = base.extend<Fixtures>({
  // API helper — authenticates directly via API to avoid storageState file race conditions
  api: async ({ playwright }, use) => {
    const API_URL = process.env.API_URL ?? 'http://localhost:4000';
    // Create context first, then login to get Bearer token
    const context = await playwright.request.newContext({
      baseURL: API_URL,
      extraHTTPHeaders: { 'X-Tenant': 'default' },
    });
    // Authenticate to get JWT token
    const loginRes = await context.post(`${API_URL}/api/auth/login`, {
      data: { email: 'admin@noir.local', password: '123qwe' },
      headers: { 'Content-Type': 'application/json', 'X-Tenant': 'default' },
    });
    let bearerToken = '';
    if (loginRes.ok()) {
      const loginBody = await loginRes.json();
      bearerToken = loginBody?.auth?.accessToken ?? '';
    }
    // Recreate context with Authorization header
    await context.dispose();
    const authContext = await playwright.request.newContext({
      baseURL: API_URL,
      extraHTTPHeaders: {
        'X-Tenant': 'default',
        ...(bearerToken ? { 'Authorization': `Bearer ${bearerToken}` } : {}),
      },
    });
    const api = new ApiHelper(authContext);
    await use(api);
    await authContext.dispose();
  },

  // Page objects — auto-created from page
  loginPage: async ({ page }, use) => {
    await use(new LoginPage(page));
  },
  dashboardPage: async ({ page }, use) => {
    await use(new DashboardPage(page));
  },
  productsPage: async ({ page }, use) => {
    await use(new ProductsPage(page));
  },
  ordersPage: async ({ page }, use) => {
    await use(new OrdersPage(page));
  },
  orderDetailPage: async ({ page }, use) => {
    await use(new OrderDetailPage(page));
  },
  crmContactsPage: async ({ page }, use) => {
    await use(new CrmContactsPage(page));
  },
  crmPipelinePage: async ({ page }, use) => {
    await use(new CrmPipelinePage(page));
  },
  employeesPage: async ({ page }, use) => {
    await use(new EmployeesPage(page));
  },
  usersPage: async ({ page }, use) => {
    await use(new UsersPage(page));
  },
  rolesPage: async ({ page }, use) => {
    await use(new RolesPage(page));
  },
  blogPostsPage: async ({ page }, use) => {
    await use(new BlogPostsPage(page));
  },
  inventoryPage: async ({ page }, use) => {
    await use(new InventoryPage(page));
  },
  projectsPage: async ({ page }, use) => {
    await use(new ProjectsPage(page));
  },
  customersPage: async ({ page }, use) => {
    await use(new CustomersPage(page));
  },
  promotionsPage: async ({ page }, use) => {
    await use(new PromotionsPage(page));
  },
  reviewsPage: async ({ page }, use) => {
    await use(new ReviewsPage(page));
  },
  customerGroupsPage: async ({ page }, use) => {
    await use(new CustomerGroupsPage(page));
  },
  notificationsPage: async ({ page }, use) => {
    await use(new NotificationsPage(page));
  },

  // Cleanup tracker — register cleanup functions during test, run after
  trackCleanup: async ({}, use) => {
    const cleanups: CleanupFn[] = [];
    await use((fn: CleanupFn) => { cleanups.push(fn); });
    // Run cleanups in reverse order (LIFO)
    for (const fn of cleanups.reverse()) {
      try { await fn(); } catch { /* best-effort cleanup */ }
    }
  },
});

export { expect };
