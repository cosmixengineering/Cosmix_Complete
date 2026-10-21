import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, ExternalLink, MapPin, RefreshCw, Search, ShieldCheck } from 'lucide-react';
import { z } from 'zod';
import type * as Leaflet from 'leaflet';
import { Button } from '@/components/ui/Button';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { useAttendanceQuery } from '@/modules/attendance/hooks/use-attendance-query';
import { siteSchema } from '../schemas/sites';
import { geofenceConfiguration } from '@/modules/attendance/schemas/site-geofences';
import { Denied } from '../components/Feedback';
import 'leaflet/dist/leaflet.css';
import '@/styles/site-detail.css';

const workerIdentity = {
  employeeId: z.uuid(),
  employeeNumber: z.number().int().nonnegative(),
  fullLegalName: z.string(),
  designationName: z.string(),
};
const checkInLocation = z.object({
  ...workerIdentity,
  checkInTimeUtc: z.iso.datetime({ offset: true }),
  latitude: z.number().min(-90).max(90).nullable(),
  longitude: z.number().min(-180).max(180).nullable(),
  accuracyMeters: z.number().nonnegative().nullable(),
});
const operationalDetails = z.object({
  siteId: z.uuid(),
  supervisor: z
    .object({
      supervisorId: z.uuid(),
      fullLegalName: z.string(),
      designationName: z.string().nullable(),
      personalPhone: z.string().nullable(),
      officialEmail: z.string().nullable(),
    })
    .nullable(),
  assignedWorkers: z.array(
    z.object({
      ...workerIdentity,
      departmentName: z.string(),
      personalPhone: z.string().nullable(),
      status: z.string(),
      effectiveFrom: z.iso.date(),
    }),
  ),
  activeWorkersOnDuty: z.array(checkInLocation),
});
type CheckInLocation = z.infer<typeof checkInLocation>;
type Boundary = NonNullable<z.infer<typeof geofenceConfiguration>['boundary']>;

function timestamp(value: string | number) {
  return new Date(value)
    .toISOString()
    .replace('T', ' ')
    .replace(/\.\d{3}Z$/, ' UTC');
}

function RecordedSiteMap({
  boundary,
  locations,
}: {
  boundary: Boundary;
  locations: CheckInLocation[];
}) {
  const container = useRef<HTMLDivElement>(null);
  const [mapError, setMapError] = useState(false);

  useEffect(() => {
    let disposed = false;
    let map: Leaflet.Map | undefined;
    let observer: ResizeObserver | undefined;
    void import('leaflet')
      .then((L) => {
        if (disposed || !container.current) return;
        map = L.map(container.current, { scrollWheelZoom: false });
        const tiles = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
          attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
          maxZoom: 19,
        }).addTo(map);
        tiles.on('tileerror', () => {
          if (!disposed) setMapError(true);
        });
        const fence = L.circle([boundary.latitude, boundary.longitude], {
          radius: boundary.radiusMeters,
          color: '#2563eb',
          fillOpacity: 0.08,
        }).addTo(map);
        const bounds = fence.getBounds();
        for (const location of locations) {
          if (location.latitude === null || location.longitude === null) continue;
          // These are check-in punch coordinates, not a current tracking feed.
          const label = document.createElement('span');
          label.textContent =
            location.fullLegalName + ' | Session start: ' + timestamp(location.checkInTimeUtc);
          L.circleMarker([location.latitude, location.longitude], {
            radius: 6,
            color: '#a16207',
            fillOpacity: 0.8,
          })
            .bindPopup(label)
            .addTo(map);
          bounds.extend([location.latitude, location.longitude]);
        }
        map.fitBounds(bounds, { padding: [24, 24], maxZoom: 17 });
        observer = new ResizeObserver(() => map?.invalidateSize());
        observer.observe(container.current);
      })
      .catch(() => {
        if (!disposed) setMapError(true);
      });
    return () => {
      disposed = true;
      observer?.disconnect();
      map?.remove();
    };
  }, [boundary, locations]);

  return (
    <div>
      <div
        ref={container}
        aria-label="Configured boundary and recorded check-in coordinates"
        style={{ height: 360, width: '100%', minWidth: 0, isolation: 'isolate' }}
      />
      {mapError && (
        <p role="status">Map imagery unavailable. Recorded coordinates remain listed below.</p>
      )}
    </div>
  );
}

