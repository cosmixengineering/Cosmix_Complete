import { SelectInput } from '@/components/forms/SelectInput';
import type { EmployeeArchiveFilterValue } from './employee-archive-directory';

export function EmployeeArchiveFilter({
  value,
  onChange,
}: {
  value: EmployeeArchiveFilterValue;
  onChange: (value: EmployeeArchiveFilterValue) => void;
}) {
  return (
    <SelectInput
      label="Directory status"
      value={value}
      options={[
        { value: 'current', label: 'Current records' },
        { value: 'all', label: 'All records' },
        { value: 'archived', label: 'Archived records' },
      ]}
      onChange={(event) => {
        const next = event.target.value;
        if (next === 'current' || next === 'all' || next === 'archived') onChange(next);
      }}
    />
  );
}
