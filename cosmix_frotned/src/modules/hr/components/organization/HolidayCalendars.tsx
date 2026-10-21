import { useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { DataTable } from '@/components/data-display/DataTable';
import { Pagination } from '@/components/data-display/Pagination';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrQuery, useHrAction } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { pageOf } from '../../schemas/contracts';
import { calendarSummary, calendarSchema, type Calendar } from '../../schemas/organization';
import { QueryState, ActionMessage } from '../Feedback';
import { OrganizationHistory } from './OrganizationHistory';
import { RecordView } from '../RecordView';

export function HolidayCalendars() {
  const access = useHrAccess();
  const [page, setPage] = useState(1);
  const [history, setHistory] = useState(false);
  const [selected, setSelected] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const query = useHrQuery(
    `holiday-calendars?page=${page}&pageSize=20&includeHistory=${history}`,
    pageOf(calendarSummary),
    ['hr.holidays.read'],
  );
  if (creating)
    return (
      <CalendarEditor
        onClose={() => setCreating(false)}
        onSaved={() => {
          setCreating(false);
          void query.refetch();
        }}
      />
    );
  if (selected)
    return (
      <CalendarDetail
        id={selected}
        onClose={() => {
          setSelected(null);
          void query.refetch();
        }}
      />
    );
  return (
    <>
      <div className="hr-page-heading">
        <h2>Holiday calendars</h2>
        {access.can('hr.holidays.manage') && (
          <Button onClick={() => setCreating(true)}>
            <Plus size={16} />
            New calendar
          </Button>
        )}
      </div>
      <label className="hr-check">
        <input
          type="checkbox"
          checked={history}
          onChange={(e) => {
            setHistory(e.target.checked);
            setPage(1);
          }}
        />
        Include earlier revisions
      </label>
      <QueryState query={query}>
        {query.data && (
          <>
            <DataTable
              caption="Holiday calendars"
              rows={query.data.items}
              rowKey={(r) => r.id}
              columns={[
                {
                  id: 'name',
                  header: 'CALENDAR',
                  cell: (r) => (
                    <button className="hr-text-button" onClick={() => setSelected(r.id)}>
                      {r.name}
                    </button>
                  ),
                },
                { id: 'code', header: 'CODE', cell: (r) => r.code },
                {
                  id: 'period',
                  header: 'PERIOD',
                  cell: (r) => `${r.effectiveFrom} - ${r.effectiveTo ?? 'Open-ended'}`,
                },
                { id: 'revision', header: 'REVISION', cell: (r) => r.revision },
                {
                  id: 'state',
                  header: 'STATUS',
                  cell: (r) => (r.isActive ? 'Active' : 'Inactive'),
                },
              ]}
            />
            {!query.data.items.length && <p className="hr-notice">No calendars found.</p>}
            <Pagination
              page={page}
              pageSize={20}
              total={query.data.totalCount}
              onChange={setPage}
            />
          </>
        )}
      </QueryState>
    </>
  );
}
function CalendarDetail({ id, onClose }: { id: string; onClose: () => void }) {
  const access = useHrAccess();
  const [editing, setEditing] = useState(false);
  const query = useHrQuery('holiday-calendars/' + id, calendarSchema, ['hr.holidays.read']);
  return (
    <QueryState query={query}>
      {query.data &&
        (editing ? (
          <CalendarEditor value={query.data} onClose={() => setEditing(false)} onSaved={onClose} />
        ) : (
          <>
            <Button variant="ghost" onClick={onClose}>
              Back to calendars
            </Button>
            <div className="hr-page-heading">
              <h2>{query.data.calendar.name}</h2>
              {access.can('hr.holidays.manage') && (
                <Button onClick={() => setEditing(true)}>New revision</Button>
              )}
            </div>
            <RecordView
              value={{
                ...query.data.calendar,
                governmentNotificationReference: query.data.governmentNotificationReference,
              }}
              omit={['version', 'id', 'supersedesCalendarId']}
            />
            <DataTable
              caption="Calendar holidays"
              rows={query.data.holidays}
              rowKey={(r) => r.id}
              columns={[
                { id: 'date', header: 'DATE', cell: (r) => r.date },
                { id: 'name', header: 'HOLIDAY', cell: (r) => r.name },
                { id: 'ref', header: 'NOTIFICATION', cell: (r) => r.notificationReference },
              ]}
            />
            <OrganizationHistory path={'holiday-calendars/' + id} permission="hr.holidays.read" />
          </>
        ))}
    </QueryState>
  );
}
function CalendarEditor({
  value,
  onClose,
  onSaved,
}: {
  value?: Calendar;
  onClose: () => void;
  onSaved: () => void;
}) {
  const action = useHrAction();
  const [dirty, setDirty] = useState(false);
  useDirtyForm(dirty || action.pending);
  const [code, setCode] = useState(value?.calendar.code ?? '');
  const [name, setName] = useState(value?.calendar.name ?? '');
  const [from, setFrom] = useState(value?.calendar.effectiveFrom ?? '');
  const [to, setTo] = useState(value?.calendar.effectiveTo ?? '');
  const [reference, setReference] = useState(value?.governmentNotificationReference ?? '');
  const [active, setActive] = useState(value?.calendar.isActive ?? true);
  const [reason, setReason] = useState('');
  const [rows, setRows] = useState(
    () =>
      value?.holidays.map((r) => ({
        key: r.id,
        date: r.date,
        name: r.name,
        notificationReference: r.notificationReference,
      })) ?? [],
  );
  const close = () => {
    if (!action.pending && (!dirty || window.confirm('Discard unsaved calendar changes?')))
      onClose();
  };
  return (
    <>
      <Button variant="ghost" onClick={close}>
        Back to calendars
      </Button>
      <h2>{value ? 'New calendar revision' : 'New calendar'}</h2>
      <form
        onChange={() => setDirty(true)}
        onSubmit={async (e) => {
          e.preventDefault();
          const common = {
            name,
            governmentNotificationReference: reference,
            holidays: rows.map(({ date, name, notificationReference }) => ({
              date,
              name,
              notificationReference,
            })),
          };
          const body = value
            ? { ...common, isActive: active, reason, expectedVersion: value.calendar.version }
            : { ...common, code, effectiveFrom: from, effectiveTo: to || null };
          const result = await action.run(
            'holiday-calendars' + (value ? '/' + value.calendar.id + '/revisions' : ''),
            body,
            'POST',
            calendarSchema,
          );
          if (result) onSaved();
        }}
      >
        <fieldset disabled={action.pending}>
          <div className="hr-form-grid">
            <TextInput
              label="Code"
              value={code}
              required
              maxLength={40}
              pattern="[A-Za-z0-9][A-Za-z0-9_.-]{0,39}"
              disabled={Boolean(value)}
              onChange={(e) => setCode(e.target.value)}
            />
            <TextInput
              label="Name"
              value={name}
              required
              maxLength={200}
              onChange={(e) => setName(e.target.value)}
            />
            <TextInput
              label="Effective from"
              type="date"
              required
              disabled={Boolean(value)}
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            />
            <TextInput
              label="End date (exclusive)"
              type="date"
              min={from}
              disabled={Boolean(value)}
              value={to}
              onChange={(e) => setTo(e.target.value)}
            />
            <TextInput
              label="Government notification reference"
              value={reference}
              required
              maxLength={500}
              onChange={(e) => setReference(e.target.value)}
            />
            {value && (
              <>
                <TextInput
                  label="Revision reason"
                  required
                  maxLength={500}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
                <label className="hr-check">
                  <input
                    type="checkbox"
                    checked={active}
                    onChange={(e) => setActive(e.target.checked)}
                  />
                  Active
                </label>
              </>
            )}
          </div>
          <section className="hr-form-section">
            <h3>Holidays</h3>
            {rows.map((r, i) => (
              <div className="hr-repeat" key={r.key}>
                <div className="hr-form-grid">
                  <TextInput
                    label="Date"
                    type="date"
                    required
                    value={r.date}
                    onChange={(e) =>
                      setRows(rows.map((x, j) => (j === i ? { ...x, date: e.target.value } : x)))
                    }
                  />
                  <TextInput
                    label="Holiday name"
                    required
                    maxLength={200}
                    value={r.name}
                    onChange={(e) =>
                      setRows(rows.map((x, j) => (j === i ? { ...x, name: e.target.value } : x)))
                    }
                  />
                  <TextInput
                    label="Notification reference"
                    required
                    maxLength={500}
                    value={r.notificationReference}
                    onChange={(e) =>
                      setRows(
                        rows.map((x, j) =>
                          j === i ? { ...x, notificationReference: e.target.value } : x,
                        ),
                      )
                    }
                  />
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setDirty(true);
                      setRows(rows.filter((_, j) => j !== i));
                    }}
                  >
                    <Trash2 size={16} />
                    Remove holiday
                  </Button>
                </div>
              </div>
            ))}
            <Button
              variant="secondary"
              disabled={rows.length >= 400}
              onClick={() => {
                setDirty(true);
                setRows([
                  ...rows,
                  { key: crypto.randomUUID(), date: '', name: '', notificationReference: '' },
                ]);
              }}
            >
              <Plus size={16} />
              Add holiday
            </Button>
          </section>
          <ActionMessage action={action} />
          <div className="hr-actions">
            <Button type="submit">{action.pending ? 'Publishing...' : 'Publish calendar'}</Button>
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
          </div>
        </fieldset>
      </form>
    </>
  );
}