export function SiteDetailPage() {
  const { siteId } = useParams<{ siteId: string }>();
  const access = useHrAccess();
  if (!access.ready || !access.can('hr.sites.read')) return <Denied />;
  const parsedId = z.uuid().safeParse(siteId);
  if (!parsedId.success) return <p role="alert">Invalid site identifier.</p>;
  return (
    <SiteEvidence
      key={JSON.stringify([parsedId.data, access.subject, access.user])}
      siteId={parsedId.data}
    />
  );
}

function SiteEvidence({ siteId }: { siteId: string }) {
  const access = useHrAccess();
  const allowed = access.can('hr.sites.read');
  const siteQuery = useHrQuery(
    'sites/' + siteId,
    siteSchema.refine((site) => site.id.toLowerCase() === siteId.toLowerCase()),
    ['hr.sites.read'],
    allowed,
  );
  const detailsQuery = useHrQuery(
    'sites/' + siteId + '/operational-details',
    operationalDetails.refine((details) => details.siteId.toLowerCase() === siteId.toLowerCase()),
    ['hr.sites.read'],
    allowed,
  );
  const geofenceQuery = useAttendanceQuery(
    '/hr/sites/' + siteId + '/geofences',
    geofenceConfiguration.refine(
      (configuration) =>
        configuration.siteId.toLowerCase() === siteId.toLowerCase() &&
        (configuration.status === 'configured'
          ? configuration.boundary !== null &&
            configuration.boundary.siteId.toLowerCase() === siteId.toLowerCase() &&
            configuration.boundary.effectiveFrom <= configuration.asOfDate
          : configuration.boundary === null),
    ),
    allowed,
  );
  const [search, setSearch] = useState('');
  const fetching = siteQuery.isFetching || detailsQuery.isFetching || geofenceQuery.isFetching;
  function refresh() {
    if (!access.can('hr.sites.read') || fetching) return;
    void siteQuery.refetch();
    void detailsQuery.refetch();
    void geofenceQuery.refetch();
  }
  const refreshButton = (
    <Button
      variant="secondary"
      onClick={refresh}
      disabled={fetching}
      title="Refresh all site records"
    >
      <RefreshCw size={15} />
      <span>{fetching ? 'Refreshing...' : 'Refresh'}</span>
    </Button>
  );
  if (!allowed) return <Denied />;
  if (siteQuery.isError)
    return (
      <section role="alert">
        <p>Site information unavailable. Previously loaded evidence is hidden.</p>
        {refreshButton}
      </section>
    );
  if (siteQuery.isPending || !siteQuery.data)
    return <p role="status">Loading site information...</p>;

  const site = siteQuery.data;
  const details = detailsQuery.isError || detailsQuery.isPending ? undefined : detailsQuery.data;
  const configuration =
    geofenceQuery.isError || geofenceQuery.isPending ? undefined : geofenceQuery.data;
  const boundary = configuration?.boundary;
  const supervisor = details?.supervisor;
  const matchingSupervisor =
    supervisor && supervisor.supervisorId.toLowerCase() === site.supervisorEmployeeId?.toLowerCase()
      ? supervisor
      : null;
  const records = details?.assignedWorkers ?? [];
  const sessions = details?.activeWorkersOnDuty ?? [];
  const term = search.trim().toLowerCase();
  const matches = (worker: {
    fullLegalName: string;
    employeeNumber: number;
    designationName: string;
  }) =>
    [worker.fullLegalName, String(worker.employeeNumber), worker.designationName].some((value) =>
      value.toLowerCase().includes(term),
    );
  const filteredRecords = records.filter(matches);
  const filteredSessions = sessions.filter(matches);

  return (
    <div className="site-detail-container" style={{ minWidth: 0, overflowWrap: 'anywhere' }}>
      <header className="site-detail-header">
        <div style={{ minWidth: 0 }}>
          <Link to="/hr/sites" className="site-back-btn">
            <ArrowLeft size={16} />
            Sites
          </Link>
          <div className="site-title-row">
            <h1 style={{ letterSpacing: 0 }}>{site.name}</h1>
            <span className="site-code-badge">{site.code}</span>
            <span className={'badge ' + (site.isActive ? 'badge--positive' : 'badge--neutral')}>
              {site.isActive ? 'Active site' : 'Inactive site'}
            </span>
          </div>
        </div>
        <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 12 }}>
          {refreshButton}
          <Link to={'/hr/attendance/sites/' + siteId + '/geofences'}>
            <ShieldCheck size={15} /> Geofence
          </Link>
        </div>
      </header>

      <section aria-label="Site information">
        <dl
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 240px), 1fr))',
            gap: 12,
            margin: 0,
          }}
        >
          {[
            ['Client', site.clientName],
            ['Address', site.address],
            ['City', site.cityName],
            ['Starts on', site.startsOn],
            ['Ends on', site.endsOn],
          ].map(([label, value]) => (
            <div key={label}>
              <dt>{label}</dt>
              <dd style={{ margin: 0 }}>{value || 'Not recorded'}</dd>
            </div>
          ))}
        </dl>
        {siteQuery.dataUpdatedAt > 0 && <p>Site retrieved: {timestamp(siteQuery.dataUpdatedAt)}</p>}
      </section>

      <section aria-labelledby="site-geofence-heading">
        <h2 id="site-geofence-heading" style={{ fontSize: 17 }}>
          Geofence
        </h2>
        {geofenceQuery.isError ? (
          <p role="alert">Geofence unavailable. No boundary is shown.</p>
        ) : !configuration ? (
          <p role="status">Loading geofence...</p>
        ) : !boundary ? (
          <p>Geofence not configured as of {configuration.asOfDate}.</p>
        ) : (
          <>
            <p>
              Version {boundary.versionNumber}; effective {boundary.effectiveFrom}; as of{' '}
              {configuration.asOfDate}.
              {!site.isActive && ' Inactive site: this boundary is not punch authorization.'}
            </p>
            <p>
              <MapPin size={14} /> Latitude {boundary.latitude}, longitude {boundary.longitude};
              radius {boundary.radiusMeters} m; maximum GPS accuracy{' '}
              {boundary.maximumAccuracyMeters} m.
            </p>
            <RecordedSiteMap
              key={JSON.stringify([boundary, detailsQuery.dataUpdatedAt, detailsQuery.isError])}
              boundary={boundary}
              locations={sessions}
            />
            <p>
              Boundary: blue. Check-in snapshots: amber. Current worker position and face
              verification: unavailable.
            </p>
          </>
        )}
      </section>

      <section aria-labelledby="site-workers-heading">
        <h2 id="site-workers-heading" style={{ fontSize: 17 }}>
          Site personnel
        </h2>
        {detailsQuery.isError ? (
          <p role="alert">Site personnel unavailable. Previously loaded records are hidden.</p>
        ) : !details ? (
          <p role="status">Loading site personnel...</p>
        ) : (
          <>
            <p>Personnel retrieved: {timestamp(detailsQuery.dataUpdatedAt)}</p>
            <h3 style={{ fontSize: 15 }}>Assigned supervisor</h3>
            {matchingSupervisor ? (
              <dl>
                <dt>Name</dt>
                <dd>{matchingSupervisor.fullLegalName}</dd>
                <dt>Designation</dt>
                <dd>{matchingSupervisor.designationName || 'Not recorded'}</dd>
                <dt>Phone</dt>
                <dd>{matchingSupervisor.personalPhone || 'Not recorded'}</dd>
                <dt>Email</dt>
                <dd>{matchingSupervisor.officialEmail || 'Not recorded'}</dd>
              </dl>
            ) : (
              <p>
                {site.supervisorEmployeeId
                  ? 'Supervisor details unavailable.'
                  : 'No supervisor assigned.'}
              </p>
            )}
            <label style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
              <Search size={16} />
              Search personnel
              <input
                type="search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                style={{ maxWidth: '100%', minWidth: 0 }}
              />
            </label>

            <h3 style={{ fontSize: 15 }}>Assignment records ({records.length})</h3>
            {records.length === 0 ? (
              <p>No assignment records returned.</p>
            ) : filteredRecords.length === 0 ? (
              <p>No matching assignment records.</p>
            ) : (
              <div style={{ maxWidth: '100%', overflowX: 'auto' }}>
                <table className="hr-table" style={{ minWidth: 680, width: '100%' }}>
                  <thead>
                    <tr>
                      <th>Employee</th>
                      <th>Designation</th>
                      <th>Department</th>
                      <th>Effective from</th>
                      <th>Employee status</th>
                      <th>Phone</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredRecords.map((worker, index) => (
                      <tr key={worker.employeeId + worker.effectiveFrom + index}>
                        <td>
                          {worker.fullLegalName}
                          <br />#{worker.employeeNumber}
                        </td>
                        <td>{worker.designationName}</td>
                        <td>{worker.departmentName}</td>
                        <td>{worker.effectiveFrom}</td>
                        <td>{worker.status}</td>
                        <td>{worker.personalPhone || 'Not recorded'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            <h3 style={{ fontSize: 15 }}>Open-session check-in records ({sessions.length})</h3>
            <p>
              Sessions started within the API's last 24-hour window. Coordinates are from check-in,
              not live tracking.
            </p>
            {sessions.length === 0 ? (
              <p>No open-session check-in records returned.</p>
            ) : filteredSessions.length === 0 ? (
              <p>No matching check-in records.</p>
            ) : (
              <div style={{ maxWidth: '100%', overflowX: 'auto' }}>
                <table className="hr-table" style={{ minWidth: 680, width: '100%' }}>
                  <thead>
                    <tr>
                      <th>Employee</th>
                      <th>Session start (UTC)</th>
                      <th>Recorded latitude, longitude</th>
                      <th>GPS accuracy</th>
                      <th>Map</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredSessions.map((worker, index) => (
                      <tr key={worker.employeeId + worker.checkInTimeUtc + index}>
                        <td>
                          {worker.fullLegalName}
                          <br />#{worker.employeeNumber}
                        </td>
                        <td>{timestamp(worker.checkInTimeUtc)}</td>
                        <td>
                          {worker.latitude === null || worker.longitude === null
                            ? 'Not recorded'
                            : worker.latitude + ', ' + worker.longitude}
                        </td>
                        <td>
                          {worker.accuracyMeters === null
                            ? 'Not recorded'
                            : worker.accuracyMeters + ' m'}
                        </td>
                        <td>
                          {worker.latitude !== null && worker.longitude !== null ? (
                            <a
                              href={
                                'https://www.google.com/maps/search/?api=1&query=' +
                                worker.latitude +
                                ',' +
                                worker.longitude
                              }
                              target="_blank"
                              rel="noreferrer"
                              aria-label={
                                'Open recorded check-in coordinates for ' + worker.fullLegalName
                              }
                              title="Open recorded check-in coordinates"
                            >
                              <ExternalLink size={16} />
                            </a>
                          ) : (
                            'Unavailable'
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </section>
    </div>
  );
}
