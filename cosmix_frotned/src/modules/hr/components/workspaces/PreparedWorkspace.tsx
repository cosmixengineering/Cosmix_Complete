import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { ClipboardList, Plus, SlidersHorizontal } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import type { PreparedWorkspaceDefinition } from '../../types/prepared-workspace';
import { PreparedForm } from './PreparedForm';
import { PreparedField } from './PreparedField';
import { ScheduleView } from './ScheduleView';
import { ReportActions } from './ReportActions';
import type { PreparedRow } from '../../types/prepared-workspace';

export function PreparedWorkspace({
  definition,
  fixedEmployee,
}: {
  definition: PreparedWorkspaceDefinition;
  fixedEmployee?: { id: string; name: string };
}) {
  const [params, setParams] = useSearchParams();
  const requested = params.get('tab');
  const tab = definition.tabs.find((t) => t.id === requested) ?? definition.tabs[0];
  const view = params.get('view') === 'draft' ? 'draft' : 'records';
  const [filters, setFilters] = useState<PreparedRow>({});
  if (!tab) return <p role="alert">Workspace configuration is unavailable.</p>;
  function open(nextView: string) {
    const next = new URLSearchParams(params);
    next.set('tab', tab!.id);
    next.set('view', nextView);
    setParams(next);
  }
  return (
    <>
      <div className="hr-page-heading">
        <div>
          <p className="hr-kicker">{fixedEmployee ? 'MY WORKSPACE' : 'HUMAN RESOURCES'}</p>
          <h1>{definition.title}</h1>
        </div>
        <span className="hr-status hr-status--neutral">Service not connected</span>
      </div>
      <nav className="hr-tabs" aria-label={definition.title + ' views'}>
        {definition.tabs.map((t) => (
          <Link key={t.id} className={tab.id === t.id ? 'active' : ''} to={`?tab=${t.id}`}>
            {t.label}
          </Link>
        ))}
      </nav>
      <div className="hr-repeat-heading">
        <h2>{tab.label}</h2>
        {!tab.readOnly && tab.sections.length > 0 && (
          <Button
            variant={view === 'draft' ? 'secondary' : 'primary'}
            onClick={() => open(view === 'draft' ? 'records' : 'draft')}
          >
            {view === 'draft' ? <ClipboardList size={16} /> : <Plus size={16} />}
            {view === 'draft' ? 'Records' : 'New draft'}
          </Button>
        )}
      </div>
      <p className="hr-notice" role="status">
        The service is not connected. Records and calculated results are unavailable; drafts are not
        saved or submitted.
      </p>
      {definition.id === 'reports' && tab.id !== 'export-jobs' && view === 'records' && (
        <ReportActions key={tab.id} />
      )}
      {view === 'draft' && !tab.readOnly && tab.sections.length > 0 ? (
        <PreparedForm key={`${definition.id}:${tab.id}`} tab={tab} fixedEmployee={fixedEmployee} />
      ) : (
        <>
          {!!tab.filters?.length && (
            <details className="hr-form-section">
              <summary>
                <SlidersHorizontal size={16} />
                Filters
              </summary>
              <div className="hr-form-grid">
                {tab.filters.map((field) => (
                  <PreparedField
                    key={field.id}
                    field={field}
                    value={filters[field.id] ?? null}
                    fixedEmployee={fixedEmployee}
                    onChange={(value) => setFilters({ ...filters, [field.id]: value })}
                  />
                ))}
              </div>
              <Button variant="secondary" disabled>
                Apply filters
              </Button>
            </details>
          )}
          {tab.presentation ? (
            <ScheduleView
              key={`${definition.id}:${tab.id}`}
              mode={tab.presentation}
              title={tab.label}
            />
          ) : (
            <div className="hr-table-scroll">
              <table className="hr-prepared-table">
                <caption className="sr-only">{tab.label}</caption>
                <thead>
                  <tr>
                    {tab.columns.map((column) => (
                      <th scope="col" key={column}>
                        {column}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td colSpan={Math.max(tab.columns.length, 1)}>Records unavailable</td>
                  </tr>
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
      {!!tab.serverFields?.length && (
        <section className="hr-form-section">
          <h2>Verified results</h2>
          <dl className="hr-dl">
            {tab.serverFields.map((field) => (
              <div key={field}>
                <dt>{field}</dt>
                <dd className="muted">Unavailable</dd>
              </div>
            ))}
          </dl>
        </section>
      )}
    </>
  );
}
