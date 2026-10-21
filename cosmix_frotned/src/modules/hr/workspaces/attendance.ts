import type {
  PreparedWorkspaceDefinition,
  PreparedField,
  PreparedTab,
  PreparedSection,
} from '../types/prepared-workspace';

const field = (
  id: string,
  label: string,
  kind: PreparedField['kind'],
  extra: Partial<PreparedField> = {},
): PreparedField => ({ id, label, kind, ...extra });
const employee = (): PreparedField =>
  field('employeeId', 'Employee', 'reference', { reference: 'employees', required: true });
const site = (): PreparedField => field('siteId', 'Site', 'reference', { reference: 'sites' });
const section = (
  id: string,
  title: string,
  fields: readonly PreparedField[],
  repeatable = false,
): PreparedSection => ({ id, title, fields, repeatable });
const period = (): PreparedField[] => [
  field('effectiveFrom', 'Effective from', 'date', { required: true }),
  field('effectiveTo', 'Effective to', 'date'),
];
const reason = (): PreparedField => field('reason', 'Reason', 'textarea', { required: true });
const evidence = (): PreparedField => field('evidence', 'Supporting evidence', 'file');
const dateFilter = [
  field('date', 'Attendance date', 'date'),
  site(),
  field('departmentId', 'Department', 'reference', { reference: 'departments' }),
];
const decision = section('decision', 'Review decision', [
  field('decision', 'Decision', 'select', {
    required: true,
    options: [
      { value: 'approve', label: 'Approve' },
      { value: 'reject', label: 'Reject' },
    ],
  }),
  field('comment', 'Decision comment', 'textarea', { required: true }),
]);
const reviewAudit = [
  'Request status',
  'Requester',
  'Configured approving authority',
  'Reviewer',
  'Decision time (server)',
  'Before and after values',
  'Audit reference',
];

const shifts: PreparedTab[] = [
  {
    id: 'definitions',
    label: 'Shift definitions',
    columns: [
      'Shift code',
      'Shift name',
      'Start time',
      'End time',
      'Effective from',
      'Effective to',
    ],
    sections: [
      section('shift', 'Shift', [
        field('code', 'Shift code', 'text', { required: true }),
        field('name', 'Shift name', 'text', { required: true }),
        field('startTime', 'Start time', 'time', { required: true }),
        field('endTime', 'End time', 'time', { required: true }),
        field('crossMidnight', 'Ends on the following day', 'checkbox'),
        field('committedHours', 'Daily committed hours', 'number', {
          required: true,
          min: 0,
          step: 'any',
        }),
        field('policyVersionId', 'Applicable policy version ID', 'uuid', { required: true }),
        ...period(),
      ]),
      section(
        'days',
        'Applicable days',
        [
          field('day', 'Day', 'select', {
            required: true,
            options: [
              'Monday',
              'Tuesday',
              'Wednesday',
              'Thursday',
              'Friday',
              'Saturday',
              'Sunday',
            ].map((value) => ({ value, label: value })),
          }),
        ],
        true,
      ),
      section(
        'breaks',
        'Breaks',
        [
          field('name', 'Break name', 'text'),
          field('startTime', 'Start time', 'time'),
          field('endTime', 'End time', 'time'),
          field('durationMinutes', 'Duration in minutes', 'number', { min: 0 }),
          field('paid', 'Paid break', 'checkbox'),
        ],
        true,
      ),
    ],
    serverFields: [
      'Shift ID',
      'Version',
      'Applicable late / half-day / hourly thresholds',
      'Early-reporting rule',
      'Overtime eligibility',
      'Approval status',
      'History',
    ],
    dateOrders: [{ section: 'shift', start: 'effectiveFrom', end: 'effectiveTo' }],
    timeOrders: [
      { section: 'shift', start: 'startTime', end: 'endTime', nextDayField: 'crossMidnight' },
    ],
    actionLabel: 'Prepare shift version',
  },
  {
    id: 'assignments',
    label: 'Population assignments',
    columns: ['Shift', 'Population', 'Effective from', 'Effective to', 'Approval status'],
    sections: [
      section(
        'assignments',
        'Shift assignments',
        [
          field('shiftId', 'Shift ID', 'uuid', { required: true }),
          field('population', 'Population', 'select', {
            required: true,
            options: ['employee', 'team', 'site', 'staffCategory', 'department'].map((value) => ({
              value,
              label: value,
            })),
          }),
          field('employeeId', 'Employee', 'reference', {
            reference: 'employees',
            required: true,
            when: { field: 'population', values: ['employee'] },
          }),
          field('teamId', 'Team ID', 'uuid', {
            required: true,
            when: { field: 'population', values: ['team'] },
          }),
          field('siteId', 'Site', 'reference', {
            reference: 'sites',
            required: true,
            when: { field: 'population', values: ['site'] },
          }),
          field('staffCategory', 'Approved staff category', 'text', {
            required: true,
            when: { field: 'population', values: ['staffCategory'] },
          }),
          field('departmentId', 'Department', 'reference', {
            reference: 'departments',
            required: true,
            when: { field: 'population', values: ['department'] },
          }),
          ...period(),
        ],
        true,
      ),
    ],
    dateOrders: [{ section: 'assignments', start: 'effectiveFrom', end: 'effectiveTo' }],
    serverFields: ['Effective shift resolution', 'Approval status', 'Assignment history'],
    actionLabel: 'Prepare assignments',
  },
  {
    id: 'roster',
    label: 'Employee roster',
    presentation: 'roster',
    columns: ['Employee', 'Date', 'Site', 'Shift', 'Start time', 'End time', 'Policy version'],
    filters: dateFilter,
    sections: [],
    readOnly: true,
    serverFields: [
      'Effective employee / team / site assignment',
      'Scheduled hours',
      'Breaks',
      'Temporary changes',
      'Applicable policy version',
    ],
  },
  {
    id: 'temporary-changes',
    label: 'Temporary changes',
    columns: ['Site', 'Date', 'Requested opening time', 'Approval status'],
    sections: [
      section('change', 'Temporary site opening', [
        { ...site(), required: true },
        field('date', 'Change date', 'date', { required: true }),
        field('openingTime', 'Requested opening time', 'time', { required: true }),
        field('shiftId', 'Affected shift ID', 'uuid', { required: true }),
        field('approvingAuthorityId', 'Approving authority', 'reference', {
          reference: 'employees',
          required: true,
        }),
        reason(),
        { ...evidence(), required: true },
      ]),
      section('employees', 'Affected employees', [employee()], true),
    ],
    serverFields: ['Written approval validation', 'Affected schedule', ...reviewAudit],
    actionLabel: 'Prepare temporary change',
  },
];

