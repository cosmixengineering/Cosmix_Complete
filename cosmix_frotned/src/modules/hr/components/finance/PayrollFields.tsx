import { useId, useState } from 'react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { employeePage } from '../../schemas/contracts';
import { FinanceQueryState } from './FinanceQueryState';
export function PayrollSelect({ label, value, options, onChange, required = true }: { label: string; value: string; options: { value: string; label: string }[]; onChange(value: string): void; required?: boolean }) {
  const id = useId();
  return <div className="field"><label htmlFor={id}>{label}</label><select id={id} required={required} value={value} onChange={event => onChange(event.target.value)}><option value="">Select…</option>{options.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select></div>;
}
export function PayrollEmployeePicker({ value, onChange }: { value: string; onChange(value: string): void }) {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const employees = useHrQuery(`employees?page=${page}&pageSize=25&search=${encodeURIComponent(search)}`, employeePage, ['hr.employees.read']);
  return <div><TextInput label="Find employee" value={search} onChange={event => { setSearch(event.target.value); setPage(1); }} /><FinanceQueryState query={employees}><PayrollSelect label="Employee" value={value} onChange={onChange} options={(employees.data?.items ?? []).map(employee => ({ value: employee.id, label: `${employee.employeeNumber} · ${employee.name ?? employee.id}` }))} /><div className="hr-actions"><Button type="button" variant="secondary" disabled={page <= 1 || employees.isFetching} onClick={() => setPage(page - 1)}>Previous employees</Button><Button type="button" variant="secondary" disabled={employees.isFetching || page * 25 >= (employees.data?.totalCount ?? 0)} onClick={() => setPage(page + 1)}>Next employees</Button></div></FinanceQueryState><TextInput label="Selected employee ID" value={value} onChange={event => onChange(event.target.value)} required /></div>;
}
