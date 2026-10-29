import { useState, useMemo } from 'react';
import {
  ArrowLeft,
  Save,
  X,
  Clock,
  Calendar,
  Check,
  AlertCircle,
  Building2,
  Info,
  RotateCcw,
  Timer,
} from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Button } from '@/components/ui/Button';
import { useAttendanceAction } from '../../hooks/use-attendance-action';
import { useDirtyForm } from '@/modules/hr/hooks/use-dirty-form';
import {
  createShiftSchema,
  shiftDetail,
  type Shift,
  type ShiftSchedule,
} from '../../schemas/shifts';
import { reason, rowVersion } from '../../schemas/common';
import { businessToday } from '../../utils/messages';
import { emptySchedule, nextDate, validateSchedule, weekdays } from '../../utils/shift-schedule';

function formatTime12(timeStr?: string): string {
  if (!timeStr) return '--:--';
  const parts = timeStr.split(':');
  const h = parseInt(parts[0] || '0', 10);
  const m = parts[1] || '00';
  const ampm = h >= 12 ? 'PM' : 'AM';
  const h12 = h % 12 || 12;
  return `${String(h12).padStart(2, '0')}:${m} ${ampm}`;
}

export function ShiftMetadataForm({
  mode,
  shift,
  onSaved,
  onCancel,
}: {
  mode: 'create' | 'rename' | 'status' | 'schedule';
  shift?: Shift;
  onSaved: (result: Shift) => void;
  onCancel: () => void;
}) {
  const [source] = useState(shift);
  const [code, setCode] = useState(mode === 'create' && shift ? `${shift.code}-COPY` : '');
  const [name, setName] = useState(
    mode === 'create' && shift ? `${shift.name} (Copy)` : (source?.name ?? ''),
  );
  const [inputReason, setReason] = useState(
    mode === 'create' && shift ? `Cloned from shift ${shift.code}` : '',
  );
  const [validationError, setValidationError] = useState('');
  const [saved, setSaved] = useState(false);

  const action = useAttendanceAction();
  useDirtyForm(!saved);

  const today = businessToday();
  const latest =
    source?.versions.reduce(
      (max, version) => (version.effectiveFrom > max ? version.effectiveFrom : max),
      '',
    ) ?? '';
  const minimum = mode === 'schedule' ? nextDate(latest > today ? latest : today) : today;

  // A copied schedule retains its saved values; a new schedule requires HR input.
  const [schedule, setSchedule] = useState<ShiftSchedule>(() => {
    if (mode === 'create' && shift && shift.versions.length > 0) {
      const sorted = [...shift.versions].sort((a, b) =>
        b.effectiveFrom.localeCompare(a.effectiveFrom),
      );
      const latestVer = sorted[0];
      if (latestVer) {
        return {
          effectiveFrom: minimum,
          startTime: latestVer.startTime,
          endTime: latestVer.endTime,
          endDayOffset: latestVer.endDayOffset,
          scheduledMinutes: latestVer.scheduledMinutes,
          paidBreakMinutes: latestVer.paidBreakMinutes,
          unpaidBreakMinutes: latestVer.unpaidBreakMinutes,
          applicableWeekdays: latestVer.applicableWeekdays,
          missingCheckoutReminderAfterMinutes: latestVer.missingCheckoutReminderAfterMinutes,
        };
      }
    }
    return emptySchedule(minimum);
  });

  const title =
    mode === 'create'
      ? 'Create Shift'
      : mode === 'rename'
        ? 'Rename Shift'
        : mode === 'status'
          ? `${source?.isActive ? 'Deactivate' : 'Activate'} Shift`
          : 'Publish Future Schedule';

  // Calculate live preview metrics
  const previewMetrics = useMemo(() => {
    const startFormatted = formatTime12(schedule.startTime);
    const endFormatted = formatTime12(schedule.endTime);

    const totalScheduled = schedule.scheduledMinutes || 0;
    const durationHours = (totalScheduled / 60).toFixed(1);
    const totalBreaks = (schedule.paidBreakMinutes || 0) + (schedule.unpaidBreakMinutes || 0);

    const activeDaysCount = weekdays.filter(
      (d) => (schedule.applicableWeekdays & d.value) !== 0,
    ).length;

    const isNight =
      (code && code.toUpperCase().includes('NGT')) ||
      (name && name.toLowerCase().includes('night')) ||
      schedule.endDayOffset === 1;

    return {
      startFormatted,
      endFormatted,
      durationHours,
      totalBreaks,
      activeDaysCount,
      isNight,
    };
  }, [schedule, code, name]);

  function build() {
    if (mode === 'create' || mode === 'schedule') {
      const error = validateSchedule(
        schedule,
        businessToday(),
        mode === 'schedule' ? latest : undefined,
      );
      if (error) throw new Error(error);
    }
    if (mode === 'create') {
      return {
        path: '/hr/shifts',
        method: 'POST' as const,
        body: createShiftSchema.parse({
          code: code.trim().toUpperCase(),
          name: name.trim(),
          initialSchedule: schedule,
          reason: inputReason.trim(),
        }),
        schema: shiftDetail,
      };
    }
    if (!source) throw new Error('Reload the shift before editing.');
    const expectedVersion = rowVersion.parse(source.version);
    const base = { expectedVersion, reason: reason(500).parse(inputReason.trim()) };
    return {
      path:
        `/hr/shifts/${source.id}` +
        (mode === 'rename' ? '' : mode === 'status' ? '/status' : '/versions'),
      method: mode === 'rename' ? ('PUT' as const) : ('POST' as const),
      schema: shiftDetail,
      body:
        mode === 'rename'
          ? { ...base, name: reason(150).parse(name.trim()) }
          : mode === 'status'
            ? { ...base, isActive: !source.isActive }
            : { ...base, schedule },
    };
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setValidationError('');

    if (action.pending) return;

    try {
      const intent = build();
      const result = await action.run(intent.path, intent.body, intent.method, intent.schema);
      if (result !== undefined) {
        setSaved(true);
        onSaved(shiftDetail.parse(result));
      }
    } catch (err) {
      if (err instanceof Error) {
        setValidationError(err.message);
      } else {
        setValidationError('Please review form fields and correct invalid entries.');
      }
    }
  };

  const handleCancelClick = () => {
    if (action.pending) return;
    if (window.confirm('Discard this unsaved shift form?')) {
      onCancel();
    }
  };

  const toggleWeekday = (maskValue: number) => {
    setSchedule((prev) => ({
      ...prev,
      applicableWeekdays:
        (prev.applicableWeekdays & maskValue) !== 0
          ? prev.applicableWeekdays & ~maskValue
          : prev.applicableWeekdays | maskValue,
    }));
  };

  const activeError = validationError || action.error;

  return (
    <div className="shift-editor-container" aria-label={title}>
      {/* 1. Header with Breadcrumb and Actions */}
      <div className="shift-editor-topbar">
        <div className="shift-editor-breadcrumb">
          <button
            type="button"
            className="shift-editor-back-btn"
            onClick={handleCancelClick}
            disabled={action.pending}
          >
            <ArrowLeft size={15} />
            Back
          </button>
          <span>&middot;</span>
          <span>Work Shifts</span>
          <span>/</span>
          <strong style={{ color: '#0f172a' }}>{title}</strong>
        </div>

        <div className="shift-editor-actions">
          <Button
            type="button"
            variant="secondary"
            onClick={handleCancelClick}
            disabled={action.pending}
          >
            <X size={15} />
            Cancel
          </Button>
          <Button type="button" variant="primary" onClick={handleSubmit} disabled={action.pending}>
            {action.pending ? (
              <>
                <RotateCcw size={15} className="animate-spin" />
                Saving Shift...
              </>
            ) : (
              <>
                <Save size={15} />
                Save Shift
              </>
            )}
          </Button>
        </div>
      </div>

      <div className="shift-editor-heading-wrap">
        <h1 className="shift-editor-main-title">{title}</h1>
        <p className="shift-editor-subtitle">
          Configure operating hours, breaks, working days, and versioning for this work shift.
        </p>
      </div>

      {activeError && (
        <div className="shift-editor-error-banner" role="alert">
          <AlertCircle size={18} style={{ flexShrink: 0, marginTop: '1px' }} />
          <div>
            <strong>Action could not be saved:</strong>
            <p style={{ margin: '3px 0 0' }}>{activeError}</p>
          </div>
        </div>
      )}

      {/* 2. Main 2-Column Split Layout */}
      <form onSubmit={handleSubmit}>
        <div className="shift-form-layout">
          {/* LEFT: Main Form Card */}
          <div className="shift-form-main-card">
            {source && (
              <div
                style={{
                  background: '#f8fafc',
                  border: '1px solid #e2e8f0',
                  borderRadius: '8px',
                  padding: '12px 16px',
                  marginBottom: '24px',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                }}
              >
                <Building2 size={16} color="#2563eb" />
                <span style={{ fontSize: '13.5px', color: '#1e293b' }}>
                  Target Shift Record:{' '}
                  <strong>
                    {source.code} &ndash; {source.name}
                  </strong>
                </span>
              </div>
            )}

            {/* Section 1: Shift Identification */}
            {(mode === 'create' || mode === 'rename') && (
              <div className="shift-form-section">
                <div className="shift-form-section-header">
                  <h3 className="shift-form-section-title">
                    <Building2 size={17} color="#2563eb" />
                    Shift Identification
                  </h3>
                  <p className="shift-form-section-subtitle">
                    Enter the unique corporate code and descriptive shift title for attendance
                    assignment.
                  </p>
                </div>

                <div className="shift-form-row-2">
                  {mode === 'create' && (
                    <TextInput
                      label="Shift Code"
                      required
                      placeholder="e.g. SH-SITE-01"
                      maxLength={30}
                      pattern="[A-Za-z0-9][A-Za-z0-9_.-]{0,29}"
                      value={code}
                      onChange={(e) => setCode(e.target.value.toUpperCase())}
                    />
                  )}
                  <div style={{ gridColumn: mode === 'create' ? undefined : '1 / -1' }}>
                    <TextInput
                      label="Shift Name"
                      required
                      placeholder="e.g. General Site Operations (Day Shift)"
                      maxLength={150}
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                    />
                  </div>
                </div>
              </div>
            )}

            {/* Section 2: Timings & Operating Hours */}
            {(mode === 'create' || mode === 'schedule') && (
              <div className="shift-form-section">
                <div className="shift-form-section-header">
                  <h3 className="shift-form-section-title">
                    <Clock size={17} color="#f59e0b" />
                    Operating Timings &amp; Baseline Hours
                  </h3>
                  <p className="shift-form-section-subtitle">
                    Daily scheduled duty window, meal breaks, and effective start date in
                    Asia/Karachi timezone.
                  </p>
                </div>

                {/* Effective Date & End Day Offset */}
                <div className="shift-form-row-2">
                  <TextInput
                    label="Effective from (Asia/Karachi)"
                    type="date"
                    min={minimum}
                    required
                    value={schedule.effectiveFrom}
                    onChange={(e) =>
                      setSchedule((prev) => ({ ...prev, effectiveFrom: e.target.value }))
                    }
                  />

                  <SelectInput
                    label="End Day (Duty Transition)"
                    value={String(schedule.endDayOffset)}
                    options={[
                      { value: '0', label: 'Same business day' },
                      { value: '1', label: 'Next calendar day (Overnight)' },
                    ]}
                    onChange={(e) =>
                      setSchedule((prev) => ({
                        ...prev,
                        endDayOffset: Number(e.target.value),
                      }))
                    }
                  />
                </div>

                {/* Start Time & End Time */}
                <div className="shift-form-row-2">
                  <TextInput
                    label="Start Time"
                    type="time"
                    step={1}
                    required
                    value={schedule.startTime}
                    onChange={(e) =>
                      setSchedule((prev) => ({ ...prev, startTime: e.target.value }))
                    }
                  />

                  <TextInput
                    label="End Time"
                    type="time"
                    step={1}
                    required
                    value={schedule.endTime}
                    onChange={(e) => setSchedule((prev) => ({ ...prev, endTime: e.target.value }))}
                  />
                </div>

                {/* Minutes Specs */}
                <div className="shift-form-row-3">
                  <TextInput
                    label="Scheduled Minutes"
                    type="number"
                    min={1}
                    max={1440}
                    step={1}
                    required
                    value={schedule.scheduledMinutes || ''}
                    placeholder="e.g. 540"
                    onChange={(e) =>
                      setSchedule((prev) => ({
                        ...prev,
                        scheduledMinutes: e.target.value === '' ? 0 : Number(e.target.value),
                      }))
                    }
                  />

                  <TextInput
                    label="Paid Break Minutes"
                    type="number"
                    min={0}
                    max={1440}
                    step={1}
                    required
                    value={schedule.paidBreakMinutes ?? 0}
                    placeholder="0"
                    onChange={(e) =>
                      setSchedule((prev) => ({
                        ...prev,
                        paidBreakMinutes: e.target.value === '' ? 0 : Number(e.target.value),
                      }))
                    }
                  />

                  <TextInput
                    label="Unpaid Break Minutes"
                    type="number"
                    min={0}
                    max={1440}
                    step={1}
                    required
                    value={schedule.unpaidBreakMinutes ?? 0}
                    placeholder="60"
                    onChange={(e) =>
                      setSchedule((prev) => ({
                        ...prev,
                        unpaidBreakMinutes: e.target.value === '' ? 0 : Number(e.target.value),
                      }))
                    }
                  />
                </div>

                {/* Optional Missing Checkout Reminder Delay */}
                <TextInput
                  label="Missing Checkout Reminder Delay (minutes, optional)"
                  type="number"
                  min={1}
                  max={2147483647}
                  step={1}
                  placeholder="e.g. 30 (Leaves blank for standard notifications)"
                  value={schedule.missingCheckoutReminderAfterMinutes ?? ''}
                  onChange={(e) => {
                    const val = e.target.value;
                    setSchedule((prev) => {
                      const next = { ...prev };
                      if (val === '') {
                        delete next.missingCheckoutReminderAfterMinutes;
                      } else {
                        next.missingCheckoutReminderAfterMinutes = Number(val);
                      }
                      return next;
                    });
                  }}
                />
              </div>
            )}

            {/* Section 3: Scheduled Working Days */}
            {(mode === 'create' || mode === 'schedule') && (
              <div className="shift-form-section">
                <div className="shift-form-section-header">
                  <h3 className="shift-form-section-title">
                    <Calendar size={17} color="#10b981" />
                    Applicable Working Days
                  </h3>
                  <p className="shift-form-section-subtitle">
                    Select the weekdays on which this schedule applies.
                  </p>
                </div>

                <div
                  className="shift-weekdays-toggle-group"
                  role="group"
                  aria-label="Applicable Weekdays"
                >
                  {weekdays.map((day) => {
                    const isSelected = (schedule.applicableWeekdays & day.value) !== 0;
                    return (
                      <button
                        type="button"
                        key={day.value}
                        className={`shift-weekday-pill-btn ${isSelected ? 'is-active' : ''}`}
                        onClick={() => toggleWeekday(day.value)}
                        aria-pressed={isSelected}
                      >
                        {isSelected && <Check size={14} />}
                        {day.label}
                      </button>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Section 4: Deactivation Notice (if mode === 'status') */}
            {mode === 'status' && (
              <div className="shift-form-section">
                <div
                  className="shift-side-policy-box"
                  style={{ background: '#fef3c7', borderColor: '#fde68a', color: '#92400e' }}
                >
                  <Info size={18} style={{ color: '#d97706', flexShrink: 0 }} />
                  <div>
                    <strong>Lifecycle Status Warning:</strong>
                    <p style={{ margin: '4px 0 0' }}>
                      Current or future roster assignments may prevent deactivating this shift.
                      Historical schedule versions will be retained for audit consistency.
                    </p>
                  </div>
                </div>
              </div>
            )}

            {/* Section 5: Audit & Compliance Reason */}
            <div className="shift-form-section" style={{ borderBottom: 'none', paddingBottom: 0 }}>
              <div className="shift-form-section-header">
                <h3 className="shift-form-section-title">
                  <Timer size={17} color="#7c3aed" />
                  Audit Reason &amp; Authorization
                </h3>
                <p className="shift-form-section-subtitle">
                  Required corporate audit log entry explaining why this work shift schedule is
                  being established or modified.
                </p>
              </div>

              <TextInput
                label="Reason"
                required
                maxLength={500}
                placeholder="e.g. Configured baseline working schedule for technical project operations team."
                value={inputReason}
                onChange={(e) => setReason(e.target.value)}
              />
            </div>

            {/* Form Footer Action Buttons */}
            <div
              style={{
                display: 'flex',
                justifyContent: 'flex-end',
                alignItems: 'center',
                gap: '12px',
                marginTop: '32px',
                paddingTop: '20px',
                borderTop: '1px solid #e2e8f0',
              }}
            >
              <Button
                type="button"
                variant="secondary"
                onClick={handleCancelClick}
                disabled={action.pending}
              >
                Cancel
              </Button>
              <Button type="submit" variant="primary" disabled={action.pending}>
                {action.pending ? (
                  <>
                    <RotateCcw size={15} className="animate-spin" />
                    Saving Shift...
                  </>
                ) : (
                  <>
                    <Save size={15} />
                    Save Shift
                  </>
                )}
              </Button>
            </div>
          </div>

          {/* RIGHT: Side Summary & Live Preview Card */}
          <aside className="shift-form-side-card">
            <h2 className="shift-side-card-title">Shift Overview &amp; Preview</h2>
            <p className="shift-side-card-subtitle">
              Live preview of this shift specification as it will appear across workforce rosters.
            </p>

            {/* Live Visual Card */}
            <div className="shift-side-preview-box">
              <div className="shift-preview-header">
                <span className="shift-preview-code">
                  {code.trim() || source?.code || 'SH-CODE'}
                </span>
                <span className="shift-preview-status">
                  {mode === 'status'
                    ? source?.isActive
                      ? 'To Deactivate'
                      : 'To Activate'
                    : 'Active'}
                </span>
              </div>

              <h4 className="shift-preview-title">
                {name.trim() || source?.name || 'Untitled Shift'}
              </h4>

              <div className="shift-preview-timings">
                <div className="shift-preview-time-point">
                  <span className="shift-preview-time-label">Start Time</span>
                  <span className="shift-preview-time-val">{previewMetrics.startFormatted}</span>
                </div>
                <div style={{ textAlign: 'center' }}>
                  <span
                    style={{
                      fontSize: '9.5px',
                      fontWeight: 800,
                      background: '#eff6ff',
                      color: '#2563eb',
                      padding: '1px 6px',
                      borderRadius: '9999px',
                      border: '1px solid #bfdbfe',
                    }}
                  >
                    {previewMetrics.durationHours}h
                  </span>
                </div>
                <div className="shift-preview-time-point" style={{ textAlign: 'right' }}>
                  <span className="shift-preview-time-label">End Time</span>
                  <span className="shift-preview-time-val">{previewMetrics.endFormatted}</span>
                </div>
              </div>

              {/* Working Days Mini Badges */}
              <div
                style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}
              >
                <span
                  style={{
                    fontSize: '10px',
                    fontWeight: 700,
                    color: '#64748b',
                    textTransform: 'uppercase',
                  }}
                >
                  Days Active:
                </span>
                <div style={{ display: 'flex', gap: '3px' }}>
                  {['M', 'T', 'W', 'T', 'F', 'S', 'S'].map((l, i) => {
                    const mask = 1 << i;
                    const on = (schedule.applicableWeekdays & mask) !== 0;
                    return (
                      <span
                        key={i}
                        style={{
                          width: '15px',
                          height: '15px',
                          borderRadius: '3px',
                          display: 'inline-flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          fontSize: '8px',
                          fontWeight: 800,
                          background: on ? '#242b5f' : '#e2e8f0',
                          color: on ? '#ffffff' : '#94a3b8',
                        }}
                      >
                        {l}
                      </span>
                    );
                  })}
                </div>
              </div>
            </div>

            {/* Specifications Key-Value Table */}
            <table className="shift-preview-specs-table">
              <tbody>
                <tr>
                  <td>Effective From</td>
                  <td>{schedule.effectiveFrom || today}</td>
                </tr>
                <tr>
                  <td>Duty Window</td>
                  <td>{schedule.endDayOffset === 1 ? 'Overnight (Next Day)' : 'Same Day'}</td>
                </tr>
                <tr>
                  <td>Total Scheduled</td>
                  <td>
                    {schedule.scheduledMinutes || 0} mins ({previewMetrics.durationHours}h)
                  </td>
                </tr>
                <tr>
                  <td>Meal / Breaks</td>
                  <td>{previewMetrics.totalBreaks} mins total</td>
                </tr>
                <tr>
                  <td>Weekly Schedule</td>
                  <td>{previewMetrics.activeDaysCount} Days / Week</td>
                </tr>
                <tr>
                  <td>Checkout Reminder</td>
                  <td>
                    {schedule.missingCheckoutReminderAfterMinutes
                      ? `${schedule.missingCheckoutReminderAfterMinutes}m delay`
                      : 'Not specified'}
                  </td>
                </tr>
              </tbody>
            </table>

            {/* Compliance Guarantee Banner */}
            <div className="shift-side-policy-box">
              <Info size={16} className="shift-side-policy-icon" />
              <span>
                <strong>Schedule history:</strong> Saved schedules retain their effective dates and
                version history. Review the timings and audit reason before saving.
              </span>
            </div>
          </aside>
        </div>
      </form>
    </div>
  );
}
