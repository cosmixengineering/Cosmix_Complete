import { useState } from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import {
  Plus,
  Trash2,
  Calendar as CalendarIcon,
  CalendarDays,
  X,
  History,
  Check,
  Eye,
} from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { Pagination } from '@/components/data-display/Pagination';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrQuery, useHrAction } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { pageOf } from '../../schemas/contracts';
import { calendarSummary, calendarSchema, type Calendar } from '../../schemas/organization';
import { QueryState, ActionMessage } from '../Feedback';
import { OrganizationHistory } from './OrganizationHistory';

export function HolidayCalendars() {
  const access = useHrAccess();
  const [page, setPage] = useState(1);
  const [history, setHistory] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [editingCalendar, setEditingCalendar] = useState<Calendar | null>(null);

  const query = useHrQuery(
    `holiday-calendars?page=${page}&pageSize=20&includeHistory=${history}`,
    pageOf(calendarSummary),
    ['hr.holidays.read'],
  );

  return (
    <div className="org-content-card">
      {/* Table Section Header */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: '14px',
          flexWrap: 'wrap',
          gap: '10px',
        }}
      >
        <div>
          <h2 style={{ fontSize: '16px', fontWeight: 700, color: '#0f172a', margin: 0 }}>
            Holiday calendars
          </h2>
          <label
            className="hr-check"
            style={{
              marginTop: '6px',
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              fontSize: '12px',
              color: '#64748b',
              cursor: 'pointer',
              userSelect: 'none',
            }}
          >
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
        </div>
        {access.can('hr.holidays.manage') && (
          <button
            type="button"
            className="org-add-btn"
            onClick={() => setCreating(true)}
          >
            <Plus size={15} strokeWidth={2.5} />
            New calendar
          </button>
        )}
      </div>

      <QueryState query={query}>
        {query.data && (
          <>
            <div className="org-table-container">
              <table className="org-table">
                <thead>
                  <tr>
                    <th>CALENDAR</th>
                    <th>CODE</th>
                    <th>PERIOD</th>
                    <th>REVISION</th>
                    <th>STATUS</th>
                    <th style={{ textAlign: 'right' }}>ACTIONS</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.length === 0 ? (
                    <tr>
                      <td
                        colSpan={6}
                        style={{ textAlign: 'center', padding: '36px 18px', color: '#94a3b8' }}
                      >
                        No holiday calendars found.
                      </td>
                    </tr>
                  ) : (
                    query.data.items.map((r) => (
                      <tr key={r.id}>
                        <td>
                          <button
                            type="button"
                            className="org-table-name-btn"
                            onClick={() => setSelectedId(r.id)}
                          >
                            {r.name}
                          </button>
                        </td>
                        <td>
                          <span className="org-table-code">{r.code}</span>
                        </td>
                        <td style={{ fontSize: '12px', color: '#475569' }}>
                          {r.effectiveFrom} — {r.effectiveTo ?? 'Open-ended'}
                        </td>
                        <td>
                          <span
                            style={{
                              fontSize: '11.5px',
                              fontWeight: 600,
                              color: '#64748b',
                              background: '#f1f5f9',
                              padding: '2px 7px',
                              borderRadius: '4px',
                            }}
                          >
                            Rev {r.revision}
                          </span>
                        </td>
                        <td>
                          <span className={`org-badge-status ${r.isActive ? 'active' : 'inactive'}`}>
                            <span className="org-badge-dot"></span>
                            {r.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td>
                          <div
                            className="org-actions-cell"
                            style={{ justifyContent: 'flex-end', gap: '6px' }}
                          >
                            <button
                              type="button"
                              className="org-action-icon-btn edit"
                              title={`View ${r.name} details`}
                              aria-label={`View ${r.name} details`}
                              onClick={() => setSelectedId(r.id)}
                            >
                              <Eye size={14} />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            <Pagination
              page={page}
              pageSize={20}
              total={query.data.totalCount}
              onChange={setPage}
            />
          </>
        )}
      </QueryState>

      {/* New Calendar Popup Modal */}
      {creating && (
        <CalendarEditor
          onClose={() => setCreating(false)}
          onSaved={() => {
            setCreating(false);
            void query.refetch();
          }}
        />
      )}

      {/* Calendar Detail Popup Modal */}
      {selectedId && (
        <CalendarDetailModal
          id={selectedId}
          onClose={() => setSelectedId(null)}
          onEditRevision={(calendar) => {
            setSelectedId(null);
            setEditingCalendar(calendar);
          }}
        />
      )}

      {/* Revision Editor Popup Modal */}
      {editingCalendar && (
        <CalendarEditor
          value={editingCalendar}
          onClose={() => setEditingCalendar(null)}
          onSaved={() => {
            setEditingCalendar(null);
            void query.refetch();
          }}
        />
      )}
    </div>
  );
}

function CalendarDetailModal({
  id,
  onClose,
  onEditRevision,
}: {
  id: string;
  onClose: () => void;
  onEditRevision: (calendar: Calendar) => void;
}) {
  const access = useHrAccess();
  const [showHistory, setShowHistory] = useState(false);
  const query = useHrQuery('holiday-calendars/' + id, calendarSchema, ['hr.holidays.read']);

  return (
    <DialogPrimitive.Root open onOpenChange={(isOpen) => !isOpen && onClose()}>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="dialog-overlay" />
        <DialogPrimitive.Content className="org-modal-content org-modal-wide">
          <QueryState query={query}>
            {query.data && (
              <>
                <div className="org-modal-header">
                  <div className="org-modal-header-left">
                    <div className="org-modal-icon-badge">
                      <CalendarIcon size={20} strokeWidth={2} />
                    </div>
                    <div className="org-modal-titles">
                      <DialogPrimitive.Title className="org-modal-title">
                        {query.data.calendar.name}
                      </DialogPrimitive.Title>
                      <DialogPrimitive.Description className="org-modal-desc">
                        Code: <strong>{query.data.calendar.code}</strong> • Revision {query.data.calendar.revision} •{' '}
                        <span
                          className={`org-status-pill-${query.data.calendar.isActive ? 'active' : 'inactive'}`}
                        >
                          {query.data.calendar.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </DialogPrimitive.Description>
                    </div>
                  </div>
                  <button
                    type="button"
                    className="org-modal-close-btn"
                    aria-label="Close dialog"
                    onClick={onClose}
                  >
                    <X size={18} />
                  </button>
                </div>

                <div className="org-modal-body-scroll">
                  <div className="cal-detail-card">
                    <div className="cal-detail-summary">
                      <div className="cal-detail-item">
                        <span className="cal-detail-label">Period</span>
                        <span className="cal-detail-value">
                          {query.data.calendar.effectiveFrom} — {query.data.calendar.effectiveTo ?? 'Open-ended'}
                        </span>
                      </div>
                      <div className="cal-detail-item">
                        <span className="cal-detail-label">Notification Reference</span>
                        <span className="cal-detail-value">
                          {query.data.governmentNotificationReference || 'None'}
                        </span>
                      </div>
                    </div>

                    <div className="cal-holidays-section" style={{ marginTop: '8px' }}>
                      <div className="cal-holidays-header">
                        <div className="cal-holidays-title-row">
                          <CalendarDays size={15} style={{ color: '#2563eb' }} />
                          <span className="cal-holidays-title">Observed Holidays</span>
                          <span className="cal-count-badge">
                            {query.data.holidays.length} observed
                          </span>
                        </div>
                      </div>

                      <div className="cal-holidays-scroll-box" style={{ background: '#ffffff' }}>
                        {query.data.holidays.length === 0 ? (
                          <div className="cal-empty-holidays">No holidays listed in this calendar.</div>
                        ) : (
                          <table className="org-table" style={{ margin: 0 }}>
                            <thead>
                              <tr>
                                <th>DATE</th>
                                <th>HOLIDAY</th>
                                <th>NOTIFICATION</th>
                              </tr>
                            </thead>
                            <tbody>
                              {query.data.holidays.map((h) => (
                                <tr key={h.id}>
                                  <td style={{ fontWeight: 600, color: '#0f172a' }}>{h.date}</td>
                                  <td>{h.name}</td>
                                  <td style={{ color: '#64748b' }}>{h.notificationReference}</td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        )}
                      </div>
                    </div>

                    {showHistory && (
                      <div style={{ marginTop: '12px', borderTop: '1px solid #e2e8f0', paddingTop: '10px' }}>
                        <OrganizationHistory path={'holiday-calendars/' + id} permission="hr.holidays.read" />
                      </div>
                    )}
                  </div>
                </div>

                <div className="org-modal-footer">
                  <button
                    type="button"
                    className="org-btn-cancel"
                    onClick={() => setShowHistory((prev) => !prev)}
                  >
                    <History size={13} style={{ marginRight: 4 }} />
                    {showHistory ? 'Hide history' : 'History'}
                  </button>
                  <button type="button" className="org-btn-cancel" onClick={onClose}>
                    Close
                  </button>
                  {access.can('hr.holidays.manage') && (
                    <button
                      type="button"
                      className="org-btn-submit"
                      onClick={() => onEditRevision(query.data!)}
                    >
                      <Plus size={14} /> New revision
                    </button>
                  )}
                </div>
              </>
            )}
          </QueryState>
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}

export function CalendarEditor({
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
    if (!action.pending && (!dirty || window.confirm('Discard unsaved calendar changes?'))) {
      onClose();
    }
  };

  const dialogTitle = value ? 'New calendar revision' : 'New holiday calendar';
  const dialogSubtitle = value
    ? `Create revision ${value.calendar.revision + 1} for ${value.calendar.name}.`
    : 'Define the operational calendar and statutory observed dates.';

  return (
    <DialogPrimitive.Root
      open
      onOpenChange={(isOpen) => {
        if (!isOpen) close();
      }}
    >
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="dialog-overlay" />
        <DialogPrimitive.Content className="org-modal-content org-modal-wide">
          <div className="org-modal-header">
            <div className="org-modal-header-left">
              <div className="org-modal-icon-badge">
                <CalendarIcon size={20} strokeWidth={2} />
              </div>
              <div className="org-modal-titles">
                <DialogPrimitive.Title className="org-modal-title">
                  {dialogTitle}
                </DialogPrimitive.Title>
                <DialogPrimitive.Description className="org-modal-desc">
                  {dialogSubtitle}
                </DialogPrimitive.Description>
              </div>
            </div>
            <button
              type="button"
              className="org-modal-close-btn"
              aria-label="Close dialog"
              onClick={close}
            >
              <X size={18} />
            </button>
          </div>

          <form
            className="org-modal-form"
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
            <div className="org-modal-body-scroll">
              <fieldset disabled={action.pending} className="org-modal-fieldset">
                <div className="org-modal-fields">
                  <div className="org-modal-grid-2">
                    <TextInput
                      label="Calendar code"
                      value={code}
                      required
                      placeholder="e.g. CAL-2027"
                      maxLength={40}
                      pattern="[A-Za-z0-9][A-Za-z0-9_.-]{0,39}"
                      disabled={Boolean(value)}
                      onChange={(e) => setCode(e.target.value)}
                    />
                    <TextInput
                      label="Calendar name"
                      value={name}
                      required
                      placeholder="e.g. National Holidays 2027"
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
                    <div className="org-modal-col-full">
                      <TextInput
                        label="Government notification reference"
                        value={reference}
                        required
                        placeholder="e.g. Gazetted Notification No. 14/2026-Public"
                        maxLength={500}
                        onChange={(e) => setReference(e.target.value)}
                      />
                    </div>
                    {value && (
                      <>
                        <div className="org-modal-col-full">
                          <TextInput
                            label="Revision reason"
                            required
                            placeholder="Reason for revision..."
                            maxLength={500}
                            value={reason}
                            onChange={(e) => setReason(e.target.value)}
                          />
                        </div>
                        <div className="org-modal-col-full">
                          <label className="hr-check">
                            <input
                              type="checkbox"
                              checked={active}
                              onChange={(e) => setActive(e.target.checked)}
                            />
                            Active calendar status
                          </label>
                        </div>
                      </>
                    )}
                  </div>

                  <div className="cal-holidays-section">
                    <div className="cal-holidays-header">
                      <div className="cal-holidays-title-row">
                        <CalendarDays size={15} style={{ color: '#2563eb' }} />
                        <span className="cal-holidays-title">Observed Holidays</span>
                        <span className="cal-count-badge">{rows.length} added</span>
                      </div>
                      <button
                        type="button"
                        className="cal-add-holiday-btn"
                        disabled={rows.length >= 400 || action.pending}
                        onClick={() => {
                          setDirty(true);
                          setRows((prev) => [
                            ...prev,
                            {
                              key: crypto.randomUUID(),
                              date: '',
                              name: '',
                              notificationReference: '',
                            },
                          ]);
                        }}
                      >
                        <Plus size={13} strokeWidth={2.5} /> Add holiday
                      </button>
                    </div>

                    <div className="cal-holidays-scroll-box">
                      {rows.length === 0 ? (
                        <div className="cal-empty-holidays">
                          No holidays added yet. Click <strong>+ Add holiday</strong> to define specific dates.
                        </div>
                      ) : (
                        rows.map((r, i) => (
                          <div className="cal-holiday-row" key={r.key}>
                            <input
                              type="date"
                              required
                              value={r.date}
                              aria-label={`Holiday ${i + 1} date`}
                              onChange={(e) =>
                                setRows((prev) =>
                                  prev.map((x, j) =>
                                    j === i ? { ...x, date: e.target.value } : x,
                                  ),
                                )
                              }
                              className="cal-holiday-input-date"
                            />
                            <input
                              type="text"
                              required
                              placeholder="Holiday title (e.g. Eid-ul-Fitr)"
                              maxLength={200}
                              value={r.name}
                              aria-label={`Holiday ${i + 1} title`}
                              onChange={(e) =>
                                setRows((prev) =>
                                  prev.map((x, j) =>
                                    j === i ? { ...x, name: e.target.value } : x,
                                  ),
                                )
                              }
                              className="cal-holiday-input-text"
                            />
                            <input
                              type="text"
                              required
                              placeholder="Notification ref"
                              maxLength={500}
                              value={r.notificationReference}
                              aria-label={`Holiday ${i + 1} notification reference`}
                              onChange={(e) =>
                                setRows((prev) =>
                                  prev.map((x, j) =>
                                    j === i
                                      ? { ...x, notificationReference: e.target.value }
                                      : x,
                                  ),
                                )
                              }
                              className="cal-holiday-input-text"
                            />
                            <button
                              type="button"
                              className="cal-holiday-del-btn"
                              title="Remove holiday"
                              aria-label="Remove holiday"
                              onClick={() => {
                                setDirty(true);
                                setRows((prev) => prev.filter((_, j) => j !== i));
                              }}
                            >
                              <Trash2 size={14} />
                            </button>
                          </div>
                        ))
                      )}
                    </div>
                  </div>

                  <ActionMessage action={action} />
                </div>
              </fieldset>
            </div>

            <div className="org-modal-footer">
              <button
                type="button"
                className="org-btn-cancel"
                disabled={action.pending}
                onClick={close}
              >
                Cancel
              </button>
              <button
                type="submit"
                className="org-btn-submit"
                disabled={action.pending}
              >
                <Check size={14} />
                {action.pending
                  ? 'Publishing...'
                  : value
                    ? 'Publish revision'
                    : 'Publish calendar'}
              </button>
            </div>
          </form>
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}
