export type PreparedFieldKind =
  | 'text'
  | 'textarea'
  | 'number'
  | 'date'
  | 'time'
  | 'datetime-local'
  | 'month'
  | 'select'
  | 'checkbox'
  | 'reference'
  | 'uuid'
  | 'file'
  | 'geojson';

export interface PreparedField {
  id: string;
  label: string;
  kind: PreparedFieldKind;
  required?: boolean;
  min?: number;
  minExclusive?: boolean;
  max?: number;
  step?: string;
  maxLength?: number;
  options?: readonly { value: string; label: string }[];
  reference?: 'employees' | 'departments' | 'designations' | 'branches' | 'cities' | 'sites';
  when?: { field: string; values: readonly string[] };
}

export interface PreparedSection {
  id: string;
  title: string;
  repeatable?: boolean;
  fields: readonly PreparedField[];
}

export interface PreparedTab {
  id: string;
  label: string;
  columns: readonly string[];
  filters?: readonly PreparedField[];
  sections: readonly PreparedSection[];
  serverFields?: readonly string[];
  actionLabel?: string;
  readOnly?: boolean;
  presentation?: 'calendar' | 'roster' | 'register';
  dateOrders?: readonly { section: string; start: string; end: string }[];
  timeOrders?: readonly { section: string; start: string; end: string; nextDayField?: string }[];
}

// SRS view definitions for services not delivered yet; these are not invented API DTOs.
export interface PreparedWorkspaceDefinition {
  id: string;
  title: string;
  srs: readonly string[];
  permissions?: readonly string[];
  tabs: readonly PreparedTab[];
}

export type PreparedValue = string | boolean | File | null;
export type PreparedRow = Record<string, PreparedValue>;
export type PreparedDraft = Record<string, { key: string; values: PreparedRow }[]>;