const face: PreparedTab[] = [
  {
    id: 'enrollments',
    label: 'Enrollment administration',
    columns: [
      'Employee',
      'Enrollment status',
      'Enrollment date',
      'Authorized enroller',
      'Revoked date',
    ],
    sections: [
      section('request', 'Enrollment administration request', [
        employee(),
        field('operation', 'Operation', 'select', {
          required: true,
          options: [
            { value: 'enrollment', label: 'Request authorized enrollment' },
            { value: 're-enrollment', label: 'Request re-enrollment' },
            { value: 'reset', label: 'Request reset' },
            { value: 'revoke', label: 'Request revocation' },
          ],
        }),
        reason(),
      ]),
    ],
    serverFields: [
      'Enrollment status',
      'Authorized enroller',
      'Enrollment time (server)',
      'Revocation time (server)',
      'Provider / version',
      'Re-enrollment history',
      'Audit reference',
    ],
    actionLabel: 'Prepare enrollment administration request',
  },
  {
    id: 'attempts',
    label: 'Verification attempts',
    columns: ['Employee', 'Check-In attempt', 'Result', 'Device', 'Server time', 'Correlation ID'],
    filters: dateFilter,
    sections: [],
    readOnly: true,
    serverFields: [
      'Liveness result',
      'Match result',
      'Failure category',
      'Provider / version',
      'Device',
      'Server time',
      'Audit reference',
    ],
  },
  {
    id: 'exceptions',
    label: 'Alternative Check-In',
    columns: ['Employee', 'Effective from', 'Effective to', 'Approval status'],
    sections: [
      section('exception', 'Alternative Check-In request', [
        employee(),
        field('instructionReference', 'Approved alternative process reference', 'text', {
          required: true,
        }),
        ...period(),
      ]),
    ],
    dateOrders: [{ section: 'exception', start: 'effectiveFrom', end: 'effectiveTo' }],
    serverFields: [
      'Approved alternative process',
      'Approval status',
      'Approving authority',
      'Effective exception',
      'Audit reference',
    ],
    actionLabel: 'Prepare alternative-process request',
  },
];

