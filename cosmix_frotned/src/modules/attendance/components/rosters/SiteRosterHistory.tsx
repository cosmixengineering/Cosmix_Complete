import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { CalendarPlus, History, Replace, StopCircle, Undo2, Users } from 'lucide-react';
import type { SiteRosterHistory as RosterHistory } from '../../schemas/site-rosters';
import { businessToday } from '../../utils/messages';
import { Command } from '../SchedulingUi';

export type RosterOperation =
  | { mode: 'assign'; assignmentId?: never }
  | { mode: 'replace' | 'end'; assignmentId: string }
  | { mode: 'temporary'; changeId?: never }
  | { mode: 'cancel' | 'supersede'; changeId: string };
export function StaffManifest({
  ids,
  title = 'Affected employee manifest',
}: {
  ids: readonly string[];
  title?: string;
}) {
  return (
    <details>
      <summary title={title}>
        <Users size={14} aria-hidden="true" /> {title} ({ids.length})
      </summary>
      {ids.length ? (
        <ul className="attendance-manifest">
          {ids.map((id) => (
            <li key={id}>
              <EmployeeReference employeeId={id} />
            </li>
          ))}
        </ul>
      ) : (
        <p>No employees in the returned manifest.</p>
      )}
    </details>
  );
}
export function SiteRosterHistory({
  history,
  canManage,
  busy,
  onAction,
}: {
  history: RosterHistory;
  canManage: boolean;
  busy: boolean;
  onAction: (operation: RosterOperation) => void;
}) {
  const today = businessToday();
  return (
    <>
      {canManage && (
        <div className="attendance-commands">
          <Command icon={CalendarPlus} disabled={busy} onClick={() => onAction({ mode: 'assign' })}>
            Assign shift
          </Command>
          <Command
            icon={CalendarPlus}
            disabled={busy}
            onClick={() => onAction({ mode: 'temporary' })}
          >
            Temporary change
          </Command>
        </div>
      )}
      <section className="attendance-section">
        <h2>Site Assignment History</h2>
        {!history.assignments.length ? (
          <p>No site schedule assignments recorded.</p>
        ) : (
          <div className="hr-table-scroll">
            <table className="attendance-table">
              <thead>
                <tr>
                  <th>Shift / assignment</th>
                  <th>Effective interval</th>
                  <th>Calendar revision</th>
                  <th>Evidence</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {history.assignments.map((row) => (
                  <tr key={row.id}>
                    <td>
                      {row.shiftId}
                      <details>
                        <summary>Assignment ID</summary>
                        {row.id}
                      </details>
                    </td>
                    <td>
                      {row.effectiveFrom} to {row.effectiveTo ?? 'open'} (exclusive)
                    </td>
                    <td>{row.holidayCalendarId ?? 'None pinned'}</td>
                    <td>
                      {row.legacyApprovalLinked
                        ? 'Legacy approval-linked'
                        : 'Direct recorded assignment'}
                    </td>
                    <td>
                      {canManage && (!row.effectiveTo || row.effectiveTo > today) && (
                        <div className="attendance-commands">
                          <Command
                            icon={Replace}
                            variant="ghost"
                            disabled={busy}
                            onClick={() => onAction({ mode: 'replace', assignmentId: row.id })}
                          >
                            Replace
                          </Command>
                          <Command
                            icon={StopCircle}
                            variant="ghost"
                            disabled={busy}
                            onClick={() => onAction({ mode: 'end', assignmentId: row.id })}
                          >
                            End
                          </Command>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      <section className="attendance-section">
        <h2>Temporary Changes</h2>
        {!history.temporaryChanges.length ? (
          <p>No temporary site changes recorded.</p>
        ) : (
          <div className="hr-table-scroll">
            <table className="attendance-table">
              <thead>
                <tr>
                  <th>Business date / ID</th>
                  <th>Versions</th>
                  <th>Status / evidence</th>
                  <th>Recorded</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {history.temporaryChanges.map((row) => (
                  <tr key={row.id}>
                    <td>
                      {row.businessDate}
                      <details>
                        <summary>Change ID</summary>
                        {row.id}
                      </details>
                    </td>
                    <td>
                      Original: {row.originalShiftVersionId}
                      <br />
                      Replacement: {row.replacementShiftVersionId}
                    </td>
                    <td>
                      {row.status}
                      <br />
                      {row.legacyApprovalLinked
                        ? 'Legacy approval-linked'
                        : 'Direct recorded change'}
                      <StaffManifest ids={row.affectedEmployeeIds} />
                      {row.supersededByChangeId && <p>Successor: {row.supersededByChangeId}</p>}
                      {row.amendmentId && <p>Amendment: {row.amendmentId}</p>}
                    </td>
                    <td>
                      {row.recordedAt}
                      <br />
                      {row.recordedBy}
                    </td>
                    <td>
                      {canManage && row.status === 'active' && row.businessDate > today && (
                        <div className="attendance-commands">
                          <Command
                            icon={Undo2}
                            variant="ghost"
                            disabled={busy}
                            onClick={() => onAction({ mode: 'cancel', changeId: row.id })}
                          >
                            Cancel change
                          </Command>
                          <Command
                            icon={Replace}
                            variant="ghost"
                            disabled={busy}
                            onClick={() => onAction({ mode: 'supersede', changeId: row.id })}
                          >
                            Supersede
                          </Command>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
      <section className="attendance-section">
        <h2>
          <History size={18} aria-hidden="true" /> Immutable Amendments
        </h2>
        {!history.temporaryAmendments.length ? (
          <p>No amendments recorded.</p>
        ) : (
          <div className="hr-table-scroll">
            <table className="attendance-table">
              <thead>
                <tr>
                  <th>Amendment</th>
                  <th>Original / replacement change</th>
                  <th>Recorded</th>
                </tr>
              </thead>
              <tbody>
                {history.temporaryAmendments.map((row) => (
                  <tr key={row.id}>
                    <td>{row.id}</td>
                    <td>
                      {row.originalChangeId}
                      <br />
                      {row.replacementChangeId ?? 'Cancelled; base restored'}
                    </td>
                    <td>
                      {row.recordedAt}
                      <br />
                      {row.recordedBy}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  );
}
