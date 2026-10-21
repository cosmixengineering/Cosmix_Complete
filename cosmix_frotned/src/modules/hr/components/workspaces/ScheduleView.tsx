import { useState } from 'react';
import { CalendarDays, ChevronLeft, ChevronRight, List, RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { Dialog } from '@/components/ui/Dialog';
import {
  businessToday,
  dateLabel,
  monthDays,
  monthGrid,
  shiftMonth,
  weekDays,
} from '../../utils/calendar';

const weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

export function ScheduleView({
  mode,
  title,
}: {
  mode: 'calendar' | 'roster' | 'register';
  title: string;
}) {
  const [today] = useState(businessToday);
  const [month, setMonth] = useState(() => today.slice(0, 7));
  const [anchor, setAnchor] = useState(today);
  const [selected, setSelected] = useState<string | null>(null);
  const [view, setView] = useState<'schedule' | 'list'>('schedule');
  const dates =
    mode === 'roster'
      ? weekDays(anchor)
      : mode === 'register'
        ? monthDays(month)
        : monthGrid(month);
  function navigate(direction: number) {
    if (mode === 'roster') {
      const date = new Date(anchor + 'T12:00:00Z');
      date.setUTCDate(date.getUTCDate() + 7 * direction);
      setAnchor(date.toISOString().slice(0, 10));
    } else setMonth(shiftMonth(month, direction));
  }
  const start = mode === 'roster' ? dates[0]! : month + '-01';
  return (
    <section className="hr-schedule" aria-label={title + ' schedule'}>
      <div className="hr-schedule-toolbar">
        <div className="hr-inline" role="group" aria-label="Date navigation">
          <Button
            variant="secondary"
            aria-label="Previous period"
            title="Previous period"
            disabled={start <= '0002-01-08'}
            onClick={() => navigate(-1)}
          >
            <ChevronLeft size={17} />
          </Button>
          <Button
            variant="secondary"
            aria-label="Next period"
            title="Next period"
            disabled={start >= '9998-12-01'}
            onClick={() => navigate(1)}
          >
            <ChevronRight size={17} />
          </Button>
          <Button
            variant="ghost"
            title="Current period"
            aria-label="Current period"
            onClick={() => {
              setMonth(today.slice(0, 7));
              setAnchor(today);
            }}
          >
            <RotateCcw size={17} />
          </Button>
        </div>
        <TextInput
          label={mode === 'roster' ? 'Week containing' : 'Calendar month'}
          type={mode === 'roster' ? 'date' : 'month'}
          value={mode === 'roster' ? anchor : month}
          min={mode === 'roster' ? '0002-01-08' : '0002-01'}
          max={mode === 'roster' ? '9998-12-01' : '9998-12'}
          onChange={(event) => {
            const value = event.target.value;
            if (!value || !event.target.validity.valid) return;
            if (mode === 'roster') setAnchor(value);
            else setMonth(value);
          }}
        />
        <div className="hr-inline" role="group" aria-label="Schedule layout">
          <Button
            variant={view === 'schedule' ? 'secondary' : 'ghost'}
            aria-pressed={view === 'schedule'}
            onClick={() => setView('schedule')}
          >
            <CalendarDays size={16} />
            {mode === 'register' ? 'Register' : mode === 'roster' ? 'Week' : 'Month'}
          </Button>
          <Button
            variant={view === 'list' ? 'secondary' : 'ghost'}
            aria-pressed={view === 'list'}
            onClick={() => setView('list')}
          >
            <List size={16} />
            List
          </Button>
        </div>
      </div>
      <p className="hr-schedule-period" aria-live="polite">
        {mode === 'roster'
          ? `${dateLabel(dates[0]!)} to ${dateLabel(dates[6]!)}`
          : dateLabel(month + '-01', { month: 'long', year: 'numeric' })}
        <span className="muted">Pakistan business dates</span>
      </p>
      <p className="muted">Authorized records unavailable</p>
      {view === 'list' ? (
        <div className="hr-table-scroll">
          <table className="hr-prepared-table">
            <caption className="sr-only">{title} date list</caption>
            <thead>
              <tr>
                <th>Date</th>
                <th>Authorized records</th>
              </tr>
            </thead>
            <tbody>
              {dates
                .filter((date) => mode === 'roster' || date.startsWith(month))
                .map((date) => (
                  <tr key={date}>
                    <td>
                      <button
                        type="button"
                        className="hr-text-button"
                        onClick={() => setSelected(date)}
                      >
                        {dateLabel(date)}
                      </button>
                    </td>
                    <td>Unavailable</td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      ) : mode === 'register' ? (
        <div className="hr-table-scroll">
          <table className="hr-prepared-table hr-month-register">
            <caption className="sr-only">{title} employee by day register</caption>
            <thead>
              <tr>
                <th>Employee</th>
                {dates.map((date) => (
                  <th key={date}>
                    <button
                      type="button"
                      title={dateLabel(date)}
                      aria-label={'Review ' + dateLabel(date)}
                      onClick={() => setSelected(date)}
                    >
                      {Number(date.slice(-2))}
                      <small>{dateLabel(date, { weekday: 'short' })}</small>
                    </button>
                  </th>
                ))}
                <th>Verified totals</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td colSpan={dates.length + 2}>
                  Employee rows and attendance results are unavailable.
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      ) : (
        <div className="hr-calendar-scroll">
          <div className={`hr-calendar-grid ${mode === 'roster' ? 'hr-calendar-grid--week' : ''}`}>
            {weekdays.map((name) => (
              <div key={name} className="hr-calendar-weekday">
                {name}
              </div>
            ))}
            {dates.map((date) => (
              <button
                type="button"
                key={date}
                className={`hr-calendar-day ${date === today ? 'is-today' : ''} ${mode !== 'roster' && !date.startsWith(month) ? 'is-outside' : ''}`}
                aria-label={`${dateLabel(date)}; records unavailable`}
                aria-current={date === today ? 'date' : undefined}
                onClick={() => setSelected(date)}
              >
                <span>
                  <time dateTime={date}>{Number(date.slice(-2))}</time>
                  {date === today && <span className="hr-calendar-today">Today</span>}
                </span>
                <small>{mode === 'roster' ? 'Shift unavailable' : 'Unavailable'}</small>
                <span className="hr-calendar-unknown" aria-hidden="true">
                  -
                </span>
              </button>
            ))}
          </div>
        </div>
      )}
      <Dialog
        open={selected !== null}
        onOpenChange={(open) => {
          if (!open) setSelected(null);
        }}
        title={selected ? dateLabel(selected) : 'Day details'}
        description={title}
      >
        <p role="status">
          Authorized records, policy results and history are unavailable until the service is
          connected. No attendance or leave status has been inferred.
        </p>
        <Button variant="secondary" onClick={() => setSelected(null)}>
          Close
        </Button>
      </Dialog>
    </section>
  );
}
