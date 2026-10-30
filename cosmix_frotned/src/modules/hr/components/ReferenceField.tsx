import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { employeeSearchTerm, formatEmployeeCode } from '@/utils/employee-code';
import { useState } from 'react';
import { SelectInput } from '@/components/forms/SelectInput';
import { TextInput } from '@/components/forms/TextInput';
import { Button } from '@/components/ui/Button';
import { useDebouncedValue } from '@/hooks/use-debounced-value';
import { useHrQuery } from '../hooks/use-hr-queries';
import { useHrAccess } from '../hooks/use-hr-access';
import { pageOf, employeeSchema } from '../schemas/contracts';
import { masterSchema } from '../schemas/organization';
import { queryString } from '../api/hr-api';
import { errorMessage } from '../utils/messages';

export function ReferenceField({
  label,
  kind,
  value,
  onChange,
  required = false,
  disabled = false,
  currentLabel,
  excludeId,
  onRecord,
  error,
  enableFind = false,
  dataField,
}: {
  label: string;
  kind: string;
  value: string | null;
  onChange: (id: string | null) => void;
  required?: boolean;
  disabled?: boolean;
  currentLabel?: string;
  excludeId?: string;
  onRecord?: (row: { id: string; code: string; name: string } | undefined) => void;
  error?: string;
  enableFind?: boolean;
  dataField?: string;
}) {
  const access = useHrAccess();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const term = useDebouncedValue(search);
  const employee = kind === 'employees';
  const permission = employee
    ? 'hr.employees.read'
    : kind === 'sites'
      ? 'hr.sites.read'
      : 'hr.organization.read';
  const query = useHrQuery(
    kind +
      queryString({
        page,
        pageSize: 100,
        search: employee ? employeeSearchTerm(term) : term,
        ...(employee ? { status: 2 } : { isActive: true }),
      }),
    employee ? pageOf(employeeSchema) : pageOf(masterSchema),
    [permission],
  );
  const rows = (query.data?.items ?? []).filter(
    (r) => r.id !== excludeId && ('status' in r ? r.status === 2 : true),
  );
  const options = [
    { value: '', label: 'Select ' + label.toLowerCase() },
    ...rows.map((r) => ({
      value: r.id,
      label:
        'employeeNumber' in r
          ? `${formatEmployeeCode(r.employeeNumber)} · ${r.name ?? 'Unnamed employee'}`
          : (r.name ?? 'Unnamed record'),
    })),
  ];
  if (value && !rows.some((r) => r.id === value))
    options.push({
      value,
      label: employee ? 'Current employee selection' : (currentLabel ?? 'Current selection'),
    });
  return (
    <div className="hr-reference" data-field={dataField}>
      <SelectInput
        label={label}
        required={required}
        error={error}
        data-field={dataField}
        disabled={disabled || !access.can(permission)}
        value={value ?? ''}
        options={options}
        onChange={(e) => {
          onChange(e.target.value || null);
          const row = rows.find((r) => r.id === e.target.value);
          onRecord?.(
            row && 'code' in row ? { id: row.id, code: row.code, name: row.name } : undefined,
          );
        }}
      />
      {employee && value && <EmployeeReference employeeId={value} showName />}
      {(enableFind || employee) && !disabled && access.can(permission) && (
        <details>
          <summary>Find {label.toLowerCase()}</summary>
          <TextInput
            label={`Search ${label.toLowerCase()}`}
            value={search}
            maxLength={100}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
          {query.isPending && <small role="status">Loading options...</small>}
          {query.isError && <p role="alert">{errorMessage(query.error)}</p>}
          <div className="hr-inline">
            <Button variant="ghost" disabled={page === 1} onClick={() => setPage(page - 1)}>
              Previous
            </Button>
            <span>Page {page}</span>
            <Button
              variant="ghost"
              disabled={!query.data || page * 100 >= query.data.totalCount}
              onClick={() => setPage(page + 1)}
            >
              Next
            </Button>
          </div>
        </details>
      )}
      {!access.can(permission) && <small className="muted">Reference access is restricted.</small>}
    </div>
  );
}
