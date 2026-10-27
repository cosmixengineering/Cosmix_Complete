import { useEffect, useRef, useState } from 'react';
import { Building2, Crosshair, Minus, Plus } from 'lucide-react';
import { getGoogleTileUrl } from '@/utils/google-maps';

export interface InteractiveGeofenceMapProps {
  latitude: number;
  longitude: number;
  radiusMeters: number;
  onCoordinatesChange: (lat: number, lon: number) => void;
  onRadiusChange?: (radius: number) => void;
}

function project(lat: number, lon: number, zoom: number) {
  const siny = Math.sin((lat * Math.PI) / 180);
  const clampedSiny = Math.min(Math.max(siny, -0.9999), 0.9999);
  const scale = 256 * Math.pow(2, zoom);
  const x = scale * (0.5 + lon / 360);
  const y = scale * (0.5 - Math.log((1 + clampedSiny) / (1 - clampedSiny)) / (4 * Math.PI));
  return { x, y };
}

function unproject(x: number, y: number, zoom: number) {
  const scale = 256 * Math.pow(2, zoom);
  const lon = (x / scale - 0.5) * 360;
  const y2 = (0.5 - y / scale) * 4 * Math.PI;
  const lat = (Math.atan(Math.sinh(y2)) * 180) / Math.PI;
  return { lat, lon };
}

