import { useEffect, useState } from 'react';
import { Eye, Plus, Search, Undo2, Wrench } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import {
  Command,
  Denied,
  PrivateWorkspace,
  QueryState,
  SavedNotice,
} from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { date } from '../../schemas/common';
import { canAll } from '../leave-policy/inputs';
import { EvidenceRecord, Reload } from '../leave-policy/Ui';
import { EmployeeWorkspace } from '../review/ReviewUi';
import {
  benefits,
  benefit,
  benefitHistoryPath,
  benefitRoot,
  permissions,
  type Benefit,
} from './api';
import { AwardBenefitForm, BenefitChangeForm } from './BenefitForms';

export function LeaveBenefitsPage() {
  return (
    <PrivateWorkspace resource="leave-benefits">
      <BenefitAccess />
    </PrivateWorkspace>
  );
}
function BenefitAccess() {
  const access = useAttendanceAccess();
  if (!canAll(access.user, permissions.benefits))
    return <Denied>Benefit management requires All-scoped benefit review permission.</Denied>;
  return (
    <EmployeeWorkspace title="Employee leave benefits" permission={permissions.benefits}>
      {(employeeId, _scope, onEditing) => (
        <BenefitHistory key={employeeId} employeeId={employeeId} onEditing={onEditing} />
      )}
    </EmployeeWorkspace>
  );
}
function BenefitHistory({
  employeeId,
  onEditing,
}: {
  employeeId: string;
  onEditing?: (editing: boolean) => void;
}) {
  const access = useAttendanceAccess();
  const [year, setYear] = useState('');
  const [loadedYear, setLoadedYear] = useState('');
  const [operation, setOperation] = useState<
    'award' | { mode: 'revoke' | 'correct'; row: Benefit } | null
  >(null);
  const [saved, setSaved] = useState<Benefit | null>(null);
  const allowed = access.ready && canAll(access.user, permissions.benefits);
  const canWrite =
    allowed &&
    Boolean(access.user?.account?.employeeId) &&
    access.user?.account?.employeeId !== employeeId;
  const validYear = date.safeParse(year).success && year >= '1901-01-01' && year <= '9997-12-31';
  const history = useAttendanceQuery(
    loadedYear ? benefitHistoryPath(employeeId, loadedYear) : benefitRoot(employeeId),
    benefits,
    allowed && Boolean(loadedYear),
  );
  const visibleSaved = history.isError ? null : saved;
  useEffect(() => {
    if (history.isError && saved) setSaved(null);
  }, [history.isError, saved]);
  function open(next: NonNullable<typeof operation>) {
    if (!canWrite) return;
    setSaved(null);
    setOperation(next);
    onEditing?.(true);
  }
  function close() {
    setOperation(null);
    onEditing?.(false);
    if (loadedYear) void history.refetch();
  }
  function onSaved(result: unknown) {
    const row = benefit.parse(result);
    setSaved(row);
    setLoadedYear(row.leaveYearFrom);
    setYear(row.leaveYearFrom);
    close();
  }
  if (!allowed) return <Denied />;
  return (
    <>
      <p>Employee: {employeeId}</p>
      {!canWrite && (
        <p role="status">
          {access.user?.account?.employeeId === employeeId
            ? 'Your own employee record is read-only in benefit management.'
            : 'Benefit writes require an identified independent reviewer.'}
        </p>
      )}
      <SavedNotice
        message={visibleSaved ? `Benefit ${visibleSaved.id}: ${visibleSaved.status}.` : ''}
      />
      {visibleSaved && (
        <details>
          <summary>Saved response</summary>
          <EvidenceRecord value={visibleSaved} />
        </details>
      )}
      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (validYear && !operation) {
            setSaved(null);
            setLoadedYear(year);
            if (year === loadedYear) void history.refetch();
          }
        }}
      >
        <fieldset disabled={Boolean(operation) || history.isFetching} className="attendance-grid">
          <TextInput
            label="Leave year starts on"
            type="date"
            required
            min="1901-01-01"
            max="9997-12-31"
            value={year}
            onChange={(event) => setYear(event.target.value)}
          />
          <Command icon={Search} type="submit" disabled={!validYear}>
            Load benefit history
          </Command>
        </fieldset>
      </form>
      <div className="attendance-commands">
        <Command
          icon={Plus}
          disabled={!canWrite || Boolean(operation)}
          onClick={() => open('award')}
        >
          Award configured benefit
        </Command>
        <Reload
          disabled={!loadedYear || Boolean(operation) || history.isFetching}
          onClick={() => {
            if (loadedYear) void history.refetch();
          }}
        />
      </div>
      {operation === 'award' && (
        <AwardBenefitForm employeeId={employeeId} onSaved={onSaved} onCancel={close} />
      )}
      {operation && operation !== 'award' && (
        <BenefitChangeForm
          mode={operation.mode}
          row={operation.row}
          onSaved={onSaved}
          onCancel={close}
        />
      )}
      {loadedYear && (
        <section className="attendance-section">
          <h2>Benefit history from {loadedYear}</h2>
          <QueryState
            query={history}
            empty={history.data?.length === 0}
            emptyText="No benefit awards returned for this leave year."
          />
          {!history.isError && history.data && (
            <div className="hr-table-scroll">
              <table className="attendance-table">
                <thead>
                  <tr>
                    <th>Award / source year</th>
                    <th>Leave year</th>
                    <th>Award minutes</th>
                    <th>Status</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {history.data.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <details>
                          <summary>
                            <Eye size={14} aria-hidden="true" /> {row.id}
                          </summary>
                          <EvidenceRecord value={row} />
                        </details>
                        {row.sourceFrom} to {row.sourceToExclusive} (exclusive)
                      </td>
                      <td>
                        {row.leaveYearFrom} to {row.leaveYearToExclusive} (exclusive)
                        <br />
                        Available {row.availableFrom}
                      </td>
                      <td>
                        {row.minutes}
                        <br />
                        {row.standardDayMinutes} minutes per policy day
                      </td>
                      <td>
                        {row.status}
                        {row.correction && (
                          <>
                            <br />
                            {row.correction.status}
                            <br />
                            Protected consumed minutes: {row.correction.protectedConsumedMinutes}
                          </>
                        )}
                      </td>
                      <td>
                        {row.status === 'awarded' && canWrite && (
                          <div className="attendance-commands">
                            <Command
                              icon={Undo2}
                              variant="secondary"
                              disabled={Boolean(operation) || history.isFetching}
                              onClick={() => open({ mode: 'revoke', row: structuredClone(row) })}
                            >
                              Revoke unused award
                            </Command>
                            <Command
                              icon={Wrench}
                              variant="secondary"
                              disabled={Boolean(operation) || history.isFetching}
                              onClick={() => open({ mode: 'correct', row: structuredClone(row) })}
                            >
                              Review correction
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
      )}
    </>
  );
}
