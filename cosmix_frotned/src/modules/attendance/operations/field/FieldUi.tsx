import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { useState, type ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import { ArrowRight, RefreshCw } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { Command } from '../../components/SchedulingUi';
import { FieldWorkspace } from './FieldWorkspace';
import { id } from '../../schemas/common';

export function SiteWorkspace({ children }: { children: (siteId: string) => ReactNode }) {
  const params = useParams();
  const [input, setInput] = useState(''),
    [selected, setSelected] = useState('');
  const siteId = params.siteId ?? selected;
  if (!id.safeParse(siteId).success)
    return (
      <FieldWorkspace>
        <form
          className="attendance-commands"
          onSubmit={(e) => {
            e.preventDefault();
            if (id.safeParse(input).success) setSelected(input);
          }}
        >
          <TextInput
            label="Site ID"
            required
            value={input}
            onChange={(e) => setInput(e.target.value)}
          />
          <Command icon={ArrowRight} type="submit" disabled={!id.safeParse(input).success}>
            Open site
          </Command>
        </form>
        {params.siteId && <p role="alert">Invalid site identifier.</p>}
      </FieldWorkspace>
    );
  return <FieldWorkspace resource={siteId}>{children(siteId)}</FieldWorkspace>;
}

export function Reload({ onClick, disabled = false }: { onClick: () => void; disabled?: boolean }) {
  return (
    <Command icon={RefreshCw} variant="secondary" disabled={disabled} onClick={onClick}>
      Reload
    </Command>
  );
}

export function RecordFields({
  values,
}: {
  values: Record<string, string | number | boolean | null>;
}) {
  return (
    <dl className="attendance-record">
      {Object.entries(values).map(([key, value]) => (
        <div key={key} style={{ display: 'contents' }}>
          <dt>{key}</dt>
          <dd>
            {key === 'Employee' && typeof value === 'string' ? (
              <EmployeeReference employeeId={value} />
            ) : value === null ? (
              'Not supplied'
            ) : (
              String(value)
            )}
          </dd>
        </div>
      ))}
    </dl>
  );
}
