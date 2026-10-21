import type { PreparedField, PreparedWorkspaceDefinition } from '../types/prepared-workspace';

const reason: PreparedField = {
  id: 'reason',
  label: 'Reason',
  kind: 'textarea',
  required: true,
  maxLength: 1000,
};
const evidence: PreparedField = { id: 'evidence', label: 'Supporting evidence', kind: 'file' };
const period: PreparedField[] = [
  { id: 'from', label: 'From date', kind: 'date' },
  { id: 'to', label: 'To date', kind: 'date' },
];
export const selfServiceWorkspace: PreparedWorkspaceDefinition = {
  id: 'self-service',
  title: 'My employee workspace',
  srs: ['1.2', 'ROLE-003', '11.1', 'MOB-007'],
  tabs: [
    {
      id: 'profile',
      label: 'My profile',
      columns: ['Field', 'Approved value'],
      sections: [
        {
          id: 'profile',
          title: 'Profile change request',
          fields: [
            { id: 'address', label: 'Current address', kind: 'textarea' },
            { id: 'city', label: 'City', kind: 'text' },
            { id: 'province', label: 'Province', kind: 'text' },
            { id: 'contactName', label: 'Emergency contact name', kind: 'text' },
            { id: 'relationship', label: 'Emergency relationship', kind: 'text' },
            { id: 'contactPhone', label: 'Emergency phone', kind: 'text', maxLength: 16 },
            reason,
          ],
        },
      ],
      serverFields: [
        'Approved profile',
        'Employee number',
        'Employment and assignment',
        'Change request history',
      ],
      actionLabel: 'Submit profile request',
    },
    {
      id: 'documents',
      label: 'My documents',
      columns: ['Document', 'Expiry', 'Availability'],
      sections: [
        {
          id: 'document',
          title: 'Document update request',
          fields: [
            { id: 'type', label: 'Document type', kind: 'text', required: true },
            { id: 'expiry', label: 'Expiry date', kind: 'date' },
            evidence,
            reason,
          ],
        },
      ],
      serverFields: ['Document verification', 'Expiry reminders'],
      actionLabel: 'Submit document request',
    },
    {
      id: 'attendance',
      presentation: 'calendar',
      label: 'My attendance',
      columns: ['Date', 'Status', 'Worked hours', 'Payable hours', 'Overtime', 'Exceptions'],
      filters: period,
      sections: [],
      readOnly: true,
      serverFields: [
        'Monthly attendance',
        'Raw punches',
        'Correction history',
        'Calculation version',
      ],
    },
    {
      id: 'shift',
      label: 'My shift',
      presentation: 'roster',
      columns: ['Day', 'Office or site', 'Shift', 'Start', 'End', 'Breaks', 'Holiday'],
      filters: period,
      sections: [],
      readOnly: true,
      serverFields: ['Current roster', 'Assigned location', 'Effective shift', 'Temporary changes'],
    },
    {
      id: 'corrections',
      label: 'Corrections',
      columns: ['Date', 'Requested correction', 'Status', 'Decision'],
      sections: [
        {
          id: 'correction',
          title: 'Attendance correction request',
          fields: [
            { id: 'date', label: 'Attendance date', kind: 'date', required: true },
            { id: 'event', label: 'Original event reference', kind: 'uuid' },
            {
              id: 'action',
              label: 'Requested event',
              kind: 'select',
              required: true,
              options: [
                { value: 'check-in', label: 'Check In' },
                { value: 'check-out', label: 'Check Out' },
                { value: 'status', label: 'Attendance status' },
              ],
            },
            {
              id: 'time',
              label: 'Requested time (Pakistan)',
              kind: 'datetime-local',
              when: { field: 'action', values: ['check-in', 'check-out'] },
            },
            {
              id: 'status',
              label: 'Requested status',
              kind: 'text',
              when: { field: 'action', values: ['status'] },
            },
            reason,
            evidence,
          ],
        },
      ],
      serverFields: ['Original evidence', 'Period lock', 'Correction decision'],
      actionLabel: 'Submit correction',
    },
    {
      id: 'leave',
      label: 'My leave',
      columns: ['Type', 'From', 'To', 'Duration', 'Status'],
      sections: [
        {
          id: 'leave',
          title: 'Leave request',
          fields: [
            { id: 'type', label: 'Leave type reference', kind: 'uuid', required: true },
            { id: 'from', label: 'Leave from', kind: 'date', required: true },
            { id: 'to', label: 'Leave to', kind: 'date', required: true },
            { id: 'contact', label: 'Contact during leave', kind: 'text', required: true },
            { id: 'handover', label: 'Handover notes', kind: 'textarea' },
            { id: 'reliever', label: 'Reliever reference', kind: 'uuid' },
            reason,
            evidence,
          ],
        },
      ],
      dateOrders: [{ section: 'leave', start: 'from', end: 'to' }],
      serverFields: [
        'Available balances',
        'Eligibility',
        'Duration and holidays',
        'Paid or unpaid impact',
        'Approval and cancellation history',
      ],
      actionLabel: 'Submit leave request',
    },
    {
      id: 'overtime',
      label: 'My overtime',
      columns: ['Date', 'Site or job', 'Minutes', 'Status', 'Payable amount'],
      sections: [
        {
          id: 'overtime',
          title: 'Overtime request',
          fields: [
            { id: 'start', label: 'Work start (Pakistan)', kind: 'datetime-local', required: true },
            { id: 'end', label: 'Work end (Pakistan)', kind: 'datetime-local', required: true },
            { id: 'site', label: 'Assigned site reference', kind: 'uuid' },
            { id: 'job', label: 'Job reference', kind: 'text' },
            reason,
            evidence,
          ],
        },
      ],
      dateOrders: [{ section: 'overtime', start: 'start', end: 'end' }],
      serverFields: ['Eligibility', 'Approved overtime', 'Applicable rate', 'Payable amount'],
      actionLabel: 'Submit overtime request',
    },
    {
      id: 'salary',
      label: 'Salary and deductions',
      columns: ['Period', 'Approved salary', 'Deductions', 'Allowances', 'Recoveries', 'Payslip'],
      filters: [{ id: 'month', label: 'Payroll month', kind: 'month' }],
      sections: [],
      readOnly: true,
      serverFields: [
        'Approved salary summary',
        'Attendance deductions',
        'Advance and loan recoveries',
        'Authorized payslip download',
      ],
    },
    {
      id: 'advances',
      label: 'Advances and loans',
      columns: ['Request', 'Amount', 'Status', 'Repayment'],
      sections: [
        {
          id: 'financial',
          title: 'Advance or loan request',
          fields: [
            {
              id: 'type',
              label: 'Request type',
              kind: 'select',
              required: true,
              options: [
                { value: 'advance', label: 'Advance' },
                { value: 'loan', label: 'Loan' },
              ],
            },
            {
              id: 'amount',
              label: 'Requested amount',
              kind: 'number',
              min: 0,
              step: '0.01',
              required: true,
            },
            { id: 'currency', label: 'Currency', kind: 'text', required: true, maxLength: 3 },
            { id: 'repayment', label: 'Requested repayment arrangement', kind: 'textarea' },
            reason,
          ],
        },
      ],
      serverFields: ['Approved amount', 'Repayment history', 'Outstanding balance'],
      actionLabel: 'Submit financial request',
    },
    {
      id: 'claims',
      label: 'My claims',
      columns: ['Claim', 'Date', 'Status', 'Approved amount'],
      sections: [
        {
          id: 'claim',
          title: 'Allowance or travel claim',
          fields: [
            { id: 'type', label: 'Claim type', kind: 'text', required: true },
            { id: 'date', label: 'Duty or travel date', kind: 'date', required: true },
            { id: 'session', label: 'Work session reference', kind: 'uuid' },
            { id: 'deployment', label: 'Deployment reference', kind: 'uuid' },
            {
              id: 'amount',
              label: 'Requested amount',
              kind: 'number',
              min: 0,
              step: '0.01',
              required: true,
            },
            { id: 'currency', label: 'Currency', kind: 'text', maxLength: 3, required: true },
            reason,
            evidence,
          ],
        },
      ],
      serverFields: [
        'Eligibility',
        'Duplicate-cycle check',
        'Approved calculation',
        'Payment status',
      ],
      actionLabel: 'Submit claim',
    },
    {
      id: 'site-reports',
      label: 'My site reports',
      columns: ['Site', 'Report', 'Time', 'Status'],
      sections: [
        {
          id: 'report',
          title: 'Site report',
          fields: [
            { id: 'site', label: 'Assigned site reference', kind: 'uuid', required: true },
            { id: 'task', label: 'Assigned task reference', kind: 'text' },
            {
              id: 'type',
              label: 'Report type',
              kind: 'select',
              required: true,
              options: ['Arrival', 'Departure', 'Delay', 'Access issue', 'Incident'].map(
                (label) => ({ value: label, label }),
              ),
            },
            { id: 'note', label: 'Report notes', kind: 'textarea', required: true },
            evidence,
          ],
        },
      ],
      serverFields: [
        'Server receipt time',
        'Authorized location evidence',
        'Sync status',
        'Review history',
      ],
      actionLabel: 'Submit site report',
    },
    {
      id: 'zones',
      label: 'My restrictions',
      columns: ['Zone', 'Instruction', 'Effective period', 'Permission', 'Acknowledgement'],
      sections: [
        {
          id: 'exception',
          title: 'Zone exception or location correction',
          fields: [
            { id: 'zone', label: 'Applicable zone reference', kind: 'uuid', required: true },
            { id: 'event', label: 'Related event reference', kind: 'uuid' },
            reason,
            evidence,
          ],
        },
      ],
      serverFields: [
        'Applicable instructions',
        'Duty monitoring window',
        'Authorized exception',
        'Warning acknowledgements',
      ],
      actionLabel: 'Submit zone request',
    },
    {
      id: 'notifications',
      label: 'My notifications',
      columns: ['Notice', 'Received', 'Read state'],
      sections: [],
      readOnly: true,
      serverFields: [
        'Approval outcomes',
        'Missing punches',
        'Shift changes',
        'Document reminders',
        'Safe policy notices',
      ],
    },
  ],
};
