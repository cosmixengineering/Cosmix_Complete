import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Compass,
  History,
  Layers,
  MapPin,
  Plus,
  RefreshCw,
  ShieldCheck,
  Target,
} from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { queryString } from '../api/attendance-api';
import { date, id } from '../schemas/common';
import { boundaryPage, geofenceConfiguration } from '../schemas/site-geofences';
import {
  Command,
  Denied,
  PrivateWorkspace,
  QueryState,
  SavedNotice,
} from '../components/SchedulingUi';
import { SiteNavigation } from '../components/References';
import { CirclePreview } from '../components/geofences/CirclePreview';
import { SiteBoundaryForm } from '../components/geofences/SiteBoundaryForm';
import { SiteBoundaryHistory } from '../components/geofences/SiteBoundaryHistory';
import { SiteEditor } from '@/modules/hr/components/sites/SiteEditor';

function GeofenceEmptyHero({ onAddSite }: { onAddSite?: () => void }) {
  return (
    <div className="geofence-empty-hero">
      <div className="radar-scanner-graphic">
        <div className="radar-ring radar-ring-1" />
        <div className="radar-ring radar-ring-2" />
        <div className="radar-center-pin">
          <MapPin size={22} />
        </div>
      </div>
      <h2>No Physical Worksite Selected</h2>
      <p>
        Choose an authorized physical worksite from the directory on the left or enter a known Site
        ID to inspect active coordinates, radial tolerances, and versioned publication history.
      </p>
      {onAddSite && (
        <div style={{ margin: '16px 0 24px', display: 'flex', justifyContent: 'center' }}>
          <button
            type="button"
            className="btn btn--primary"
            onClick={onAddSite}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '8px',
              padding: '11px 22px',
              borderRadius: '8px',
              fontSize: '14px',
              fontWeight: 650,
              background: '#2563eb',
              color: '#ffffff',
              border: 'none',
              cursor: 'pointer',
              boxShadow: '0 4px 12px rgba(37, 99, 235, 0.25)',
            }}
          >
            <Plus size={16} />
            Add New Worksite & Assign Supervisor
          </button>
        </div>
      )}
      <div className="empty-features-grid">
        <div className="feature-item">
          <div className="feature-item-header">
            <Target size={16} />
            <span>Radial Perimeter</span>
          </div>
          <p>
            Enforces strict circular boundary coordinates around the site centroid for mobile
            clock-ins.
          </p>
        </div>
        <div className="feature-item">
          <div className="feature-item-header">
            <ShieldCheck size={16} />
            <span>Anti-Spoofing</span>
          </div>
          <p>
            Rejects mobile attendance submissions where device GPS signal accuracy exceeds
            tolerance.
          </p>
        </div>
        <div className="feature-item">
          <div className="feature-item-header">
            <Compass size={16} />
            <span>Version Audit</span>
          </div>
          <p>
            Maintains an immutable historical ledger of all published boundary revisions with
            effective dates.
          </p>
        </div>
      </div>
    </div>
  );
}

