import type { SessionManager } from '../../auth/SessionManager';
import type { ServiceData, ServiceKey } from '../../types/selfService';
import { canOpenService } from '../../constants/services';
import { AppError } from '../../utils/errors';
import { isGuid } from '../../utils/validation';
import {
  paymentHistoryGroup,
  paymentHistoryPage,
} from '../../utils/paymentHistory';
import {
  dateRange,
  record,
  records,
  requireDate,
} from '../../utils/serviceValues';

export type ServiceQuery = {
  from: string;
  to: string;
  date: string;
  year: number;
  month: number;
  page: number;
  cursor?: string;
};

export class SelfServiceClient {
  constructor(private readonly session: SessionManager) {}
  async load(key: ServiceKey, query: ServiceQuery): Promise<ServiceData> {
    await this.session.revalidate();
    if (!canOpenService(key, this.session.getSnapshot())) {
      throw new AppError('access_denied', 403);
    }
    dateRange(query.from, query.to);
    if (
      !Number.isInteger(query.page) ||
      query.page < 1 ||
      query.page > 1000000
    ) {
      throw new AppError('invalid_date_range');
    }
    const exclusiveTo = new Date(
      Date.parse(requireDate(query.to) + 'T00:00:00Z') + 86400000,
    )
      .toISOString()
      .slice(0, 10);
    const range = `from=${requireDate(query.from)}&to=${exclusiveTo}&page=${
      query.page
    }&pageSize=20`;
    const get = async (route: string) =>
      record(await this.session.request<unknown>(route));
    switch (key) {
      case 'leaves': {
        const auth = this.session.getSnapshot();
        const overviewAllowed = auth.permissions.some(grant =>
          ['ess.leaves.read', 'auth.self.read'].includes(grant.permission),
        );
        // The optional overview must not prevent authorized leave-history reads.
        const [page, overview] = await Promise.all([
          get(
            `/api/v1/leaves/mine?from=${query.from}&to=${query.to}&page=${query.page}&pageSize=20`,
          ),
          overviewAllowed
            ? get('/api/v1/self-service/leaves').catch(() => null)
            : Promise.resolve(null),
        ]);
        return {
          root: { ...page, overview },
          rows: records(page.items, 20),
        };
      }
      case 'loans': {
        const [root, requests, agreements] = await Promise.all([
          get('/api/v1/me/advances-loans/summary'),
          get(
            `/api/v1/me/advances-loans/requests?page=${query.page}&pageSize=20`,
          ),
          get(
            `/api/v1/me/advances-loans/agreements?page=${query.page}&pageSize=20`,
          ),
        ]);
        return {
          root: {
            ...root,
            requestHistory: requests,
            agreementHistory: agreements,
          },
          rows: [
            ...records(requests.items, 20).map(row => ({
              ...row,
              recordKind: 'request',
            })),
            ...records(agreements.items, 20).map(row => ({
              ...row,
              recordKind: 'agreement',
              state: row.isFullyRepaid === true ? 'Repaid' : 'Outstanding',
            })),
          ],
        };
      }
      case 'resignation': {
        const root = await get(
          `/api/v1/me/resignation?page=${query.page}&pageSize=20`,
        );
        return { root, rows: records(record(root.requests).items, 20) };
      }
      case 'profile':
        return { root: await get('/api/v1/self-service/profile'), rows: [] };
      case 'finances':
        return { root: await get('/api/v1/self-service/finances'), rows: [] };
      case 'payroll': {
        const root = await get(
          `/api/v1/self-service/finances/payroll-history?page=${query.page}&pageSize=20`,
        );
        return {
          root,
          rows: records(root.items, 20).map(row => ({
            ...row,
            id: row.batchId,
            state: 'Prepared summary',
          })),
        };
      }
      case 'payments':
        return paymentHistoryPage(
          await get(
            `/api/v1/self-service/finances/payment-history?page=${query.page}&pageSize=20`,
          ),
          query.page,
        );
      case 'requests':
        return {
          root: await get('/api/v1/self-service/requests/summary'),
          rows: [],
        };
      case 'allowances': {
        const root = await get(`/api/v1/me/allowances/claims?${range}`);
        return { root, rows: records(root.items, 20) };
      }
      case 'vehicles': {
        const [root, custody] = await Promise.all([
          get(`/api/v1/me/vehicles/activity?page=${query.page}&pageSize=20`),
          this.session.request<unknown>('/api/v1/me/vehicles/custody'),
        ]);
        const rows = [
          'trips',
          'fuelClaims',
          'maintenance',
          'incidents',
        ].flatMap(name => records(record(root[name]).items, 20));
        return { root: { ...root, custody: records(custody) }, rows };
      }
      case 'corrections': {
        const root = await get(`/api/v1/me/attendance/corrections?${range}`);
        return { root, rows: records(root.items, 20) };
      }
      case 'overtime': {
        const root = await get(
          `/api/v1/me/overtime/requests?from=${
            query.from
          }&to=${exclusiveTo}&pageSize=20${
            query.cursor ? `&cursor=${encodeURIComponent(query.cursor)}` : ''
          }`,
        );
        return { root, rows: records(root.items, 20) };
      }
      case 'calendar': {
        const root = await get(
          `/api/v1/self-service/attendance?year=${query.year}&month=${query.month}`,
        );
        return { root, rows: records(root.days, 31) };
      }
      case 'punches': {
        const root = await get(`/api/v1/me/attendance/punches?${range}`);
        return { root, rows: records(root.items, 20) };
      }
      case 'shift':
        return {
          root: await get(
            `/api/v1/me/shift?businessDate=${requireDate(query.date)}`,
          ),
          rows: [],
        };
      case 'notifications': {
        const root = await get(
          `/api/v1/me/notifications?page=${query.page}&pageSize=20`,
        );
        return { root, rows: records(root.items, 20) };
      }
      case 'encashment':
        return {
          root: {},
          rows: records(
            await this.session.request<unknown>(
              '/api/v1/leaves/mine/encashment',
            ),
          ),
        };
    }
  }
  async loadPaymentPage(batchId: string, page: number) {
    if (
      !isGuid(batchId) ||
      !Number.isInteger(page) ||
      page < 1 ||
      page > 1000000
    ) {
      throw new AppError('invalid_server_response');
    }
    await this.session.revalidate();
    if (!canOpenService('payments', this.session.getSnapshot())) {
      throw new AppError('access_denied', 403);
    }
    const result = paymentHistoryGroup(
      await this.session.request<unknown>(
        `/api/v1/self-service/finances/payment-history/${batchId}?page=${page}&pageSize=20`,
      ),
    );
    if (result.batchId !== batchId || result.paymentsPage !== page) {
      throw new AppError('invalid_server_response');
    }
    return result;
  }
  async readNotification(id: string, version: string): Promise<void> {
    if (!/^[a-f0-9-]{36}$/i.test(id) || !/^[A-Za-z0-9+/]{11}=$/.test(version)) {
      throw new AppError('invalid_server_response');
    }
    await this.session.request(`/api/v1/me/notifications/${id}/read`, {
      method: 'POST',
      body: { expectedVersion: version },
    });
  }
}