export function InteractiveGeofenceMap({
  latitude,
  longitude,
  radiusMeters,
  onCoordinatesChange,
  onRadiusChange,
}: InteractiveGeofenceMapProps) {
  const [zoom, setZoom] = useState(16);
  const [layer, setLayer] = useState<'roads' | 'satellite'>('roads');
  const [locating, setLocating] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const [size, setSize] = useState({ width: 720, height: 320 });

  // Update container size dynamically
  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        if (entry.contentRect.width > 0 && entry.contentRect.height > 0) {
          setSize({
            width: Math.round(entry.contentRect.width),
            height: Math.round(entry.contentRect.height),
          });
        }
      }
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  // Dragging state
  const dragRef = useRef<{
    startX: number;
    startY: number;
    startLat: number;
    startLon: number;
    hasMoved: boolean;
  } | null>(null);

  const handleMouseDown = (e: React.MouseEvent) => {
    // Only left click
    if (e.button !== 0) return;
    dragRef.current = {
      startX: e.clientX,
      startY: e.clientY,
      startLat: latitude,
      startLon: longitude,
      hasMoved: false,
    };
  };

  const handleMouseMove = (e: React.MouseEvent) => {
    if (!dragRef.current) return;
    const dx = e.clientX - dragRef.current.startX;
    const dy = e.clientY - dragRef.current.startY;
    if (Math.abs(dx) > 3 || Math.abs(dy) > 3) {
      dragRef.current.hasMoved = true;
    }
    if (!dragRef.current.hasMoved) return;

    const startPixel = project(dragRef.current.startLat, dragRef.current.startLon, zoom);
    const newPixelX = startPixel.x - dx;
    const newPixelY = startPixel.y - dy;
    const { lat, lon } = unproject(newPixelX, newPixelY, zoom);
    onCoordinatesChange(Number(lat.toFixed(7)), Number(lon.toFixed(7)));
  };

  const handleMouseUp = (e: React.MouseEvent) => {
    if (!dragRef.current) return;
    // If it was a simple click without dragging, center on clicked point
    if (!dragRef.current.hasMoved && containerRef.current) {
      const rect = containerRef.current.getBoundingClientRect();
      const clickX = e.clientX - rect.left;
      const clickY = e.clientY - rect.top;
      const centerPixel = project(latitude, longitude, zoom);
      const offsetX = clickX - size.width / 2;
      const offsetY = clickY - size.height / 2;
      const targetPixelX = centerPixel.x + offsetX;
      const targetPixelY = centerPixel.y + offsetY;
      const { lat, lon } = unproject(targetPixelX, targetPixelY, zoom);
      onCoordinatesChange(Number(lat.toFixed(7)), Number(lon.toFixed(7)));
    }
    dragRef.current = null;
  };

  const handleMyGps = () => {
    if (!navigator.geolocation) {
      alert('Geolocation is not supported by your browser.');
      return;
    }
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setLocating(false);
        onCoordinatesChange(
          Number(pos.coords.latitude.toFixed(7)),
          Number(pos.coords.longitude.toFixed(7)),
        );
        setZoom(17);
      },
      (err) => {
        setLocating(false);
        alert(`Could not retrieve GPS coordinates: ${err.message}`);
      },
      { enableHighAccuracy: true, timeout: 10000 },
    );
  };

  // Compute slippy map tiles
  const centerPixel = project(latitude, longitude, zoom);
  const minTileX = Math.floor((centerPixel.x - size.width / 2) / 256);
  const maxTileX = Math.floor((centerPixel.x + size.width / 2) / 256);
  const minTileY = Math.floor((centerPixel.y - size.height / 2) / 256);
  const maxTileY = Math.floor((centerPixel.y + size.height / 2) / 256);
  const maxIndex = Math.pow(2, zoom) - 1;

  const tiles: Array<{ key: string; left: number; top: number; url: string }> = [];
  for (let ty = minTileY; ty <= maxTileY; ty++) {
    if (ty < 0 || ty > maxIndex) continue;
    for (let tx = minTileX; tx <= maxTileX; tx++) {
      const txNorm = ((tx % (maxIndex + 1)) + (maxIndex + 1)) % (maxIndex + 1);
      const left = Math.round(tx * 256 - (centerPixel.x - size.width / 2));
      const top = Math.round(ty * 256 - (centerPixel.y - size.height / 2));
      const url = getGoogleTileUrl(layer === 'roads' ? 'roadmap' : 'hybrid', txNorm, ty, zoom);
      tiles.push({ key: `${zoom}-${txNorm}-${ty}-${layer}`, left, top, url });
    }
  }

  // Calculate geofence circle diameter in pixels
  const metersPerPixel = (156543.03392 * Math.cos((latitude * Math.PI) / 180)) / Math.pow(2, zoom);
  const radiusPx = radiusMeters / metersPerPixel;
  const diameterPx = Math.max(20, Math.round(radiusPx * 2));

  return (
    <div className="geofence-map-picker-card">
      {/* Top Map Sub-toolbar */}
      <div className="map-picker-subnav">
        <div className="map-instruction">
          <span className="cursor-hand" aria-hidden="true">
            👆
          </span>
          <span>Click map or drag the pin to set boundary</span>
        </div>
        <div className="map-actions-group">
          <button
            type="button"
            className="btn-map-gps"
            onClick={handleMyGps}
            disabled={locating}
            title="Use current GPS location"
          >
            <Crosshair size={14} className={locating ? 'spin' : ''} />
            <span>{locating ? 'Locating...' : 'My GPS'}</span>
          </button>
          <div className="layer-segmented-switch" role="group" aria-label="Map layers">
            <button
              type="button"
              className={`layer-switch-btn ${layer === 'roads' ? 'active' : ''}`}
              onClick={() => setLayer('roads')}
            >
              Roads
            </button>
            <button
              type="button"
              className={`layer-switch-btn ${layer === 'satellite' ? 'active' : ''}`}
              onClick={() => setLayer('satellite')}
            >
              Satellite
            </button>
          </div>
        </div>
      </div>

      {/* Interactive Slippy Map Canvas */}
      <div
        ref={containerRef}
        className="map-interactive-viewport"
        onMouseDown={handleMouseDown}
        onMouseMove={handleMouseMove}
        onMouseUp={handleMouseUp}
        onMouseLeave={handleMouseUp}
        style={{ cursor: dragRef.current ? 'grabbing' : 'grab' }}
      >
        {/* Render Tile Images */}
        {tiles.map((tile) => (
          <img
            key={tile.key}
            src={tile.url}
            alt=""
            draggable={false}
            className="map-tile-image"
            style={{
              position: 'absolute',
              left: `${tile.left}px`,
              top: `${tile.top}px`,
              width: '256px',
              height: '256px',
              userSelect: 'none',
              pointerEvents: 'none',
            }}
            onError={(e) => {
              // Retry loading Google tile once if temporary network issue
              const target = e.currentTarget;
              if (!target.dataset.retried) {
                target.dataset.retried = 'true';
                target.src = tile.url;
              }
            }}
          />
        ))}

        {/* Dynamic Circular Geofence Radius Boundary (Centered on Coordinate Pin) */}
        <div
          className="map-geofence-radius-circle"
          style={{
            width: `${diameterPx}px`,
            height: `${diameterPx}px`,
          }}
          aria-hidden="true"
        >
          <span className="radius-tag-bubble">{radiusMeters}m</span>
        </div>

        {/* Center Pin Marker (Building Pin icon) */}
        <div className="map-center-pin-wrapper" aria-hidden="true">
          <div className="pin-shape">
            <Building2 size={16} className="pin-icon" />
          </div>
          <div className="pin-ground-shadow" />
        </div>

        {/* Zoom In / Zoom Out Controls */}
        <div className="map-zoom-controls" role="group" aria-label="Map Zoom">
          <button
            type="button"
            className="zoom-btn"
            onClick={(e) => {
              e.stopPropagation();
              setZoom((z) => Math.min(19, z + 1));
            }}
            disabled={zoom >= 19}
            title="Zoom In"
            aria-label="Zoom In"
          >
            <Plus size={16} />
          </button>
          <div className="zoom-btn-divider" />
          <button
            type="button"
            className="zoom-btn"
            onClick={(e) => {
              e.stopPropagation();
              setZoom((z) => Math.max(12, z - 1));
            }}
            disabled={zoom <= 12}
            title="Zoom Out"
            aria-label="Zoom Out"
          >
            <Minus size={16} />
          </button>
        </div>

        {/* Map Attribution Tag */}
        <div className="map-attribution-badge">© Google Maps</div>
      </div>

      {/* Radius Presets and Quick Controls */}
      {onRadiusChange && (
        <div className="map-radius-preset-bar">
          <div className="preset-label-group">
            <span className="preset-title">Boundary Radius:</span>
            <div className="preset-chips">
              {[50, 100, 150, 250, 500].map((preset) => (
                <button
                  key={preset}
                  type="button"
                  className={`preset-chip ${radiusMeters === preset ? 'active' : ''}`}
                  onClick={() => onRadiusChange(preset)}
                >
                  {preset}m
                </button>
              ))}
            </div>
          </div>
          <div className="radius-slider-group">
            <input
              type="range"
              min={30}
              max={1000}
              step={10}
              value={radiusMeters}
              onChange={(e) => onRadiusChange(Number(e.target.value))}
              className="radius-range-slider"
            />
            <span className="radius-numeric-display">{radiusMeters} meters</span>
          </div>
        </div>
      )}
    </div>
  );
}
