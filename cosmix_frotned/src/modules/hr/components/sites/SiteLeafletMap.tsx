import { useEffect, useRef, useState, useMemo } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { GOOGLE_MAPS_TILES } from '@/utils/google-maps';
import {
  Compass,
  ExternalLink,
  Layers,
  Navigation,
  RotateCcw,
  ZoomIn,
  ZoomOut,
} from 'lucide-react';

export interface SiteWorker {
  name: string;
  number: number;
  role: string;
  phone: string;
  email: string;
  checkIn: string;
  faceScore: string;
  distanceMeters: number;
  angleDeg: number;
  isInside: boolean;
}

interface SiteLeafletMapProps {
  siteName: string;
  latitude: number;
  longitude: number;
  radiusMeters: number;
  accuracyMeters?: number;
  workers: SiteWorker[];
  workerFilter: 'all' | 'inside' | 'perimeter';
  onFilterChange: (filter: 'all' | 'inside' | 'perimeter') => void;
  selectedWorkerNumber: number | null;
  onSelectWorker: (number: number | null) => void;
}

// Convert polar offset (distance + angle) to real lat/long relative to centroid
function calculateWorkerCoords(
  centerLat: number,
  centerLon: number,
  distanceMeters: number,
  angleDeg: number,
): [number, number] {
  const rad = (angleDeg * Math.PI) / 180;
  const dLat = (distanceMeters * Math.sin(rad)) / 111320;
  const dLon = (distanceMeters * Math.cos(rad)) / (111320 * Math.cos((centerLat * Math.PI) / 180));
  return [centerLat + dLat, centerLon + dLon];
}

