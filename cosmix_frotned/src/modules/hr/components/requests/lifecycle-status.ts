import type { StatusState } from '../../types/requests';

export function isFutureStatusTime(value: string | null) {
  return value === null || (Number.isFinite(Date.parse(value)) && Date.parse(value) > Date.now());
}

export function statusRequestChoices(current: StatusState) {
  if (current.isArchived || current.scheduledChange) return [];
  const transitions =
    current.currentStatus === 2 ? [3, 4, 5, 6] : current.currentStatus === 3 ? [2, 4, 5, 6] : [];
  const labels: Record<number, string> = {
    2: 'Active (suspension release)',
    3: 'Suspended',
    4: 'Resigned',
    5: 'Terminated',
    6: 'Retired',
  };
  return transitions.map((status) => ({ value: String(status), label: labels[status]! }));
}
