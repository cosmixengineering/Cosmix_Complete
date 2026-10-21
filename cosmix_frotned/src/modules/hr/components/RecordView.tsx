import type { ReactNode } from 'react';
const humanLabel = (key: string) =>
  key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, (c) => c.toUpperCase());
function display(value: unknown): ReactNode {
  if (value === null || value === undefined || value === '')
    return <span className="muted">Not set</span>;
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (typeof value === 'object')
    return Array.isArray(value) ? (
      value.length ? (
        <ol>
          {value.map((v, i) => (
            <li key={i}>{display(v)}</li>
          ))}
        </ol>
      ) : (
        'None'
      )
    ) : (
      <RecordView value={value as Record<string, unknown>} />
    );
  return String(value);
}
export function RecordView({
  value,
  omit = [],
}: {
  value: Record<string, unknown>;
  omit?: string[];
}) {
  return (
    <dl className="hr-dl">
      {Object.entries(value)
        .filter(([key]) => !omit.includes(key))
        .map(([key, item]) => (
          <div key={key}>
            <dt>{humanLabel(key)}</dt>
            <dd>{display(item)}</dd>
          </div>
        ))}
    </dl>
  );
}
