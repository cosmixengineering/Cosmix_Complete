import { MapPin, Target, ShieldCheck } from 'lucide-react';

export function CirclePreview({
  latitude,
  longitude,
  radiusMeters,
  maximumAccuracyMeters,
}: {
  latitude: number;
  longitude: number;
  radiusMeters: number;
  maximumAccuracyMeters: number;
}) {
  if (
    ![latitude, longitude, radiusMeters, maximumAccuracyMeters].every(Number.isFinite) ||
    latitude < -90 ||
    latitude > 90 ||
    longitude < -180 ||
    longitude > 180 ||
    radiusMeters <= 0 ||
    maximumAccuracyMeters <= 0
  )
    return (
      <p className="attendance-error">
        Enter valid circular boundary coordinates and radius values.
      </p>
    );

  return (
    <div className="attendance-preview">
      <div
        className="attendance-circle-plane"
        role="img"
        aria-label={`Circular boundary centered at ${latitude}, ${longitude}, radius ${radiusMeters} meters. Schematic.`}
      >
        <div className="attendance-circle">
          <MapPin size={24} aria-hidden="true" />
        </div>
        <span
          style={{
            position: 'absolute',
            bottom: '10px',
            right: '12px',
            fontSize: '10.5px',
            color: '#38bdf8',
            fontFamily: 'ui-monospace, monospace',
            fontWeight: 600,
            background: 'rgba(15, 23, 42, 0.75)',
            padding: '2px 6px',
            borderRadius: '4px',
            border: '1px solid rgba(56, 189, 248, 0.3)',
          }}
        >
          {radiusMeters}m RAD
        </span>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '14px', flex: 1 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <span className="badge badge--positive" style={{ gap: '5px' }}>
            <Target size={12} />
            Calibrated GPS Perimeter
          </span>
          <span style={{ fontSize: '11.5px', color: '#64748b' }}>Schematic preview</span>
        </div>

        <div className="attendance-metric-grid">
          <div className="attendance-metric-box">
            <dt>Center Latitude</dt>
            <dd>{latitude.toFixed(6)}°</dd>
          </div>
          <div className="attendance-metric-box">
            <dt>Center Longitude</dt>
            <dd>{longitude.toFixed(6)}°</dd>
          </div>
          <div className="attendance-metric-box">
            <dt>Allowed Radius</dt>
            <dd>{radiusMeters} m</dd>
          </div>
          <div className="attendance-metric-box">
            <dt>Max GPS Accuracy</dt>
            <dd>±{maximumAccuracyMeters} m</dd>
          </div>
        </div>

        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '6px',
            fontSize: '12px',
            color: '#64748b',
          }}
        >
          <ShieldCheck size={14} style={{ color: '#10b981' }} />
          <span>
            Mobile clock-ins outside this perimeter or with accuracy &gt; {maximumAccuracyMeters}m
            are rejected.
          </span>
        </div>
      </div>
    </div>
  );
}