function GeofencesWorkspace({ siteId }: { siteId: string }) {
  const access = useAttendanceAccess();
  const canRead = access.can('hr.sites.read');
  const canManage = access.can('hr.sites.manage');
  const [asOf, setAsOf] = useState('');
  const [requestedDate, setRequestedDate] = useState('');
  const [editing, setEditing] = useState(false);
  const [administration, setAdministration] = useState(false);
  const [notice, setNotice] = useState('');
  const path = `/hr/sites/${siteId}/geofences`;
  const query = useAttendanceQuery(
    path + queryString({ asOfDate: requestedDate }),
    geofenceConfiguration,
    canRead,
  );
  const latest = useAttendanceQuery(path + '/versions?page=1&pageSize=1', boundaryPage, canRead);

  function reload() {
    void query.refetch();
    void latest.refetch();
  }

  function close() {
    setEditing(false);
    reload();
  }

  if (!canRead)
    return (
      <div className="attendance-card">
        <Denied>
          Boundary configuration and version reads require All-scoped site read access.
        </Denied>
      </div>
    );

  return (
    <>
      <div className="attendance-card">
        <div className="attendance-card-header">
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
              <span className="badge badge--info">Active Worksite</span>
              <span style={{ fontSize: '12px', color: '#64748b', fontFamily: 'monospace' }}>
                ID: {siteId}
              </span>
            </div>
            <h2 style={{ fontSize: '18px', fontWeight: 700, margin: 0, color: '#0f172a' }}>
              Worksite Geofence Configuration
            </h2>
          </div>
          <div className="attendance-commands">
            <Command
              icon={RefreshCw}
              variant="secondary"
              disabled={editing || query.isFetching || latest.isFetching}
              onClick={reload}
            >
              Reload
            </Command>
            {canManage && (
              <Command
                icon={Plus}
                variant="primary"
                disabled={
                  editing ||
                  !query.data ||
                  query.isError ||
                  !latest.data ||
                  latest.isError ||
                  query.isFetching ||
                  latest.isFetching
                }
                onClick={() => setEditing(true)}
              >
                Publish boundary
              </Command>
            )}
          </div>
        </div>

        <SavedNotice message={notice} />

        <div
          style={{
            display: 'flex',
            alignItems: 'flex-end',
            gap: '12px',
            flexWrap: 'wrap',
            padding: '14px 0 6px',
          }}
        >
          <div style={{ width: '260px' }}>
            <TextInput
              label="As-of business date (blank = today)"
              type="date"
              value={asOf}
              disabled={editing}
              onChange={(event) => setAsOf(event.target.value)}
            />
          </div>
          <Command
            icon={History}
            variant="secondary"
            disabled={editing || query.isFetching || (asOf !== '' && !date.safeParse(asOf).success)}
            onClick={() => {
              if (asOf === requestedDate) void query.refetch();
              else setRequestedDate(asOf);
            }}
          >
            Read as of date
          </Command>
        </div>

        <QueryState query={query} />

        {query.data && !query.isError && (
          <div
            style={{
              marginTop: '20px',
              borderTop: '1px solid var(--color-border-subtle)',
              paddingTop: '18px',
            }}
          >
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                marginBottom: '14px',
              }}
            >
              <h3 style={{ margin: 0, fontSize: '15px', fontWeight: 650, color: '#0f172a' }}>
                Boundary Configuration ({query.data.asOfDate})
              </h3>
              <span className="badge badge--neutral">
                Latest Version: v{query.data.latestVersionNumber}
              </span>
            </div>

            {query.data.status === 'not_configured' ? (
              <div className="empty-state" style={{ padding: '32px 16px' }}>
                <p>No circular boundary has been configured or published for this date.</p>
              </div>
            ) : query.data.boundary ? (
              <>
                <p style={{ fontSize: '13px', color: '#64748b', margin: '0 0 10px' }}>
                  Version {query.data.boundary.versionNumber} • Effective since{' '}
                  <strong style={{ color: '#0f172a' }}>{query.data.boundary.effectiveFrom}</strong>
                </p>
                <CirclePreview {...query.data.boundary} />
              </>
            ) : (
              <p role="alert" className="attendance-error">
                The configured boundary data is missing or corrupted. Reload before publication.
              </p>
            )}

            {editing && (
              <div style={{ marginTop: '20px' }}>
                <SiteBoundaryForm
                  configuration={query.data}
                  latestDate={latest.data?.items[0]?.effectiveFrom}
                  onSaved={() => {
                    setNotice('Boundary version successfully published and saved.');
                    close();
                  }}
                  onCancel={close}
                />
              </div>
            )}
          </div>
        )}

        {latest.isError && <QueryState query={latest} />}
      </div>

      <div className="attendance-card">
        <div className="attendance-card-header">
          <div className="attendance-card-title">
            <Layers size={18} style={{ color: '#2563eb' }} />
            <span>Boundary Revision & Audit Ledger</span>
          </div>
          <div className="attendance-commands" role="group" aria-label="Boundary records">
            <Command
              icon={Layers}
              variant={administration ? 'secondary' : 'primary'}
              aria-pressed={!administration}
              disabled={editing}
              onClick={() => setAdministration(false)}
            >
              Published Versions
            </Command>
            {canManage && (
              <Command
                icon={History}
                variant={administration ? 'primary' : 'secondary'}
                aria-pressed={administration}
                disabled={editing}
                onClick={() => setAdministration(true)}
              >
                Administration History
              </Command>
            )}
          </div>
        </div>

        <SiteBoundaryHistory
          key={String(administration)}
          siteId={siteId}
          administration={administration}
          disabled={editing}
        />
      </div>
    </>
  );
}

export function SiteGeofencesPage() {
  const { siteId } = useParams<{ siteId: string }>();
  const navigate = useNavigate();
  const access = useAttendanceAccess();
  const [addingSite, setAddingSite] = useState(false);
  const canManage = access.can('hr.sites.manage');

  return (
    <PrivateWorkspace resource={siteId}>
      {/* Clean Enterprise Page Header with Add New Site Action */}
      <div
        className="hr-page-heading"
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '16px',
          marginBottom: '24px',
          paddingBottom: '16px',
          borderBottom: '1px solid var(--color-border)',
        }}
      >
        <div>
          <h1 style={{ fontSize: '22px', fontWeight: 700, margin: 0, color: '#0f172a' }}>
            Site Boundaries & Geofencing
          </h1>
          <p style={{ margin: '4px 0 0', color: '#64748b', fontSize: '13.5px' }}>
            Define GPS circular geofence perimeters and radius tolerances for mobile employee
            check-ins.
          </p>
        </div>

        {canManage && (
          <button
            type="button"
            className="btn btn--primary"
            onClick={() => setAddingSite(true)}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '8px',
              padding: '9px 18px',
              borderRadius: '6px',
              fontSize: '13.5px',
              fontWeight: 650,
              background: '#2563eb',
              color: '#ffffff',
              border: 'none',
              cursor: 'pointer',
              boxShadow: '0 2px 6px rgba(37, 99, 235, 0.25)',
              transition: 'background 0.15s',
            }}
          >
            <Plus size={16} />
            Add New Site
          </button>
        )}
      </div>

      {/* Modern Two-Column Layout */}
      <div className="geofences-workspace-grid">
        <aside className="geofences-sidebar-pane">
          <SiteNavigation siteId={siteId} mode="geofences" />
        </aside>
        <main className="geofences-content-pane">
          {siteId ? (
            id.safeParse(siteId).success ? (
              <GeofencesWorkspace key={siteId} siteId={siteId} />
            ) : (
              <div className="attendance-card">
                <Denied>The route does not contain a valid site ID.</Denied>
              </div>
            )
          ) : (
            <GeofenceEmptyHero onAddSite={canManage ? () => setAddingSite(true) : undefined} />
          )}
        </main>
      </div>

      {addingSite && (
        <SiteEditor
          onClose={() => setAddingSite(false)}
          onSaved={(newSite) => {
            setAddingSite(false);
            void navigate(`/hr/attendance/sites/${newSite.id}/geofences`);
          }}
        />
      )}
    </PrivateWorkspace>
  );
}
