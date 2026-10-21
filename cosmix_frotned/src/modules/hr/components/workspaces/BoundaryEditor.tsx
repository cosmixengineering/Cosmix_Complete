import { useEffect, useRef, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { LongField } from '../hiring/FormFields';
import { addVertex, readPolygon, removeVertex, type BoundaryPolygon } from '../../utils/boundary';

function PolygonPreview({ polygon }: { polygon: BoundaryPolygon }) {
  const canvas = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    const context = canvas.current?.getContext('2d');
    if (!context) return;
    const points = polygon.coordinates.flat();
    const xs = points.map((point) => point[0]),
      ys = points.map((point) => point[1]);
    const minX = Math.min(...xs),
      maxX = Math.max(...xs),
      minY = Math.min(...ys),
      maxY = Math.max(...ys);
    const dx = Math.max(maxX - minX, 0.000001),
      dy = Math.max(maxY - minY, 0.000001);
    const scale = Math.min(620 / dx, 250 / dy);
    const project = ([x, y]: [number, number]) => [
      360 + (x - (minX + maxX) / 2) * scale,
      155 - (y - (minY + maxY) / 2) * scale,
    ];
    context.clearRect(0, 0, 720, 340);
    context.fillStyle = '#f7f9fa';
    context.fillRect(0, 0, 720, 340);
    context.strokeStyle = '#e1e7eb';
    context.lineWidth = 1;
    for (let x = 30; x < 720; x += 60) {
      context.beginPath();
      context.moveTo(x, 0);
      context.lineTo(x, 310);
      context.stroke();
    }
    for (let y = 30; y < 310; y += 50) {
      context.beginPath();
      context.moveTo(0, y);
      context.lineTo(720, y);
      context.stroke();
    }
    context.beginPath();
    for (const ring of polygon.coordinates) {
      ring.forEach((point, index) => {
        const [x, y] = project(point);
        if (index) context.lineTo(x!, y!);
        else context.moveTo(x!, y!);
      });
      context.closePath();
    }
    context.fillStyle = '#26776d20';
    context.fill('evenodd');
    context.strokeStyle = '#26776d';
    context.lineWidth = 2;
    context.stroke();
    polygon.coordinates[0]!.slice(0, -1).forEach((point, index) => {
      const [x, y] = project(point);
      context.beginPath();
      context.arc(x!, y!, 4, 0, Math.PI * 2);
      context.fillStyle = '#26776d';
      context.fill();
      context.fillStyle = '#263544';
      context.font = '12px sans-serif';
      context.fillText(String(index + 1), x! + 7, y! - 7);
    });
    context.fillStyle = '#556878';
    context.font = '12px sans-serif';
    context.fillText(`Longitude ${minX.toFixed(6)} to ${maxX.toFixed(6)}`, 22, 328);
    context.fillText(`Latitude ${minY.toFixed(6)} to ${maxY.toFixed(6)}`, 370, 328);
  }, [polygon]);
  return (
    <canvas
      ref={canvas}
      width={720}
      height={340}
      className="hr-boundary-canvas"
      role="img"
      aria-label="Draft polygon longitude and latitude outline; geographic basemap unavailable"
    />
  );
}

export function BoundaryEditor({
  label,
  value,
  required,
  onChange,
}: {
  label: string;
  value: string;
  required?: boolean;
  onChange: (value: string) => void;
}) {
  const polygon = readPolygon(value);
  const [longitude, setLongitude] = useState('');
  const [latitude, setLatitude] = useState('');
  const [error, setError] = useState('');
  function append() {
    const point: [number, number] = [Number(longitude), Number(latitude)];
    if (
      !polygon ||
      !longitude.trim() ||
      !latitude.trim() ||
      !point.every(Number.isFinite) ||
      point[0] < -180 ||
      point[0] > 180 ||
      point[1] < -90 ||
      point[1] > 90
    ) {
      setError('Enter longitude from -180 to 180 and latitude from -90 to 90.');
      return;
    }
    const text = JSON.stringify(addVertex(polygon, point));
    if (text.length > 10000) {
      setError('Boundary exceeds the draft size limit.');
      return;
    }
    onChange(text);
    setLongitude('');
    setLatitude('');
    setError('');
  }
  return (
    <div className="hr-boundary-editor">
      <LongField
        label={label}
        value={value || null}
        required={required}
        max={10000}
        onChange={(next) => {
          onChange(next ?? '');
          setError('');
        }}
      />
      <p className="muted">
        Geographic basemap unavailable. This coordinate outline is not a server-validated geofence.
      </p>
      {polygon ? (
        <>
          <PolygonPreview polygon={polygon} />
          <div className="hr-table-scroll">
            <table className="hr-prepared-table">
              <caption className="sr-only">Outer boundary vertices</caption>
              <thead>
                <tr>
                  <th>Vertex</th>
                  <th>Longitude</th>
                  <th>Latitude</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {polygon.coordinates[0]!.slice(0, -1).map((point, index) => (
                  <tr key={index}>
                    <td>{index + 1}</td>
                    <td>{point[0]}</td>
                    <td>{point[1]}</td>
                    <td>
                      <button
                        type="button"
                        className="icon-button"
                        title={`Remove vertex ${index + 1}`}
                        aria-label={`Remove vertex ${index + 1}`}
                        disabled={polygon.coordinates[0]!.length <= 4}
                        onClick={() => onChange(JSON.stringify(removeVertex(polygon, index)))}
                      >
                        <Trash2 size={16} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="muted">Interior rings: {polygon.coordinates.length - 1}</p>
          <div className="hr-form-grid">
            <TextInput
              label="New vertex longitude"
              type="number"
              min={-180}
              max={180}
              step="any"
              value={longitude}
              onChange={(event) => setLongitude(event.target.value)}
            />
            <TextInput
              label="New vertex latitude"
              type="number"
              min={-90}
              max={90}
              step="any"
              value={latitude}
              onChange={(event) => setLatitude(event.target.value)}
            />
          </div>
          {error && (
            <p role="alert" className="hr-field-error">
              {error}
            </p>
          )}
          <Button type="button" variant="secondary" onClick={append}>
            <Plus size={16} />
            Add vertex
          </Button>
        </>
      ) : (
        <p role="status">
          {value
            ? 'A valid closed Polygon boundary is required for the outline.'
            : 'No boundary entered.'}
        </p>
      )}
    </div>
  );
}

export function CirclePreview({
  latitude,
  longitude,
  radius,
}: {
  latitude: string;
  longitude: string;
  radius: string;
}) {
  const valid =
    latitude.trim() &&
    longitude.trim() &&
    radius.trim() &&
    [latitude, longitude, radius].every((v) => Number.isFinite(Number(v))) &&
    Math.abs(Number(latitude)) <= 90 &&
    Math.abs(Number(longitude)) <= 180 &&
    Number(radius) > 0;
  return (
    <div className="hr-circle-preview">
      <h3>Circle boundary</h3>
      {valid ? (
        <>
          <div
            className="hr-circle-outline"
            role="img"
            aria-label={`Circle at latitude ${latitude}, longitude ${longitude}, radius ${radius} meters`}
          >
            <span>{radius} m</span>
          </div>
          <p>
            Centre: {latitude}, {longitude}
          </p>
        </>
      ) : (
        <p role="status">Enter a valid centre and positive radius.</p>
      )}
      <p className="muted">
        Geographic basemap unavailable. Radius and containment remain subject to server validation.
      </p>
    </div>
  );
}
