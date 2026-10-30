import { useState } from 'react';
import { History } from 'lucide-react';
import { Pagination } from '@/components/data-display/Pagination';
import { Dialog } from '@/components/ui/Dialog';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { pageOf } from '../../schemas/contracts';
import { changeHistorySchema, changeSummarySchema } from '../../schemas/updates';
import type { Section } from '../../types/updates';
import { labelDate } from '../../utils/messages';
import { QueryState } from '../Feedback';
import { ChangeEvidenceViewer } from './ChangeEvidenceViewer';
import '@/styles/hr-dashboard.css';

export function ChangeHistory({ employeeId, section }: { employeeId: string; section: Section }) {
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string | null>(null);

  const path = `employees/${employeeId}/changes/${section.name}/history`;
  const query = useHrQuery(`${path}?page=${page}&pageSize=10`, pageOf(changeSummarySchema), [
    'hr.employees.read',
    ...section.read,
  ]);
  const detail = useHrQuery(
    `${path}/${selected}`,
    changeHistorySchema,
    ['hr.employees.read', ...section.read],
    !!selected,
  );

  return (
    <div
      className="emp-card"
      style={{
        marginTop: '18px',
        padding: '20px 24px',
        background: '#ffffff',
        border: '1px solid #e2e8f0',
        borderRadius: '10px',
      }}
    >
      <div className="emp-card-header simple" style={{ marginBottom: '10px' }}>
        <div
          className="emp-card-title"
          style={{ fontSize: '15px', fontWeight: 700, color: '#0f172a' }}
        >
          <History size={16} className="emp-card-title-icon" style={{ color: '#334155' }} />
          <span>Change history</span>
        </div>
      </div>
      <QueryState query={query}>
        {(!query.data?.items || query.data.items.length === 0) && (
          <p className="muted" style={{ margin: 0, fontSize: '13px', color: '#64748b' }}>
            No changes have been recorded for this section yet.
          </p>
        )}
        {query.data?.items && query.data.items.length > 0 && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
            {query.data.items.map((r) => (
              <div
                key={r.id}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '10px 14px',
                  background: '#f8fafc',
                  border: '1px solid #f1f5f9',
                  borderRadius: '6px',
                }}
              >
                <div>
                  <button
                    className="hr-text-button"
                    style={{
                      fontWeight: 600,
                      color: '#0f172a',
                      fontSize: '13px',
                      cursor: 'pointer',
                      padding: 0,
                      border: 'none',
                      background: 'transparent',
                    }}
                    onClick={() => setSelected(r.id)}
                  >
                    Effective {r.effectiveFrom}
                  </button>
                  <div style={{ fontSize: '12px', color: '#64748b', marginTop: '2px' }}>
                    {labelDate(r.recordedAt)} &middot; Actor {r.actorId}
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setSelected(r.id)}
                  style={{
                    padding: '4px 10px',
                    fontSize: '12px',
                    borderRadius: '4px',
                    border: '1px solid #cbd5e1',
                    background: '#ffffff',
                    color: '#334155',
                    cursor: 'pointer',
                    fontWeight: 500,
                  }}
                >
                  View diff
                </button>
              </div>
            ))}
          </div>
        )}
        {query.data && query.data.totalCount > 10 && (
          <div style={{ marginTop: '12px' }}>
            <Pagination
              page={page}
              pageSize={10}
              total={query.data.totalCount}
              onChange={setPage}
            />
          </div>
        )}
      </QueryState>
      <Dialog
        open={!!selected}
        onOpenChange={(open) => {
          if (!open) setSelected(null);
        }}
        title="Change Evidence & Audit Slip"
        description={`Audit record for ${section.label}`}
        className="change-evidence-dialog-wide"
      >
        <QueryState query={detail}>
          {detail.data && (
            <ChangeEvidenceViewer
              data={detail.data}
              section={section}
              onClose={() => setSelected(null)}
            />
          )}
        </QueryState>
      </Dialog>
    </div>
  );
}
