type ReadQuery = {
  data: unknown;
  isPending: boolean;
  isFetching: boolean;
  isError: boolean;
  error?: unknown;
};

export type DashboardCount = {
  status: 'ready' | 'loading' | 'restricted' | 'error' | 'unavailable';
  label: string;
  count?: number;
};

export function dashboardRead(query: ReadQuery, allowed: boolean, ready: boolean): DashboardCount {
  if (!ready) return { status: 'loading', label: 'Loading…' };
  if (!allowed) return { status: 'restricted', label: 'Access restricted' };
  if (query.isFetching || query.isPending) return { status: 'loading', label: 'Loading…' };
  if (
    query.isError &&
    typeof query.error === 'object' &&
    query.error !== null &&
    'status' in query.error &&
    query.error.status === 403
  )
    return { status: 'restricted', label: 'Access restricted' };
  if (query.isError) return { status: 'error', label: 'Could not load' };
  if (query.data === undefined) return { status: 'unavailable', label: 'Not loaded' };
  return { status: 'ready', label: 'Ready' };
}

export function dashboardCount(
  query: ReadQuery & { data: { totalCount: number } | undefined },
  allowed: boolean,
  ready: boolean,
): DashboardCount {
  const state = dashboardRead(query, allowed, ready);
  if (state.status !== 'ready' || !query.data) return state;
  return { status: 'ready', label: String(query.data.totalCount), count: query.data.totalCount };
}

export function dashboardMoney(amount: number, currency: string) {
  return `${currency} ${new Intl.NumberFormat('en-PK', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount)}`;
}

export function dashboardStatusLabel(value: string) {
  return value
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[_-]+/g, ' ')
    .replace(/^./, (first) => first.toUpperCase());
}

export function dashboardCsv(rows: readonly (readonly string[])[]) {
  return rows
    .map((row) =>
      row
        .map((value) => {
          const safe = /^\s*[=+\-@]/.test(value) ? "'" + value : value;
          return '"' + safe.replace(/"/g, '""') + '"';
        })
        .join(','),
    )
    .join('\r\n');
}
