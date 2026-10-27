import { useState } from 'react';
import { Pagination } from '@/components/data-display/Pagination';
import { Dialog } from '@/components/ui/Dialog';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { pageOf } from '../../schemas/contracts';
import { profileResponse } from '../../schemas/hiring';
import { changeHistorySchema, changeSummarySchema } from '../../schemas/updates';
import type { Section } from '../../types/updates';
import { labelDate } from '../../utils/messages';
import { QueryState } from '../Feedback';
import { RecordView } from '../RecordView';

export function ChangeHistory({ employeeId, section }: { employeeId: string; section: Section }) {
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string | null>(null);

  const profileQuery = useHrQuery(`employees/${employeeId}`, profileResponse, [
    'hr.employees.read',
  ]);
  const isApproved = (profileQuery.data?.employee?.status ?? 2) === 2;

  const path = `employees/${employeeId}/changes/${section.name}/history`;
  const query = useHrQuery(
    `${path}?page=${page}&pageSize=10`,
    pageOf(changeSummarySchema),
    ['hr.employees.read', ...section.read],
    isApproved,
  );
  const detail = useHrQuery(
    `${path}/${selected}`,
    changeHistorySchema,
    ['hr.employees.read', ...section.read],
    !!selected && isApproved,
  );

  if (!isApproved) {
    return (
      <section className="hr-form-section">
        <h2>Change history</h2>
        <p className="muted">
          Change history is activated once the employee onboarding has been approved.
        </p>
      </section>
    );
  }
  return (
    <section className="hr-form-section">
      <h2>Change history</h2>
      <QueryState query={query}>
        {!query.data?.items.length && <p className="muted">No changes recorded.</p>}
        {query.data?.items.map((r) => (
          <div className="hr-repeat" key={r.id}>
            <button className="hr-text-button" onClick={() => setSelected(r.id)}>
              Effective {r.effectiveFrom}
            </button>
            <p className="muted">
              {labelDate(r.recordedAt)} &middot; Actor {r.actorId}
            </p>
          </div>
        ))}
        {query.data && (
          <Pagination page={page} pageSize={10} total={query.data.totalCount} onChange={setPage} />
        )}
      </QueryState>
      <Dialog
        open={!!selected}
        onOpenChange={(open) => {
          if (!open) setSelected(null);
        }}
        title="Change evidence"
        description={section.label}
      >
        <QueryState query={detail}>
          {detail.data && (
            <>
              <p>{detail.data.reason}</p>
              <h3>Before</h3>
              <RecordView value={detail.data.before.data[section.name] ?? {}} />
              <h3>After</h3>
              <RecordView value={detail.data.after.data[section.name] ?? {}} />
            </>
          )}
        </QueryState>
      </Dialog>
    </section>
  );
}
