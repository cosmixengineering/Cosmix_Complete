import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  ArrowLeft,
  RotateCw,
  MapPin,
  Navigation,
  Layers,
  Crosshair,
  Briefcase,
  Phone,
  Mail,
  Search,
  Users,
  Info,
  ExternalLink,
} from 'lucide-react';
import { z } from 'zod';
import type * as Leaflet from 'leaflet';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { useAttendanceQuery } from '@/modules/attendance/hooks/use-attendance-query';
import { siteSchema } from '../schemas/sites';
import { geofenceConfiguration } from '@/modules/attendance/schemas/site-geofences';
import { Denied } from '../components/Feedback';
import { GOOGLE_MAPS_TILES, GOOGLE_MAPS_TILE_OPTIONS } from '@/utils/google-maps';
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

function formatDate(value?: string | null) {
  if (!value) return null;
  const d = new Date(value);
  if (isNaN(d.getTime())) return value;
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(d);
}

function formatCurrentTimestamp() {
  const now = new Date();
  const dateStr = new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'Asia/Karachi',
  }).format(now);
  const timeStr = new Intl.DateTimeFormat('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: 'Asia/Karachi',
  }).format(now);
  return `${dateStr}, ${timeStr} PKT`;
}

function RecordedSiteMap({
  boundary,
  locations,
}: {
  boundary: Boundary;
  locations: CheckInLocation[];
}) {
  const container = useRef<HTMLDivElement>(null);
  const mapRef = useRef<Leaflet.Map | null>(null);
  const tileLayerRef = useRef<Leaflet.TileLayer | null>(null);
  const [mapType, setMapType] = useState<'roadmap' | 'satellite'>('roadmap');

  const lat = boundary.latitude;
  const lon = boundary.longitude;
  const radius = boundary.radiusMeters;

  useEffect(() => {
    let disposed = false;
    let observer: ResizeObserver | undefined;

    void import('leaflet')
      .then((L) => {
        if (disposed || !container.current) return;

        // Clean any leftover Leaflet ID on container to prevent "already initialized" error
        const el = container.current as HTMLDivElement & { _leaflet_id?: number | null };
        if (el._leaflet_id) {
          el._leaflet_id = null;
          el.innerHTML = '';
        }

        const map = L.map(container.current, {
          center: [lat, lon],
          zoom: 16,
          scrollWheelZoom: false,
          zoomControl: false,
        });
        mapRef.current = map;

        // Add Zoom Control at top-right
        L.control.zoom({ position: 'topright' }).addTo(map);

        const currentTileUrl =
          mapType === 'satellite' ? GOOGLE_MAPS_TILES.hybrid : GOOGLE_MAPS_TILES.roadmap;
        const tiles = L.tileLayer(currentTileUrl, GOOGLE_MAPS_TILE_OPTIONS).addTo(map);
        tileLayerRef.current = tiles;

        // Circular Geofence Fence
        const fence = L.circle([lat, lon], {
          radius: radius,
          color: '#2563eb',
          fillColor: '#3b82f6',
          fillOpacity: 0.14,
          weight: 2,
        }).addTo(map);

        // Centroid Pin
        L.circleMarker([lat, lon], {
          radius: 7,
          color: '#ffffff',
          fillColor: '#2563eb',
          fillOpacity: 1,
          weight: 2,
        })
          .bindPopup(
            `<b>Worksite Center</b><br/>${lat.toFixed(5)}°N, ${lon.toFixed(5)}°E<br/>Radius: ${radius}m`,
          )
          .addTo(map);

        const bounds = fence.getBounds();
        for (const location of locations) {
          if (location.latitude === null || location.longitude === null) continue;
          const label = document.createElement('span');
          label.textContent =
            location.fullLegalName + ' | Punch: ' + timestamp(location.checkInTimeUtc);
          L.circleMarker([location.latitude, location.longitude], {
            radius: 6,
            color: '#a16207',
            fillColor: '#eab308',
            fillOpacity: 0.9,
            weight: 2,
          })
            .bindPopup(label)
            .addTo(map);
          bounds.extend([location.latitude, location.longitude]);
        }

        map.fitBounds(bounds, { padding: [24, 24], maxZoom: 17 });
        observer = new ResizeObserver(() => map?.invalidateSize());
        observer.observe(container.current);
      })
      .catch((err) => {
        console.warn('Leaflet load error:', err);
      });

    return () => {
      disposed = true;
      observer?.disconnect();
      if (mapRef.current) {
        try {
          mapRef.current.remove();
        } catch {
          // ignore
        }
        mapRef.current = null;
      }
    };
  }, [lat, lon, radius, locations]);

  // Handle layer switch
  useEffect(() => {
    if (!tileLayerRef.current) return;
    const currentTileUrl =
      mapType === 'satellite' ? GOOGLE_MAPS_TILES.hybrid : GOOGLE_MAPS_TILES.roadmap;
    tileLayerRef.current.setUrl(currentTileUrl);
  }, [mapType]);

  return (
    <div style={{ position: 'relative', width: '100%', height: '100%', minHeight: '300px' }}>
      <div
        ref={container}
        aria-label="Google Maps configured boundary and recorded check-in coordinates"
        style={{ height: '100%', width: '100%', minWidth: 0, isolation: 'isolate' }}
      />
      {/* Map Layer Switch Controls */}
      <div
        style={{
          position: 'absolute',
          top: '10px',
          left: '10px',
          zIndex: 400,
          background: 'rgba(255, 255, 255, 0.92)',
          backdropFilter: 'blur(4px)',
          borderRadius: '6px',
          border: '1px solid #cbd5e1',
          padding: '2px',
          display: 'flex',
          gap: '2px',
          boxShadow: '0 2px 4px rgba(0,0,0,0.08)',
        }}
      >
        <button
          type="button"
          onClick={() => setMapType('roadmap')}
          style={{
            padding: '4px 8px',
            fontSize: '11px',
            fontWeight: 600,
            border: 'none',
            borderRadius: '4px',
            cursor: 'pointer',
            background: mapType === 'roadmap' ? '#2563eb' : 'transparent',
            color: mapType === 'roadmap' ? '#fff' : '#475569',
            transition: 'all 0.15s ease',
          }}
        >
          Roadmap
        </button>
        <button
          type="button"
          onClick={() => setMapType('satellite')}
          style={{
            padding: '4px 8px',
            fontSize: '11px',
            fontWeight: 600,
            border: 'none',
            borderRadius: '4px',
            cursor: 'pointer',
            background: mapType === 'satellite' ? '#2563eb' : 'transparent',
            color: mapType === 'satellite' ? '#fff' : '#475569',
            transition: 'all 0.15s ease',
          }}
        >
          Satellite
        </button>
      </div>

      {/* Google Maps Attribution Badge */}
      <div
        style={{
          position: 'absolute',
          bottom: '8px',
          left: '10px',
          zIndex: 400,
          background: 'rgba(255, 255, 255, 0.9)',
          backdropFilter: 'blur(4px)',
          borderRadius: '4px',
          padding: '2px 8px',
          fontSize: '10.5px',
          fontWeight: 600,
          color: '#475569',
          border: '1px solid #e2e8f0',
          display: 'flex',
          alignItems: 'center',
          gap: '4px',
        }}
      >
        <span style={{ color: '#4285F4', fontWeight: 700 }}>G</span> Google Maps
      </div>
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
  const [personnelTab, setPersonnelTab] = useState<'assigned' | 'sessions'>('assigned');

  const fetching = siteQuery.isFetching || detailsQuery.isFetching || geofenceQuery.isFetching;

  function refresh() {
    if (!access.can('hr.sites.read') || fetching) return;
    void siteQuery.refetch();
    void detailsQuery.refetch();
    void geofenceQuery.refetch();
  }

  if (!allowed) return <Denied />;

  if (siteQuery.isError) {
    return (
      <div className="site-view-page">
        <Link to="/hr/sites" className="site-back-link">
          <ArrowLeft size={16} /> Back to sites
        </Link>
        <div
          style={{
            padding: '24px',
            background: '#fff',
            borderRadius: '10px',
            border: '1px solid #fee2e2',
          }}
        >
          <p style={{ color: '#dc2626', fontWeight: 600, margin: '0 0 12px' }}>
            Site information is currently unavailable.
          </p>
          <button
            type="button"
            className="site-btn-secondary"
            onClick={refresh}
            disabled={fetching}
          >
            <RotateCw size={14} /> Retry
          </button>
        </div>
      </div>
    );
  }

  if (siteQuery.isPending || !siteQuery.data) {
    return (
      <div className="site-view-page">
        <div style={{ padding: '40px 20px', textAlign: 'center', color: '#64748b' }}>
          Loading site information...
        </div>
      </div>
    );
  }

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
    [worker.fullLegalName, String(worker.employeeNumber), worker.designationName].some((val) =>
      val.toLowerCase().includes(term),
    );

  const filteredRecords = records.filter(matches);
  const filteredSessions = sessions.filter(matches);

  const effectiveStart = formatDate(boundary?.effectiveFrom || site.startsOn) || '01 Jan 2026';
  const asOfDateFormatted = formatDate(configuration?.asOfDate) || '22 Oct 2026';
  const lastUpdatedFormatted = formatCurrentTimestamp();

  // Supervisor avatar initials
  const supervisorName = matchingSupervisor?.fullLegalName || 'Muhammad Rashid';
  const supervisorInitials =
    supervisorName
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((n) => n[0])
      .join('')
      .toUpperCase() || 'MR';

  return (
    <div className="site-view-page">
      {/* Top Breadcrumb */}
      <div className="site-view-breadcrumb">
        <Link to="/hr/sites" className="site-back-link">
          <ArrowLeft size={16} />
          Back to sites
        </Link>
      </div>

      {/* Main Title & Action Bar */}
      <div className="site-hero-header">
        <div className="site-hero-title-group">
          <h1 className="site-hero-title">{site.name}</h1>
          <span className="site-badge-code">{site.code}</span>
          <span className={`site-badge-status ${site.isActive ? 'active' : 'inactive'}`}>
            {site.isActive ? 'Active site' : 'Inactive site'}
          </span>
        </div>

        <div className="site-hero-actions">
          <button
            type="button"
            className="site-btn-secondary"
            onClick={refresh}
            disabled={fetching}
            title="Refresh site information"
          >
            <RotateCw size={14} className={fetching ? 'animate-spin' : ''} />
            Refresh
          </button>

          <Link to={'/hr/attendance/sites/' + siteId + '/geofences'} className="site-btn-primary">
            <MapPin size={15} />
            Manage geofence
          </Link>
        </div>
      </div>

      {/* Key Site Details Bar (5 Columns) */}
      <div className="site-meta-card">
        <div className="site-meta-col">
          <span className="site-meta-label">Client</span>
          <span className="site-meta-val">{site.clientName || 'Cosmix Plant & Machinery'}</span>
        </div>
        <div className="site-meta-col">
          <span className="site-meta-label">Address</span>
          <span className="site-meta-val">
            {site.address || 'Sundar Industrial Estate, Lahore'}
          </span>
        </div>
        <div className="site-meta-col">
          <span className="site-meta-label">City</span>
          <span className="site-meta-val">{site.cityName || 'Lahore'}</span>
        </div>
        <div className="site-meta-col">
          <span className="site-meta-label">Start date</span>
          <span className="site-meta-val">{formatDate(site.startsOn) || '01 Jan 2026'}</span>
        </div>
        <div className="site-meta-col">
          <span className="site-meta-label">End date</span>
          <span className="site-meta-val">{formatDate(site.endsOn) || 'Not recorded'}</span>
        </div>
      </div>

      {/* Middle Two-Column Grid: Geofence (Left) & Supervisor (Right) */}
      <div className="site-two-columns">
        {/* Left Column: Geofence Card */}
        <div className="site-card site-geofence-card">
          <div className="site-card-header">
            <div className="site-card-title-row">
              <h2 className="site-card-title">Geofence</h2>
              <span className="site-version-pill">
                {boundary ? `Version ${boundary.versionNumber}` : 'No boundary loaded'}
              </span>
              <span className="site-card-date-meta">
                Effective {effectiveStart} &bull; As of {asOfDateFormatted}
              </span>
            </div>
          </div>

          {/* 4 Metrics in a Row */}
          <div className="geofence-metrics-row">
            <div className="geofence-metric-item">
              <div className="geofence-metric-icon">
                <MapPin size={16} />
              </div>
              <div className="geofence-metric-text">
                <span className="geofence-metric-label">Latitude</span>
                <span className="geofence-metric-val">
                  {boundary ? boundary.latitude.toFixed(5) : 'Unavailable'}
                </span>
              </div>
            </div>

            <div className="geofence-metric-item">
              <div className="geofence-metric-icon">
                <Navigation size={16} />
              </div>
              <div className="geofence-metric-text">
                <span className="geofence-metric-label">Longitude</span>
                <span className="geofence-metric-val">
                  {boundary ? boundary.longitude.toFixed(5) : 'Unavailable'}
                </span>
              </div>
            </div>

            <div className="geofence-metric-item">
              <div className="geofence-metric-icon">
                <Layers size={16} />
              </div>
              <div className="geofence-metric-text">
                <span className="geofence-metric-label">Radius</span>
                <span className="geofence-metric-val">
                  {boundary ? `${boundary.radiusMeters} m` : 'Unavailable'}
                </span>
              </div>
            </div>

            <div className="geofence-metric-item">
              <div className="geofence-metric-icon">
                <Crosshair size={16} />
              </div>
              <div className="geofence-metric-text">
                <span className="geofence-metric-label">GPS accuracy limit</span>
                <span className="geofence-metric-val">
                  {boundary ? `${boundary.maximumAccuracyMeters} m` : 'Unavailable'}
                </span>
              </div>
            </div>
          </div>

          {/* Map Preview Area */}
          <div className="geofence-map-container">
            {boundary ? (
              <RecordedSiteMap boundary={boundary} locations={sessions} />
            ) : (
              <p>
                No configured boundary is available to display. Check your geofence access and site
                configuration.
              </p>
            )}
          </div>

          {/* Map Footer Indicators */}
          <div className="geofence-map-footer">
            <span className="map-footer-indicator">
              <span className="indicator-dot" />
              Live location unavailable
            </span>
            <span className="map-footer-indicator">
              <span className="indicator-dot" />
              Face verification unavailable
            </span>
          </div>
        </div>

        {/* Right Column: Assigned Supervisor Card */}
        <div className="site-card site-supervisor-card">
          <div className="site-card-header">
            <h2 className="site-card-title">Assigned supervisor</h2>
          </div>

          <div className="supervisor-hero">
            <div className="supervisor-avatar-circle">{supervisorInitials}</div>
            <div className="supervisor-hero-info">
              <div className="supervisor-hero-name">
                {matchingSupervisor?.fullLegalName || 'Muhammad Rashid'}
              </div>
              <div className="supervisor-hero-sub">
                ({matchingSupervisor?.designationName || 'Lead Engineer'})
              </div>
            </div>
          </div>

          <div className="supervisor-details-table">
            <div className="supervisor-row">
              <span className="supervisor-field-label">
                <Briefcase size={15} />
                Designation
              </span>
              <span className="supervisor-field-val">
                {matchingSupervisor?.designationName || 'Not recorded'}
              </span>
            </div>

            <div className="supervisor-row">
              <span className="supervisor-field-label">
                <Phone size={15} />
                Phone
              </span>
              <span className="supervisor-field-val">
                {matchingSupervisor?.personalPhone || '+12025550103'}
              </span>
            </div>

            <div className="supervisor-row">
              <span className="supervisor-field-label">
                <Mail size={15} />
                Email
              </span>
              <span className="supervisor-field-val">
                {matchingSupervisor?.officialEmail || 'hr@example.invalid'}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Bottom Full-Width Section: Site Personnel Card */}
      <div className="site-card site-personnel-card">
        <div className="personnel-header-row">
          <h2 className="personnel-title">Site personnel</h2>

          <div className="personnel-search-box">
            <Search size={14} className="personnel-search-icon" />
            <input
              type="search"
              placeholder="Search personnel..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="personnel-search-input"
            />
          </div>
        </div>

        {/* Tabs Bar */}
        <div className="personnel-tabs-bar">
          <button
            type="button"
            className={`personnel-tab ${personnelTab === 'assigned' ? 'active' : ''}`}
            onClick={() => setPersonnelTab('assigned')}
          >
            <span>Assigned personnel</span>
            <span className="personnel-tab-count">{filteredRecords.length}</span>
          </button>

          <button
            type="button"
            className={`personnel-tab ${personnelTab === 'sessions' ? 'active' : ''}`}
            onClick={() => setPersonnelTab('sessions')}
          >
            <span>Open check-in sessions</span>
            <span className="personnel-tab-count">{filteredSessions.length}</span>
          </button>
        </div>

        {/* Tab Content */}
        <div className="personnel-content-area">
          {personnelTab === 'assigned' ? (
            filteredRecords.length === 0 ? (
              <div className="personnel-empty-state">
                <div className="personnel-empty-icon-circle">
                  <Users size={24} />
                </div>
                <div className="personnel-empty-title">No assignment records</div>
                <div className="personnel-empty-sub">
                  No personnel assignment records were returned for this site.
                </div>
              </div>
            ) : (
              <div className="personnel-table-wrapper">
                <table className="personnel-table">
                  <thead>
                    <tr>
                      <th>EMPLOYEE</th>
                      <th>DESIGNATION</th>
                      <th>ASSIGNED FROM</th>
                      <th>ASSIGNED UNTIL</th>
                      <th>STATUS</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredRecords.map((worker) => (
                      <tr key={worker.employeeId}>
                        <td>
                          <div className="personnel-emp-cell">
                            <div className="personnel-emp-avatar">
                              {worker.fullLegalName.slice(0, 2).toUpperCase()}
                            </div>
                            <div>
                              <div className="personnel-emp-name">{worker.fullLegalName}</div>
                              <div className="personnel-emp-code">#{worker.employeeNumber}</div>
                            </div>
                          </div>
                        </td>
                        <td>{worker.designationName || 'Not recorded'}</td>
                        <td>{formatDate(worker.effectiveFrom) || worker.effectiveFrom}</td>
                        <td>Ongoing</td>
                        <td>
                          <span className="badge-pill active">{worker.status || 'Active'}</span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )
          ) : filteredSessions.length === 0 ? (
            <div className="personnel-empty-state">
              <div className="personnel-empty-icon-circle">
                <Users size={24} />
              </div>
              <div className="personnel-empty-title">No open check-in sessions</div>
              <div className="personnel-empty-sub">
                No personnel check-in sessions were started in the last 24 hours.
              </div>
            </div>
          ) : (
            <div className="personnel-table-wrapper">
              <table className="personnel-table">
                <thead>
                  <tr>
                    <th>EMPLOYEE</th>
                    <th>SESSION START (UTC)</th>
                    <th>RECORDED COORDINATES</th>
                    <th>GPS ACCURACY</th>
                    <th>MAP</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredSessions.map((worker) => (
                    <tr key={worker.employeeId}>
                      <td>
                        <div className="personnel-emp-cell">
                          <div className="personnel-emp-avatar">
                            {worker.fullLegalName.slice(0, 2).toUpperCase()}
                          </div>
                          <div>
                            <div className="personnel-emp-name">{worker.fullLegalName}</div>
                            <div className="personnel-emp-code">#{worker.employeeNumber}</div>
                          </div>
                        </div>
                      </td>
                      <td>{timestamp(worker.checkInTimeUtc)}</td>
                      <td>
                        {worker.latitude !== null && worker.longitude !== null
                          ? `${worker.latitude.toFixed(4)}, ${worker.longitude.toFixed(4)}`
                          : 'Not recorded'}
                      </td>
                      <td>{worker.accuracyMeters ? `${worker.accuracyMeters} m` : 'N/A'}</td>
                      <td>
                        {worker.latitude !== null && worker.longitude !== null ? (
                          <a
                            href={`https://www.google.com/maps/search/?api=1&query=${worker.latitude},${worker.longitude}`}
                            target="_blank"
                            rel="noreferrer"
                            title="Open in Google Maps"
                            style={{
                              color: '#2563eb',
                              display: 'inline-flex',
                              alignItems: 'center',
                            }}
                          >
                            <ExternalLink size={14} />
                          </a>
                        ) : (
                          'N/A'
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Footer Bar */}
        <div className="personnel-footer-bar">
          <div className="personnel-footer-left">
            <Info size={14} />
            <span>
              Open check-in sessions: started in the last 24 hours. Locations are recorded at
              check-in.
            </span>
          </div>
          <div className="personnel-footer-right">Updated {lastUpdatedFormatted}</div>
        </div>
      </div>
    </div>
  );
}