const attendance: PreparedTab[] = [
  {
    id: 'daily',
    label: 'Daily attendance',
    columns: [
      'Employee',
      'Date',
      'Site',
      'Status',
      'Scheduled hours',
      'Worked hours',
      'Payable hours',
      'Late minutes',
      'Overtime',
      'Exceptions',
    ],
    filters: dateFilter,
    sections: [],
    readOnly: true,
    serverFields: [
      'Calculation version',
      'Policy version',
      'Source',
      'Lock status',
      'Generation time (server)',
    ],
  },
  {
    id: 'monthly',
    presentation: 'register',
    label: 'Monthly register',
    columns: [
      'Employee',
      'Month',
      'Daily statuses',
      'Worked hours',
      'Payable hours',
      'Status totals',
      'Lock status',
    ],
    filters: [field('month', 'Attendance month', 'month'), site()],
    sections: [],
    readOnly: true,
    serverFields: [
      'Employee-by-day matrix',
      'Applied filters',
      'Policy period',
      'Generation time (server)',
      'Calculation version',
    ],
  },
  {
    id: 'punches',
    label: 'Raw punch audit',
    columns: [
      'Employee',
      'Event type',
      'Source',
      'Server time',
      'Device time',
      'Site',
      'Verification status',
    ],
    filters: dateFilter,
    sections: [],
    readOnly: true,
    serverFields: [
      'Immutable event ID',
      'Coordinates (authorized scope)',
      'GPS accuracy',
      'Device',
      'Online / offline state',
      'Check-In verification result',
      'Duplicate / replay detection',
      'Receipt time (server)',
      'Evidence metadata',
      'Exception trail',
    ],
  },
  {
    id: 'detail',
    label: 'Summary detail',
    columns: ['Employee', 'Attendance date', 'Summary status', 'Calculation version'],
    filters: dateFilter,
    sections: [],
    readOnly: true,
    serverFields: [
      'Original punches',
      'Work sessions including cross-midnight sessions',
      'Applicable shift / roster',
      'Effective policy',
      'Scheduled hours',
      'Worked hours',
      'Payable hours',
      'Late minutes',
      'Overtime',
      'Policy effects',
      'Corrections and before / after values',
      'Recalculation history',
      'Lock status',
    ],
  },
  {
    id: 'exceptions',
    label: 'Exceptions',
    columns: ['Employee', 'Date', 'Exception type', 'Site', 'Review status'],
    filters: dateFilter,
    sections: [
      section('review', 'Exception review', [
        field('exceptionId', 'Exception ID', 'uuid', { required: true }),
        field('comment', 'Review comment', 'textarea', { required: true }),
      ]),
    ],
    serverFields: [
      'Missing Check Out',
      'Overlapping shifts',
      'Impossible travel',
      'Excessive duration',
      'Sequence errors',
      'Out-of-geofence distance',
      'GPS accuracy',
      'Unverified offline evidence',
      'Original punch references',
      'Review history',
    ],
    actionLabel: 'Prepare exception review',
  },
  {
    id: 'corrections',
    label: 'Correction requests',
    columns: ['Employee', 'Date', 'Request status', 'Requester', 'Reviewer'],
    sections: [
      section('correction', 'Requested interpretation', [
        employee(),
        field('attendanceDate', 'Attendance date', 'date', { required: true }),
        field('summaryId', 'Daily summary ID', 'uuid'),
        field('requestedStatus', 'Requested attendance status', 'text'),
        reason(),
        evidence(),
      ]),
      section(
        'times',
        'Requested times / manual entries',
        [
          field('originalPunchId', 'Original punch ID (where available)', 'uuid'),
          field('eventType', 'Event type', 'select', {
            required: true,
            options: [
              { value: 'check-in', label: 'Check In' },
              { value: 'check-out', label: 'Check Out' },
            ],
          }),
          field('requestedTime', 'Requested event date and time', 'datetime-local', {
            required: true,
          }),
        ],
        true,
      ),
    ],
    serverFields: [
      'Original values',
      'Requested values',
      'Applicable shift / policy version',
      'Lock status',
      'Reopen / next-period adjustment requirement',
      'Recalculated result',
      ...reviewAudit,
    ],
    actionLabel: 'Prepare correction request',
  },
  {
    id: 'correction-review',
    label: 'Correction decisions',
    columns: ['Employee', 'Attendance date', 'Request status', 'Requester'],
    sections: [
      section('request', 'Correction reference', [
        field('correctionId', 'Correction request ID', 'uuid', { required: true }),
      ]),
      decision,
    ],
    serverFields: [
      'Source punches',
      'Assignment / shift',
      'Evidence metadata',
      'Original / requested values',
      'Policy version',
      'Calculation impact',
      'Lock status',
      ...reviewAudit,
    ],
    actionLabel: 'Prepare correction decision',
  },
  {
    id: 'import-proposal',
    label: 'Import proposal',
    columns: ['Proposal reference', 'Source description', 'Review status'],
    sections: [
      section('proposal', 'Source proposal', [
        field('sourceDescription', 'Source description', 'text', { required: true }),
        field('fileReference', 'Source file reference', 'text'),
        reason(),
      ]),
      section(
        'mapping',
        'Proposed field mapping',
        [
          field('sourceColumn', 'Source column', 'text', { required: true }),
          field('targetField', 'Attendance field', 'text', { required: true }),
        ],
        true,
      ),
    ],
    serverFields: [
      'Source file hash',
      'Importer',
      'Import batch reference',
      'Duplicate report',
      'Validation errors',
      'Review status',
    ],
    actionLabel: 'Prepare import proposal',
  },
];