export function SiteLeafletMap({
  siteName,
  latitude,
  longitude,
  radiusMeters,
  workers,
  workerFilter,
  onFilterChange,
  selectedWorkerNumber,
  onSelectWorker,
}: SiteLeafletMapProps) {
  const mapContainerRef = useRef<HTMLDivElement | null>(null);
  const mapInstanceRef = useRef<L.Map | null>(null);
  const tileLayerRef = useRef<L.TileLayer | null>(null);
  const markersLayerRef = useRef<L.LayerGroup | null>(null);
  const circleRef = useRef<L.Circle | null>(null);

  const [mapType, setMapType] = useState<'streets' | 'satellite'>('streets');

  // Filtered workers
  const displayedWorkers = useMemo(() => {
    return workers.filter((w) => {
      if (workerFilter === 'inside') return w.distanceMeters <= radiusMeters * 0.85;
      if (workerFilter === 'perimeter') return w.distanceMeters > radiusMeters * 0.85;
      return true;
    });
  }, [workers, workerFilter, radiusMeters]);

  // Initialize Leaflet map
  useEffect(() => {
    if (!mapContainerRef.current) return;

    if (!mapInstanceRef.current) {
      const map = L.map(mapContainerRef.current, {
        center: [latitude, longitude],
        zoom: 16,
        zoomControl: false,
        attributionControl: false,
      });

      // Official Google Maps tiles (Roadmap & Hybrid Satellite)
      const tileUrl =
        mapType === 'satellite' ? GOOGLE_MAPS_TILES.hybrid : GOOGLE_MAPS_TILES.roadmap;

      const tile = L.tileLayer(tileUrl, {
        maxZoom: 20,
        attribution: '&copy; Google Maps',
      }).addTo(map);

      tileLayerRef.current = tile;

      const markersGroup = L.layerGroup().addTo(map);
      markersLayerRef.current = markersGroup;

      mapInstanceRef.current = map;
    }

    return () => {
      if (mapInstanceRef.current) {
        mapInstanceRef.current.remove();
        mapInstanceRef.current = null;
        markersLayerRef.current = null;
        tileLayerRef.current = null;
      }
    };
  }, []);

  // Handle tile switch (streets vs satellite)
  useEffect(() => {
    if (!mapInstanceRef.current || !tileLayerRef.current) return;

    const tileUrl = mapType === 'satellite' ? GOOGLE_MAPS_TILES.hybrid : GOOGLE_MAPS_TILES.roadmap;

    tileLayerRef.current.setUrl(tileUrl);
  }, [mapType]);

  // Update map objects when coordinates, workers, or filters change
  useEffect(() => {
    const map = mapInstanceRef.current;
    const markersGroup = markersLayerRef.current;
    if (!map || !markersGroup) return;

    markersGroup.clearLayers();

    // 1. Draw Real GPS Geofence Circle
    const geofenceCircle = L.circle([latitude, longitude], {
      radius: radiusMeters,
      color: '#16a34a',
      weight: 2,
      dashArray: '6, 6',
      fillColor: '#22c55e',
      fillOpacity: 0.12,
    }).addTo(markersGroup);

    geofenceCircle.bindPopup(`
      <div style="font-family: inherit; font-size: 12px; line-height: 1.4;">
        <strong style="color: #166534; font-size: 13px;">⚡ GEOFENCE PERIMETER</strong><br/>
        Radius: <b>${radiusMeters} Meters</b><br/>
        Authorized Workforce Zone: Active
      </div>
    `);
    circleRef.current = geofenceCircle;

    // 2. Site Centroid Marker
    const centroidIcon = L.divIcon({
      className: 'leaflet-centroid-div',
      html: `
        <div style="
          position: relative;
          display: flex;
          align-items: center;
          justify-content: center;
          width: 32px;
          height: 32px;
        ">
          <div style="
            position: absolute;
            width: 32px;
            height: 32px;
            border-radius: 50%;
            background: rgba(2, 132, 199, 0.25);
            animation: pulse-ring 2s infinite ease-out;
          "></div>
          <div style="
            width: 22px;
            height: 22px;
            border-radius: 50%;
            background: #0284c7;
            border: 2.5px solid #ffffff;
            display: flex;
            align-items: center;
            justify-content: center;
            box-shadow: 0 2px 6px rgba(0,0,0,0.35);
            color: #ffffff;
            font-size: 12px;
          ">📍</div>
        </div>
      `,
      iconSize: [32, 32],
      iconAnchor: [16, 16],
    });

    const centroidMarker = L.marker([latitude, longitude], { icon: centroidIcon }).addTo(
      markersGroup,
    );

    centroidMarker.bindPopup(`
      <div style="font-family: inherit; font-size: 12px; padding: 2px;">
        <div style="font-weight: 750; color: #0284c7; font-size: 13px;">${siteName}</div>
        <div style="color: #64748b; margin-top: 2px;">Site Centroid (Datum Pin)</div>
        <div style="margin-top: 6px; font-size: 11px; background: #f8fafc; padding: 4px 6px; border-radius: 4px; border: 1px solid #e2e8f0;">
          GPS: <b>${latitude.toFixed(5)}°N, ${longitude.toFixed(5)}°E</b><br/>
          Geofence Tolerance: <b>±${radiusMeters}m</b>
        </div>
      </div>
    `);

    // 3. Render Worker Pins
    displayedWorkers.forEach((w) => {
      const [wLat, wLon] = calculateWorkerCoords(latitude, longitude, w.distanceMeters, w.angleDeg);

      const isInside = w.distanceMeters <= radiusMeters * 0.85;
      const isSelected = selectedWorkerNumber === w.number;
      const initials =
        w.name
          .split(' ')
          .filter((p) => !p.startsWith('Engr.'))
          .map((p) => p[0])
          .join('')
          .slice(0, 2)
          .toUpperCase() || 'WK';

      const pinColor = isInside ? '#16a34a' : '#d97706';
      const borderStyle = isSelected ? '3px solid #38bdf8' : '2px solid #ffffff';

      const workerIcon = L.divIcon({
        className: 'leaflet-worker-div',
        html: `
          <div style="
            position: relative;
            display: flex;
            flex-direction: column;
            align-items: center;
            cursor: pointer;
            transform: ${isSelected ? 'scale(1.15)' : 'scale(1)'};
            transition: transform 0.15s ease;
          ">
            <div style="
              width: 26px;
              height: 26px;
              border-radius: 50%;
              background: ${pinColor};
              border: ${borderStyle};
              color: #ffffff;
              display: flex;
              align-items: center;
              justify-content: center;
              font-size: 9px;
              font-weight: 800;
              box-shadow: 0 2px 6px rgba(0,0,0,0.35);
            ">
              ${initials}
            </div>
            <div style="
              margin-top: 2px;
              background: rgba(15, 23, 42, 0.88);
              color: #ffffff;
              font-size: 9px;
              font-weight: 650;
              padding: 1px 5px;
              border-radius: 3px;
              white-space: nowrap;
              border: 1px solid rgba(255,255,255,0.25);
              box-shadow: 0 1px 3px rgba(0,0,0,0.3);
            ">
              ${w.name.split(' ')[0]} (${Math.round(w.distanceMeters)}m)
            </div>
          </div>
        `,
        iconSize: [60, 44],
        iconAnchor: [30, 13],
      });

      const workerMarker = L.marker([wLat, wLon], { icon: workerIcon }).addTo(markersGroup);

      workerMarker.on('click', () => {
        onSelectWorker(w.number);
      });

      workerMarker.bindPopup(`
        <div style="font-family: inherit; font-size: 12px; min-width: 190px;">
          <div style="display: flex; align-items: center; justify-content: space-between; gap: 8px;">
            <strong style="font-size: 13px; color: #0f172a;">${w.name}</strong>
            <span style="
              font-size: 10px;
              font-weight: 700;
              padding: 2px 5px;
              border-radius: 10px;
              background: ${isInside ? '#dcfce7' : '#fef3c7'};
              color: ${isInside ? '#166534' : '#92400e'};
            ">
              ${isInside ? 'Inside Site' : 'Boundary Gate'}
            </span>
          </div>
          <div style="color: #64748b; font-size: 11px; margin-top: 1px;">${w.role} (EMP #${w.number})</div>
          
          <div style="margin-top: 8px; font-size: 11px; background: #f8fafc; padding: 6px 8px; border-radius: 5px; border: 1px solid #e2e8f0; display: flex; flex-direction: column; gap: 3px;">
            <div>Check-in: <b>${w.checkIn}</b></div>
            <div>Face Match: <b style="color: #16a34a;">${w.faceScore}</b></div>
            <div>Distance: <b>${Math.round(w.distanceMeters)} meters</b></div>
            <div>Phone: <b>${w.phone}</b></div>
          </div>
        </div>
      `);
    });
  }, [latitude, longitude, radiusMeters, displayedWorkers, selectedWorkerNumber, siteName]);

  // Center on worker if selected
  useEffect(() => {
    if (!mapInstanceRef.current || selectedWorkerNumber == null) return;
    const worker = workers.find((w) => w.number === selectedWorkerNumber);
    if (!worker) return;

    const [wLat, wLon] = calculateWorkerCoords(
      latitude,
      longitude,
      worker.distanceMeters,
      worker.angleDeg,
    );

    mapInstanceRef.current.setView([wLat, wLon], 17, { animate: true });
  }, [selectedWorkerNumber, workers, latitude, longitude]);

  const handleZoomIn = () => mapInstanceRef.current?.zoomIn();
  const handleZoomOut = () => mapInstanceRef.current?.zoomOut();
  const handleCenter = () => {
    onSelectWorker(null);
    mapInstanceRef.current?.setView([latitude, longitude], 16, { animate: true });
  };

  return (
    <div className="site-map-card">
      {/* Sleek Compact Toolbar */}
      <div className="site-map-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <Navigation size={14} color="#2563eb" />
          <span style={{ fontSize: '13px', fontWeight: 700, color: '#0f172a' }}>
            Live GPS Map & Geofence
          </span>
          <span
            style={{
              fontSize: '10px',
              padding: '1px 6px',
              borderRadius: '10px',
              background: '#eff6ff',
              color: '#1d4ed8',
              fontWeight: 700,
              border: '1px solid #bfdbfe',
            }}
          >
            {displayedWorkers.length} Active Pins
          </span>
        </div>

        <div className="site-map-tools">
          {/* Filter Pills */}
          <button
            type="button"
            className={`site-map-filter-btn ${workerFilter === 'all' ? 'active' : ''}`}
            onClick={() => onFilterChange('all')}
          >
            All ({workers.length})
          </button>
          <button
            type="button"
            className={`site-map-filter-btn ${workerFilter === 'inside' ? 'active' : ''}`}
            onClick={() => onFilterChange('inside')}
          >
            Inside Core ({workers.filter((w) => w.distanceMeters <= radiusMeters * 0.85).length})
          </button>
          <button
            type="button"
            className={`site-map-filter-btn ${workerFilter === 'perimeter' ? 'active' : ''}`}
            onClick={() => onFilterChange('perimeter')}
          >
            Boundary ({workers.filter((w) => w.distanceMeters > radiusMeters * 0.85).length})
          </button>

          <div
            style={{
              height: '16px',
              width: '1px',
              background: '#e2e8f0',
              margin: '0 2px',
            }}
          />

          {/* Map Layer Switch (Street vs Satellite) */}
          <button
            type="button"
            className={`site-map-filter-btn ${mapType === 'satellite' ? 'active' : ''}`}
            onClick={() => setMapType((m) => (m === 'streets' ? 'satellite' : 'streets'))}
            title={mapType === 'streets' ? 'Switch to Satellite Imagery' : 'Switch to Street Map'}
            style={{ display: 'flex', alignItems: 'center', gap: '4px' }}
          >
            <Layers size={12} />
            <span>{mapType === 'streets' ? 'Satellite' : 'Street Map'}</span>
          </button>

          {/* Controls */}
          <button type="button" className="map-control-btn" title="Zoom In" onClick={handleZoomIn}>
            <ZoomIn size={14} />
          </button>
          <button
            type="button"
            className="map-control-btn"
            title="Zoom Out"
            onClick={handleZoomOut}
          >
            <ZoomOut size={14} />
          </button>
          <button
            type="button"
            className="map-control-btn"
            title="Recenter on Site Centroid"
            onClick={handleCenter}
          >
            <RotateCcw size={13} />
          </button>

          {/* External Google Maps Link */}
          <a
            href={`https://www.google.com/maps/search/?api=1&query=${latitude},${longitude}`}
            target="_blank"
            rel="noreferrer"
            className="map-control-btn"
            title="Open in Google Maps"
            style={{ textDecoration: 'none' }}
          >
            <ExternalLink size={13} />
          </a>
        </div>
      </div>

      {/* Real Interactive Leaflet Container */}
      <div style={{ position: 'relative', width: '100%', height: '400px', background: '#e2e8f0' }}>
        <div ref={mapContainerRef} style={{ width: '100%', height: '100%', zIndex: 1 }} />

        {/* Floating Centroid Info Badge */}
        <div
          style={{
            position: 'absolute',
            bottom: '10px',
            right: '12px',
            zIndex: 400,
            background: 'rgba(15, 23, 42, 0.88)',
            backdropFilter: 'blur(6px)',
            color: '#f8fafc',
            padding: '5px 10px',
            borderRadius: '6px',
            fontSize: '11px',
            fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
            display: 'flex',
            alignItems: 'center',
            gap: '6px',
            border: '1px solid rgba(255, 255, 255, 0.15)',
            boxShadow: '0 2px 8px rgba(0,0,0,0.25)',
          }}
        >
          <Compass size={13} color="#38bdf8" />
          <span>
            {latitude.toFixed(5)}°N, {longitude.toFixed(5)}°E &middot; RADIUS: {radiusMeters}m
          </span>
        </div>

        {/* Quick Legend Overlay */}
        <div
          style={{
            position: 'absolute',
            top: '10px',
            left: '12px',
            zIndex: 400,
            background: 'rgba(255, 255, 255, 0.92)',
            backdropFilter: 'blur(6px)',
            padding: '6px 10px',
            borderRadius: '6px',
            fontSize: '11px',
            color: '#1e293b',
            display: 'flex',
            flexDirection: 'column',
            gap: '4px',
            border: '1px solid #cbd5e1',
            boxShadow: '0 2px 6px rgba(0,0,0,0.08)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <span
              style={{
                width: '8px',
                height: '8px',
                borderRadius: '50%',
                background: '#0284c7',
                display: 'inline-block',
              }}
            />
            <span>Centroid Datum Pin</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <span
              style={{
                width: '8px',
                height: '8px',
                borderRadius: '50%',
                background: '#16a34a',
                display: 'inline-block',
              }}
            />
            <span>Inside Geofence</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <span
              style={{
                width: '8px',
                height: '8px',
                borderRadius: '50%',
                background: '#d97706',
                display: 'inline-block',
              }}
            />
            <span>Boundary Gate</span>
          </div>
        </div>
      </div>
    </div>
  );
}
