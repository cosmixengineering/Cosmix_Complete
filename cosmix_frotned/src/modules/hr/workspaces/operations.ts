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
const section = (
  id: string,
  title: string,
  fields: readonly PreparedField[],
  repeatable = false,
): PreparedSection => ({ id, title, fields, repeatable });
const employee = (): PreparedField =>
  field('employeeId', 'Employee', 'reference', { reference: 'employees', required: true });
const site = (): PreparedField => field('siteId', 'Site', 'reference', { reference: 'sites' });
const reason = (): PreparedField => field('reason', 'Reason', 'textarea', { required: true });
const evidence = (): PreparedField => field('evidence', 'Supporting evidence', 'file');
const dates = (): PreparedField[] => [
  field('effectiveFrom', 'Effective from', 'date', { required: true }),
  field('effectiveTo', 'Effective to', 'date'),
];
const times = (): PreparedField[] => [
  field('effectiveFrom', 'Effective from', 'datetime-local', { required: true }),
  field('effectiveTo', 'Effective to', 'datetime-local', { required: true }),
];
const audit = [
  'Approval status',
  'Configured authority',
  'Requester',
  'Reviewer',
  'Decision time (server)',
  'Before / after values',
  'Audit reference',
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
const effectiveOrder = (id: string): NonNullable<PreparedTab['dateOrders']> => [
  { section: id, start: 'effectiveFrom', end: 'effectiveTo' },
];

const zones: PreparedTab[] = [
  {
    id: 'boundaries',
    label: 'Zone boundaries',
    columns: [
      'Zone name',
      'Site',
      'Zone type',
      'Shape',
      'Status',
      'Effective from',
      'Effective to',
    ],
    sections: [
      section('zone', 'Zone version', [
        field('name', 'Zone name', 'text', { required: true }),
        site(),
        field('typeId', 'Configured zone type ID', 'uuid', { required: true }),
        field('shape', 'Boundary shape', 'select', {
          required: true,
          options: [
            { value: 'circle', label: 'Circle' },
            { value: 'polygon', label: 'Polygon' },
          ],
        }),
        field('latitude', 'Circle centre latitude', 'number', {
          required: true,
          min: -90,
          max: 90,
          step: 'any',
          when: { field: 'shape', values: ['circle'] },
        }),
        field('longitude', 'Circle centre longitude', 'number', {
          required: true,
          min: -180,
          max: 180,
          step: 'any',
          when: { field: 'shape', values: ['circle'] },
        }),
        field('radiusMeters', 'Circle radius in meters', 'number', {
          required: true,
          min: 0,
          minExclusive: true,
          step: 'any',
          when: { field: 'shape', values: ['circle'] },
        }),
        field('boundary', 'Polygon boundary GeoJSON', 'geojson', {
          required: true,
          when: { field: 'shape', values: ['polygon'] },
        }),
        field('instructions', 'Employee-facing instruction', 'textarea', { required: true }),
        field('reasonCode', 'Approved reason code', 'text'),
        field('status', 'Configured status', 'text', { required: true }),
        ...times(),
      ]),
    ],
    dateOrders: effectiveOrder('zone'),
    serverFields: [
      'Zone ID / version',
      'Boundary validation',
      'Boundary owner',
      'Version history',
      ...audit,
    ],
    actionLabel: 'Prepare zone version',
  },
  {
    id: 'types',
    label: 'Zone types',
    columns: ['Type name', 'Color', 'Status'],
    sections: [
      section('type', 'Configured type', [
        field('name', 'Type name', 'text', { required: true }),
        field('color', 'Approved color', 'text', { required: true }),
        field('instruction', 'Default employee instruction', 'textarea'),
        field('active', 'Active', 'checkbox'),
      ]),
    ],
    serverFields: [
      'Type ID',
      'Green / Amber / Red initial type records',
      'Configured behavior',
      'Approval reference',
      'History',
    ],
    actionLabel: 'Prepare type change',
  },
  {
    id: 'assignments',
    label: 'Zone assignments',
    columns: [
      'Zone',
      'Population',
      'Employee override',
      'Effective from',
      'Effective to',
      'Approval status',
    ],
    sections: [
      section(
        'assignments',
        'Effective assignments',
        [
          field('zoneId', 'Zone ID', 'uuid', { required: true }),
          field('population', 'Population', 'select', {
            required: true,
            options: ['employee', 'role', 'team', 'site'].map((value) => ({ value, label: value })),
          }),
          field('employeeId', 'Employee', 'reference', {
            reference: 'employees',
            when: { field: 'population', values: ['employee'] },
          }),
          field('roleId', 'Approved role ID', 'uuid', {
            when: { field: 'population', values: ['role'] },
          }),
          field('teamId', 'Team ID', 'uuid', { when: { field: 'population', values: ['team'] } }),
          field('siteId', 'Site population', 'reference', {
            reference: 'sites',
            when: { field: 'population', values: ['site'] },
          }),
          field('employeeOverride', 'Explicit employee-level override', 'checkbox', {
            when: { field: 'population', values: ['employee'] },
          }),
          field('approvingAuthorityId', 'Approving authority', 'reference', {
            reference: 'employees',
            required: true,
          }),
          field('dutyAssignmentId', 'Authorized duty / field assignment ID', 'uuid'),
          ...times(),
        ],
        true,
      ),
    ],
    dateOrders: effectiveOrder('assignments'),
    serverFields: [
      'Effective assignment resolution',
      'Individual precedence',
      'Authorized monitoring window',
      'Visible notice / retention policy reference',
      ...audit,
    ],
    actionLabel: 'Prepare zone assignments',
  },
  {
    id: 'amber-exceptions',
    label: 'Amber exceptions',
    columns: ['Employee', 'Zone', 'Effective from', 'Effective to', 'Approval status'],
    sections: [
      section('exception', 'Controlled-zone access request', [
        employee(),
        field('zoneId', 'Amber zone ID', 'uuid', { required: true }),
        field('taskReference', 'Task / permission reference', 'text', { required: true }),
        ...times(),
        reason(),
        evidence(),
      ]),
    ],
    dateOrders: effectiveOrder('exception'),
    serverFields: ['Zone version', 'Assignment', 'Active permission', ...audit],
    actionLabel: 'Prepare Amber access request',
  },
  {
    id: 'events',
    label: 'Event review',
    columns: [
      'Employee',
      'Zone',
      'Event type',
      'Server time',
      'Acknowledgement status',
      'Review status',
    ],
    filters: [field('date', 'Event date', 'date'), site()],
    sections: [
      section('review', 'Event acknowledgement / review', [
        field('eventId', 'Zone event ID', 'uuid', { required: true }),
        field('acknowledged', 'Acknowledge event', 'checkbox'),
        field('disputed', 'Dispute location evidence', 'checkbox'),
        field('comment', 'Review comment', 'textarea', { required: true }),
      ]),
    ],
    serverFields: [
      'Exact zone version',
      'Related assignment',
      'Approach / entry / exit / warning event',
      'Server time',
      'Device time',
      'Coordinates (authorized scope)',
      'Accuracy',
      'Source',
      'Device',
      'Acknowledgement history',
      'Review history',
      'Notification status',
    ],
    actionLabel: 'Prepare event review',
  },
  {
    id: 'exception-decisions',
    label: 'Access decisions',
    columns: ['Employee', 'Zone', 'Request status', 'Effective from', 'Effective to'],
    sections: [
      section('request', 'Access exception reference', [
        field('exceptionId', 'Zone exception ID', 'uuid', { required: true }),
      ]),
      decision,
    ],
    serverFields: [
      'Assignment / zone version',
      'Evidence metadata',
      'Requested access period',
      ...audit,
    ],
    actionLabel: 'Prepare access decision',
  },
];

const siteReports: PreparedTab[] = [
  {
    id: 'queue',
    label: 'Report queue',
    columns: ['Employee', 'Site', 'Report type', 'Server time', 'Sync status', 'Review status'],
    filters: [field('date', 'Report date', 'date'), site()],
    sections: [],
    readOnly: true,
    serverFields: [
      'Assigned task',
      'Arrival / departure / delay / access issue / incident type',
      'Original device time',
      'Server receipt time',
      'Pending Sync status',
      'Reviewer',
    ],
  },
  {
    id: 'review',
    label: 'Report review',
    columns: ['Report ID', 'Employee', 'Site', 'Report type', 'Review status'],
    sections: [
      section('report', 'Report reference', [
        field('reportId', 'Site report ID', 'uuid', { required: true }),
      ]),
      decision,
    ],
    serverFields: [
      'Assigned site / task',
      'Original report',
      'Arrival / departure times',
      'Notes (authorized scope)',
      'Attachment metadata',
      'Coordinates (authorized scope)',
      'Accuracy',
      'Device time',
      'Server time',
      'Submission / sync status',
      'Review history',
      ...audit,
    ],
    actionLabel: 'Prepare report decision',
  },
];

const policies: PreparedTab[] = [
  {
    id: 'versions',
    label: 'Office / site versions',
    columns: [
      'Policy name',
      'Staff scope',
      'Version',
      'Effective from',
      'Effective to',
      'Approval status',
    ],
    sections: [
      section('policy', 'Effective policy version', [
        field('name', 'Policy name', 'text', { required: true }),
        field('scope', 'Policy scope', 'select', {
          required: true,
          options: [
            { value: 'office', label: 'Office staff' },
            { value: 'site', label: 'Site staff' },
          ],
        }),
        field('sourceReference', 'Signed policy reference', 'text', { required: true }),
        ...dates(),
        evidence(),
      ]),
      section(
        'rules',
        'Approved rule values',
        [
          field('ruleId', 'SRS rule ID', 'text', { required: true }),
          field('approvedValue', 'Management-approved value', 'text', { required: true }),
          field('unit', 'Value unit', 'text'),
          field('approvalReference', 'Approval / management decision reference', 'text', {
            required: true,
          }),
        ],
        true,
      ),
    ],
    dateOrders: effectiveOrder('policy'),
    serverFields: [
      'Version ID',
      'Validation and unresolved management decisions',
      'Rule interpretation',
      'Locked-period protection',
      'Activation history',
      ...audit,
    ],
    actionLabel: 'Prepare policy version',
  },
  {
    id: 'assignments',
    label: 'Policy assignments',
    columns: [
      'Employee',
      'Site',
      'Policy version',
      'Effective from',
      'Effective to',
      'Approval status',
    ],
    sections: [
      section(
        'assignments',
        'Employee policy assignments',
        [
          employee(),
          site(),
          field('policyVersionId', 'Approved policy version ID', 'uuid', { required: true }),
          field('instructionReference', 'Assignment instruction reference', 'text'),
          ...dates(),
        ],
        true,
      ),
    ],
    dateOrders: effectiveOrder('assignments'),
    serverFields: ['Effective policy resolution', 'Assignment history', ...audit],
    actionLabel: 'Prepare policy assignments',
  },
  {
    id: 'early-studying',
    label: 'Early / studying exceptions',
    columns: ['Employee', 'Exception type', 'Effective from', 'Effective to', 'Approval status'],
    sections: [
      section('exception', 'Approved schedule exception', [
        employee(),
        field('exceptionType', 'Exception type', 'select', {
          required: true,
          options: [
            { value: 'early-reporting', label: 'Early reporting' },
            { value: 'studying', label: 'University / studying schedule' },
          ],
        }),
        field('instructionReference', 'Written instruction reference', 'text', { required: true }),
        field('policyVersionId', 'Dedicated approved policy version ID', 'uuid', {
          required: true,
        }),
        field('earlyStart', 'Requested early reporting time', 'time', {
          when: { field: 'exceptionType', values: ['early-reporting'] },
        }),
        ...dates(),
        evidence(),
      ]),
      section(
        'schedule',
        'Requested schedule rows',
        [
          field('date', 'Schedule date', 'date', { required: true }),
          field('start', 'Start', 'datetime-local', { required: true }),
          field('end', 'End', 'datetime-local', { required: true }),
          field('instructionReference', 'Schedule instruction reference', 'text'),
        ],
        true,
      ),
    ],
    dateOrders: [
      ...effectiveOrder('exception'),
      { section: 'schedule', start: 'start', end: 'end' },
    ],
    serverFields: [
      'Approved early credit',
      'Hourly treatment',
      'Make-up hours',
      'TDA exclusion',
      'KPI effect',
      ...audit,
    ],
    actionLabel: 'Prepare policy exception',
  },
  {
    id: 'activation',
    label: 'Version approval / activation',
    columns: ['Policy', 'Version', 'Effective from', 'Approval status', 'Activation status'],
    sections: [
      section('version', 'Policy version reference', [
        field('policyVersionId', 'Policy version ID', 'uuid', { required: true }),
      ]),
      decision,
    ],
    serverFields: [
      'Source policy',
      'Approved rule values',
      'Management confirmation state',
      'Affected assignments',
      'Effective period',
      'Locked-period impact',
      'Activation time (server)',
      ...audit,
    ],
    actionLabel: 'Prepare version decision',
  },
];

const policyReviews: PreparedTab[] = [
  {
    id: 'waiting',
    label: 'Paired-worker waiting',
    columns: ['Waiting employee', 'Late employee', 'Work date', 'Site', 'Review status'],
    sections: [
      section('waiting', 'Verified waiting proposal', [
        employee(),
        field('lateEmployeeId', 'Late employee', 'reference', {
          reference: 'employees',
          required: true,
        }),
        site(),
        field('workDate', 'Work date', 'date', { required: true }),
        field('taskReference', 'Paired work reference', 'text', { required: true }),
        field('start', 'Waiting start', 'datetime-local', { required: true }),
        field('end', 'Waiting end', 'datetime-local', { required: true }),
        evidence(),
      ]),
    ],
    dateOrders: [{ section: 'waiting', start: 'start', end: 'end' }],
    serverFields: [
      'Verified waiting duration',
      'Verified late duration',
      'Attributed loss',
      'Approved maximum treatment',
      'Policy calculation',
      'Management confirmation state',
      ...audit,
    ],
    actionLabel: 'Prepare waiting review',
  },
  {
    id: 'kpi',
    label: 'Late / shortage / KPI',
    columns: [
      'Employee',
      'Policy year',
      'Late count',
      'Committed hours',
      'Hours shortage',
      'Review status',
    ],
    filters: [field('policyYear', 'Policy year reference', 'text'), site()],
    sections: [],
    readOnly: true,
    serverFields: [
      'Late thresholds',
      'Applied policy version',
      'Worked hours',
      'Exact monthly shortage',
      'KPI calculation base',
      'Annual reset rule',
      'Proposed salary / performance effect',
      'Management confirmation state',
      'Approval trace',
    ],
  },
  {
    id: 'disciplinary',
    label: 'Disciplinary review references',
    columns: ['Case reference', 'Review status'],
    sections: [
      section('case', 'Case referral', [
        field('caseId', 'Authorized disciplinary case ID', 'uuid', { required: true }),
        field('sourceEventId', 'Attendance / zone event reference', 'uuid'),
      ]),
    ],
    serverFields: ['Authorized case reference', 'Review status', 'Approval trace'],
    actionLabel: 'Prepare case referral',
  },
  {
    id: 'waiting-decisions',
    label: 'Waiting decisions',
    columns: ['Request ID', 'Work date', 'Review status'],
    sections: [
      section('request', 'Waiting request reference', [
        field('waitingRequestId', 'Waiting request ID', 'uuid', { required: true }),
      ]),
      decision,
    ],
    serverFields: [
      'Verified duration',
      'Evidence metadata',
      'Applicable policy',
      'Proposed deduction treatment',
      'Management confirmation state',
      ...audit,
    ],
    actionLabel: 'Prepare waiting decision',
  },
];

const monthClose: PreparedTab[] = [
  {
    id: 'lock',
    label: 'Month lock',
    columns: ['Month', 'Population scope', 'Approval status', 'Lock status'],
    sections: [
      section('period', 'Attendance period', [
        field('month', 'Attendance month', 'month', { required: true }),
        field('departmentId', 'Department', 'reference', { reference: 'departments' }),
        site(),
        reason(),
      ]),
    ],
    serverFields: [
      'Resolved employee scope',
      'Outstanding exceptions',
      'Unapproved corrections / overtime',
      'Approved attendance snapshot',
      'Policy / calculation versions',
      'Lock readiness',
      'Lock time (server)',
      ...audit,
    ],
    actionLabel: 'Prepare month-lock request',
  },
  {
    id: 'reopen',
    label: 'Reopen / recalculation',
    columns: ['Month', 'Lock reference', 'Request status'],
    sections: [
      section('reopen', 'Authorized reopening request', [
        field('month', 'Attendance month', 'month', { required: true }),
        field('lockId', 'Locked snapshot ID', 'uuid', { required: true }),
        field('recalculationRequested', 'Request explicit recalculation', 'checkbox'),
        field('correctionId', 'Related correction ID', 'uuid'),
        reason(),
        evidence(),
      ]),
    ],
    serverFields: [
      'Existing lock / handoff status',
      'Affected approved snapshot',
      'Approved recalculation scope',
      'Before / after calculation version',
      'Reopen time (server)',
      ...audit,
    ],
    actionLabel: 'Prepare reopen request',
  },
  {
    id: 'accounts-handoff',
    label: 'Accounts handoff',
    columns: [
      'Month',
      'Locked snapshot',
      'Attendance hours',
      'Approved overtime',
      'Allowances',
      'Approved deductions',
      'Processing status',
    ],
    filters: [field('month', 'Payroll month', 'month')],
    sections: [],
    readOnly: true,
    serverFields: [
      'Locked attendance inputs',
      'Payable summary',
      'Approval trace IDs',
      'Policy / calculation versions',
      'Handoff reference',
      'Accounts verification',
      'Payroll processing status',
      'Generation time (server)',
    ],
  },
  {
    id: 'adjustments',
    label: 'Post-payroll adjustments',
    columns: [
      'Employee',
      'Original month',
      'Adjustment month',
      'Correction reference',
      'Approval status',
    ],
    sections: [
      section('adjustment', 'Adjustment request', [
        employee(),
        field('originalMonth', 'Original payroll month', 'month', { required: true }),
        field('adjustmentMonth', 'Proposed adjustment month', 'month', { required: true }),
        field('correctionId', 'Approved correction ID', 'uuid', { required: true }),
        field('handoffId', 'Original handoff ID', 'uuid', { required: true }),
        reason(),
        evidence(),
      ]),
    ],
    dateOrders: [{ section: 'adjustment', start: 'originalMonth', end: 'adjustmentMonth' }],
    serverFields: [
      'Approved corrected attendance',
      'Original paid inputs',
      'Calculated difference',
      'Accounts verification',
      'Payroll adjustment trace',
      ...audit,
    ],
    actionLabel: 'Prepare adjustment request',
  },
  {
    id: 'decisions',
    label: 'Period decisions',
    columns: ['Request ID', 'Month', 'Request type', 'Approval status'],
    sections: [
      section('request', 'Period request reference', [
        field('requestId', 'Lock / reopen / adjustment request ID', 'uuid', { required: true }),
      ]),
      decision,
    ],
    serverFields: [
      'Request type',
      'Snapshot / handoff reference',
      'Readiness checks',
      'Proposed impact',
      ...audit,
    ],
    actionLabel: 'Prepare period decision',
  },
];

// Confidential reason text and disciplinary evidence need a separately scoped view.
export const operationsWorkspaces: readonly PreparedWorkspaceDefinition[] = [
  {
    id: 'operational-zones',
    title: 'Operational Zones',
    srs: ['6.3', 'ZONE-001..010', '12', '15', 'MD-003A'],
    tabs: zones,
  },
  {
    id: 'site-report-review',
    title: 'Site Report Review',
    srs: ['7.1', 'SITE-007', 'MOB-011..013', '12', '15'],
    tabs: siteReports,
  },
  {
    id: 'attendance-policies',
    title: 'Attendance Policies',
    srs: ['8.1..8.2', 'SHIFT-004', '12', 'MD-006', 'MD-007', 'MD-009', 'MD-012'],
    tabs: policies,
  },
  {
    id: 'policy-impact-review',
    title: 'Policy Impact Review',
    srs: ['OFF-005..006', 'SITE-005..006', 'SITE-010..011', '15', 'MD-009', 'MD-010'],
    tabs: policyReviews,
  },
  {
    id: 'attendance-month-close',
    title: 'Attendance Month Close',
    srs: ['ATT-010', '7.3', '12.1..12.2', '15', 'MD-005', 'MD-014'],
    tabs: monthClose,
  },
];
