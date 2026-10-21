import { useState } from 'react';
import { Pagination } from '@/components/data-display/Pagination';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { pageOf } from '../../schemas/contracts';
import { organizationChange } from '../../schemas/organization';
import { QueryState } from '../Feedback';
import { RecordView } from '../RecordView';
import { labelDate } from '../../utils/messages';
function snapshot(text: string | null) {
  try {
    const value: unknown = JSON.parse(text ?? '{}');
    return typeof value === 'object' && value !== null && !Array.isArray(value)
      ? (value as Record<string, unknown>)
      : {};
  } catch {
    return {};
  }
}
export function OrganizationHistory({ path, permission }: { path: string; permission: string }) {
  const [page, setPage] = useState(1);
  const query = useHrQuery(path + `/history?page=${page}&pageSize=10`, pageOf(organizationChange), [
    permission,
  ]);
  return (
    <QueryState query={query}>
      {query.data && (
        <>
          <h3>Change history</h3>
          {!query.data.items.length && <p>No changes recorded.</p>}
          {query.data.items.map((row) => (
            <details className="hr-repeat" key={row.id}>
              <summary>
                {labelDate(row.occurredAt)} · {row.operation}
              </summary>
              <p>{row.reason}</p>
              <p className="muted">
                {row.actorRoleCode} · {row.actorUserId}
              </p>
              {row.beforeJson && (
                <>
                  <h4>Before</h4>
                  <RecordView value={snapshot(row.beforeJson)} />
                </>
              )}
              <h4>After</h4>
              <RecordView value={snapshot(row.afterJson)} />
            </details>
          ))}
          <Pagination page={page} pageSize={10} total={query.data.totalCount} onChange={setPage} />
        </>
      )}
    </QueryState>
  );
}