const overtime: PreparedTab[] = [
  {
    id: 'requests',
    label: 'Overtime requests',
    columns: ['Employee', 'Site / job', 'Start', 'End', 'Request status'],
    sections: [
      section('request', 'Overtime work', [
        employee(),
        site(),
        field('jobReference', 'Job reference', 'text'),
        field('start', 'Work start', 'datetime-local', { required: true }),
        field('end', 'Work end', 'datetime-local', { required: true }),
        reason(),
        evidence(),
      ]),
    ],
    dateOrders: [{ section: 'request', start: 'start', end: 'end' }],
    serverFields: [
      'Scheduled work completion',
      'Category eligibility',
      'Total minutes',
      'Rate rule / version',
      'Travel treatment',
      ...reviewAudit,
    ],
    actionLabel: 'Prepare overtime request',
  },
  {
    id: 'bulk-review',
    label: 'Explicit bulk selection',
    columns: ['Employee', 'Request ID', 'Requested duration', 'Review status'],
    sections: [
      section(
        'selection',
        'Explicitly selected requests',
        [
          employee(),
          field('requestId', 'Overtime request ID', 'uuid', { required: true }),
          field('selected', 'Include this request in the decision', 'checkbox', { required: true }),
        ],
        true,
      ),
      decision,
    ],
    serverFields: [
      'Selected employee / request validation',
      'Eligibility per selected request',
      'Approved minutes per request',
      'Rate rule per request',
      'Payable impact per request',
      ...reviewAudit,
    ],
    actionLabel: 'Prepare selected-request decision',
  },
  {
    id: 'payable-summary',
    label: 'Payable summary',
    columns: [
      'Employee',
      'Month',
      'Site / job',
      'Requested minutes',
      'Approved minutes',
      'Payable minutes',
      'Rejected minutes',
      'Payable amount',
    ],
    filters: [field('month', 'Payroll month', 'month'), site()],
    sections: [],
    readOnly: true,
    serverFields: [
      'Approved rate rule / version',
      'Calculation details',
      'Approval trace',
      'Payroll handoff status',
      'Lock status',
      'Generation time (server)',
    ],
  },
];

// Display definitions only. Server authorities resolve scope, policy and decisions.
export const attendanceWorkspaces: readonly PreparedWorkspaceDefinition[] = [
  {
    id: 'shifts-rosters',
    title: 'Shifts and Rosters',
    srs: ['6.2', 'SHIFT-001..004', 'MD-012'],
    tabs: shifts,
  },
  {
    id: 'face-enrollment',
    title: 'Face Enrollment Administration',
    srs: ['HR-008', '7.2.1', 'FACE-001..008', 'MD-002', 'MD-004', 'MD-013'],
    tabs: face,
  },
  {
    id: 'attendance-review',
    title: 'Attendance Review',
    srs: ['7.1..7.3', 'ATT-001..011', '12', '15', 'MD-003', 'MD-005'],
    tabs: attendance,
  },
  {
    id: 'overtime',
    title: 'Overtime',
    srs: ['8.3', 'OT-001..005', '12', '15', 'MD-007'],
    tabs: overtime,
  },
];
