import type { BrowserContext, Page } from '@playwright/test';

export async function lockEnvironment(context: BrowserContext): Promise<void> {
  await context.addInitScript(() => {
    localStorage.setItem('noir-theme', 'light');
    localStorage.setItem('noir-language', 'en');
    localStorage.setItem('sidebar-collapsed', 'false');
    document.cookie = 'noir-language=en; path=/; max-age=31536000';
  });
}

export interface PageListeners {
  consoleErrors: Array<{ type: string; text: string }>;
  networkErrors: Array<{ url: string; status: number }>;
  waitForApiIdle: (options?: {
    timeoutMs?: number;
    idleMs?: number;
    maxRequestAgeMs?: number;
  }) => Promise<void>;
  detach: () => void;
}

export function attachListeners(page: Page): PageListeners {
  const consoleErrors: Array<{ type: string; text: string }> = [];
  const networkErrors: Array<{ url: string; status: number }> = [];
  const pendingApiRequests = new Map<any, number>();

  const IGNORED_CONSOLE = ['i18next', '[HMR]', 'favicon', 'vite', 'Download the React DevTools', 'gravatar.com', 'status of 404'];
  const IGNORED_URLS = [
    '/health',
    '/favicon',
    '.hot-update',
    '/sockjs-node',
    '/events',
    '/sse',
    '/stream',
    '/signalr',
    '/hub',
    '/socket',
    'gravatar.com',
  ];

  const isTrackedApiRequest = (req: any): boolean => {
    const url = req.url();
    if (IGNORED_URLS.some(s => url.includes(s))) return false;
    if (req.method && req.method() === 'OPTIONS') return false;
    const resourceType = req.resourceType?.() ?? '';
    return url.includes('/api/') || resourceType === 'fetch' || resourceType === 'xhr';
  };

  const onConsole = (msg: any) => {
    if (msg.type() === 'error') {
      const text = msg.text();
      if (IGNORED_CONSOLE.some(s => text.includes(s))) return;
      consoleErrors.push({ type: msg.type(), text });
    }
  };

  const onResponse = (res: any) => {
    const req = res.request?.();
    if (req && isTrackedApiRequest(req)) {
      pendingApiRequests.delete(req);
    }

    if (!res.ok() && res.status() !== 304) {
      const url = res.url();
      if (IGNORED_URLS.some(s => url.includes(s))) return;
      networkErrors.push({ url, status: res.status() });
    }
  };

  const onRequest = (req: any) => {
    if (!isTrackedApiRequest(req)) return;
    pendingApiRequests.set(req, Date.now());
  };

  const onRequestFailed = (req: any) => {
    if (!isTrackedApiRequest(req)) return;
    pendingApiRequests.delete(req);
  };

  page.on('console', onConsole);
  page.on('request', onRequest);
  page.on('response', onResponse);
  page.on('requestfailed', onRequestFailed);

  return {
    consoleErrors,
    networkErrors,
    waitForApiIdle: async (options?: {
      timeoutMs?: number;
      idleMs?: number;
      maxRequestAgeMs?: number;
    }) => {
      const timeoutMs = options?.timeoutMs ?? 8_000;
      const idleMs = options?.idleMs ?? 500;
      const maxRequestAgeMs = options?.maxRequestAgeMs ?? 6_000;
      const start = Date.now();
      let idleSince = Date.now();

      while (Date.now() - start < timeoutMs) {
        if (pendingApiRequests.size === 0) {
          if (Date.now() - idleSince >= idleMs) return;
        } else {
          const now = Date.now();
          const hasOnlyLongRunning = [...pendingApiRequests.values()].every(
            startedAt => now - startedAt >= maxRequestAgeMs,
          );
          if (hasOnlyLongRunning) return;
          idleSince = now;
        }
        await new Promise(resolve => setTimeout(resolve, 100));
      }
    },
    detach: () => {
      page.removeListener('console', onConsole);
      page.removeListener('request', onRequest);
      page.removeListener('response', onResponse);
      page.removeListener('requestfailed', onRequestFailed);
      pendingApiRequests.clear();
    },
  };
}

const LOADING_INDICATOR_SELECTORS = [
  '[aria-busy="true"]',
  '[role="progressbar"]',
  '[data-testid*="loading"]',
  '[class*="loading"]',
  '[class*="spinner"]',
  '[class*="skeleton"]',
  '[class*="animate-pulse"]',
];

export async function waitForPageReady(
  page: Page,
  listeners: PageListeners,
  waitForSelector?: string,
  options?: {
    apiIdleTimeoutMs?: number;
    selectorTimeoutMs?: number;
  },
): Promise<void> {
  const apiIdleTimeoutMs = options?.apiIdleTimeoutMs ?? 8_000;
  const selectorTimeoutMs = options?.selectorTimeoutMs ?? 8_000;

  await page.waitForLoadState('domcontentloaded').catch(() => {});
  await page.waitForLoadState('networkidle', { timeout: 12_000 }).catch(() => {});
  await listeners.waitForApiIdle({ timeoutMs: apiIdleTimeoutMs, idleMs: 500 });

  if (waitForSelector) {
    await page
      .waitForSelector(waitForSelector, { state: 'visible', timeout: selectorTimeoutMs })
      .catch(() => {});
  }

  for (const selector of LOADING_INDICATOR_SELECTORS) {
    const indicator = page.locator(selector).first();
    const visible = await indicator.isVisible({ timeout: 500 }).catch(() => false);
    if (visible) {
      await indicator.waitFor({ state: 'hidden', timeout: 8_000 }).catch(() => {});
    }
  }

  await listeners.waitForApiIdle({ timeoutMs: 4_000, idleMs: 500 });
  await page.waitForTimeout(300);
}
