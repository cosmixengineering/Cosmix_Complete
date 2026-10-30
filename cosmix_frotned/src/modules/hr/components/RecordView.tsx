import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { formatEmployeeCode } from '@/utils/employee-code';
import type { ReactNode } from 'react';
const humanLabel = (key: string) =>
  key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, (c) => c.toUpperCase());
function display(value: unknown, key = ''): ReactNode {
  if (value === null || value === undefined || value === '')
    return <span className="muted">Not set</span>;
  if (typeof value === 'string' && (/employeeId$/i.test(key) || key === 'employeeReference'))
    return <EmployeeReference employeeId={value} />;
  if (
    (typeof value === 'string' || typeof value === 'number') &&
    /(?:employeeNumber|employeeCode)$/i.test(key)
  )
    return formatEmployeeCode(value);
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (typeof value === 'object')
    return Array.isArray(value) ? (
      value.length ? (
        <ol>
          {value.map((v, i) => (
            <li key={i}>{display(v, /employeeIds$/i.test(key) ? 'employeeId' : key)}</li>
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
            <dd>{display(item, key)}</dd>
          </div>
        ))}
    </dl>
  );
}
