import { useState, useMemo, type ReactNode } from 'react';
import {
  FileText,
  Calendar,
  Clock,
  User,
  CheckCircle2,
  Undo2,
  Columns2,
  Table as TableIcon,
  ShieldCheck,
  AlertTriangle,
  Briefcase,
  CreditCard,
  HeartPulse,
  X,
  History,
} from 'lucide-react';
import type { Section } from '../../types/updates';
import type { EmployeeSummary } from '../../types/contracts';
import { approvalLabels } from '../../types/contracts';
import { labelDate } from '../../utils/messages';
import { evidenceRecord, sectionEvidence } from '../../utils/evidence-record';
import '@/styles/change-evidence.css';

interface DecisionRecord {
  id: string;
  decision: number;
  actorId: string;
  roleCode: string;
  reason: string | null;
  decidedAt: string;
}

interface ChangeRequestReviewViewerProps {
  request: {
    id: string;
    employeeId: string;
    state: number;
    requesterId: string;
    requestedAt: string;
    closedAt: string | null;
    version: string;
  };
  section: Section;
  employee?: EmployeeSummary | null;
  reason: string;
  effectiveFrom: string | null;
  beforeData: Record<string, unknown>;
  proposedData: Record<string, unknown>;
  decisions: DecisionRecord[];
  decisionFormSlot?: ReactNode;
  onClose: () => void;
}

interface FieldDiff {
  key: string;
  label: string;
  beforeVal: unknown;
  proposedVal: unknown;
  isModified: boolean;
}

interface GroupDiff {
  title: string;
  fields: FieldDiff[];
}

const SECTION_NAMES = new Set(['basic', 'identity', 'employment', 'bank', 'medical', 'documents']);

function isEmptyVal(val: unknown): boolean {
  return val === null || val === undefined || val === '';
}

function areEqualValues(a: unknown, b: unknown): boolean {
  if (isEmptyVal(a) && isEmptyVal(b)) return true;
  if (typeof a === 'string' && typeof b === 'string') {
    return a.trim() === b.trim();
  }
  return JSON.stringify(a) === JSON.stringify(b);
}

const humanLabel = (key: string) =>
  key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, (c) => c.toUpperCase());

function formatDocumentType(val: unknown): string {
  if (val === 1 || val === '1') return 'CNIC';
  if (val === 2 || val === '2') return 'Passport';
  return String(val ?? '—');
}

function formatRequestValue(
  key: string,
  val: unknown,
): { text: string; isMuted: boolean; isCode?: boolean; isDocType?: boolean } {
  if (isEmptyVal(val)) {
    return { text: 'Not set', isMuted: true };
  }
  if (typeof val === 'boolean') {
    return { text: val ? 'Yes' : 'No', isMuted: false };
  }
  if (key.toLowerCase().includes('documenttype')) {
    return { text: formatDocumentType(val), isMuted: false, isDocType: true };
  }
  if (
    typeof val === 'string' &&
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(val)
  ) {
    return { text: `${val.slice(0, 8)}...${val.slice(-4)}`, isMuted: false, isCode: true };
  }
  return { text: String(val), isMuted: false };
}

function buildGroups(
  sectionName: string,
  beforeObj: Record<string, unknown>,
  proposedObj: Record<string, unknown>,
): GroupDiff[] {
  const makeFields = (keys: string[]) =>
    keys
      .filter((k) => (k in beforeObj || k in proposedObj) && !SECTION_NAMES.has(k))
      .map((k) => {
        const bv = beforeObj[k];
        const pv = proposedObj[k];
        return {
          key: k,
          label: humanLabel(k),
          beforeVal: bv,
          proposedVal: pv,
          isModified: !areEqualValues(bv, pv),
        };
      });

  if (sectionName === 'bank') {
    const bankKeys = ['bankName', 'accountTitle', 'accountNumber', 'iban'];
    const handled = new Set(bankKeys);
    const remainingKeys = Object.keys({ ...beforeObj, ...proposedObj }).filter(
      (k) => !handled.has(k) && !SECTION_NAMES.has(k),
    );
    const keysToShow = [
      ...bankKeys.filter((k) => k in beforeObj || k in proposedObj),
      ...remainingKeys,
    ];
    return [
      {
        title: 'Bank Account & Settlement Details',
        fields: makeFields(keysToShow.length ? keysToShow : bankKeys),
      },
    ];
  }

  if (sectionName === 'employment') {
    const empKeys = ['departmentId', 'designationId', 'reportingManagerEmployeeId', 'branchId'];
    const handled = new Set(empKeys);
    const remainingKeys = Object.keys({ ...beforeObj, ...proposedObj }).filter(
      (k) => !handled.has(k) && !SECTION_NAMES.has(k),
    );
    const keysToShow = [
      ...empKeys.filter((k) => k in beforeObj || k in proposedObj),
      ...remainingKeys,
    ];
    return [
      {
        title: 'Employment & Role Assignment',
        fields: makeFields(keysToShow.length ? keysToShow : empKeys),
      },
    ];
  }

  if (sectionName === 'medical') {
    const medKeys = ['bloodGroup', 'allergies', 'medicalConditions', 'emergencyHospital'];
    const handled = new Set(medKeys);
    const remainingKeys = Object.keys({ ...beforeObj, ...proposedObj }).filter(
      (k) => !handled.has(k) && !SECTION_NAMES.has(k),
    );
    const keysToShow = [
      ...medKeys.filter((k) => k in beforeObj || k in proposedObj),
      ...remainingKeys,
    ];
    return [
      {
        title: 'Medical & Emergency Profile',
        fields: makeFields(keysToShow.length ? keysToShow : medKeys),
      },
    ];
  }

  if (sectionName === 'identity') {
    const personalKeys = ['fullLegalName', 'fatherOrSpouseName', 'gender', 'dateOfBirth'];
    const docKeys = [
      'identityDocumentType',
      'identityDocumentNumber',
      'identityDocumentExpiresOn',
      'identityDocumentId',
    ];
    const contactKeys = ['personalPhone', 'companyPhone', 'officialEmail'];

    const groups: GroupDiff[] = [];
    const g1 = makeFields(personalKeys);
    if (g1.length) groups.push({ title: 'Personal & Legal Identity', fields: g1 });

    const g2 = makeFields(docKeys);
    if (g2.length) groups.push({ title: 'Identification Documents', fields: g2 });

    const g3 = makeFields(contactKeys);
    if (g3.length) groups.push({ title: 'Contact & Communication', fields: g3 });

    const handled = new Set([...personalKeys, ...docKeys, ...contactKeys]);
    const remainingKeys = Object.keys({ ...beforeObj, ...proposedObj }).filter(
      (k) => !handled.has(k) && !SECTION_NAMES.has(k),
    );
    if (remainingKeys.length) {
      groups.push({ title: 'Additional Identity Details', fields: makeFields(remainingKeys) });
    }
    return groups;
  }

  if (sectionName === 'basic') {
    const groups: GroupDiff[] = [];
    const allKeys = Array.from(
      new Set([...Object.keys(beforeObj), ...Object.keys(proposedObj)]),
    ).filter((k) => !SECTION_NAMES.has(k) || k === 'basic');
    const topLevelFields: FieldDiff[] = [];

    for (const key of allKeys) {
      const bVal = beforeObj[key];
      const aVal = proposedObj[key];

      if (
        (bVal && typeof bVal === 'object' && !Array.isArray(bVal)) ||
        (aVal && typeof aVal === 'object' && !Array.isArray(aVal))
      ) {
        const subBefore = evidenceRecord(bVal);
        const subAfter = evidenceRecord(aVal);
        const subKeys = Array.from(new Set([...Object.keys(subBefore), ...Object.keys(subAfter)]));

        const subFields: FieldDiff[] = subKeys.map((subKey) => {
          const svB = subBefore[subKey];
          const svA = subAfter[subKey];
          return {
            key: `${key}.${subKey}`,
            label: humanLabel(subKey),
            beforeVal: svB,
            proposedVal: svA,
            isModified: !areEqualValues(svB, svA),
          };
        });

        groups.push({
          title: humanLabel(key),
          fields: subFields,
        });
      } else {
        topLevelFields.push({
          key,
          label: humanLabel(key),
          beforeVal: bVal,
          proposedVal: aVal,
          isModified: !areEqualValues(bVal, aVal),
        });
      }
    }

    if (topLevelFields.length > 0) {
      groups.push({
        title: 'General Details',
        fields: topLevelFields,
      });
    }

    return groups;
  }

  // Fallback for generic section
  const allKeys = Array.from(
    new Set([...Object.keys(beforeObj), ...Object.keys(proposedObj)]),
  ).filter((k) => !SECTION_NAMES.has(k));
  return [
    {
      title: `${humanLabel(sectionName)} Details`,
      fields: makeFields(allKeys),
    },
  ];
}

export function ChangeRequestReviewViewer({
  request,
  section,
  employee,
  reason,
  effectiveFrom,
  beforeData,
  proposedData,
  decisions,
  decisionFormSlot,
  onClose,
}: ChangeRequestReviewViewerProps) {
  const [viewMode, setViewMode] = useState<'horizontal' | 'table'>('horizontal');

  const beforeObj = sectionEvidence(beforeData, section.name);
  const proposedObj = sectionEvidence(proposedData, section.name);

  const groups = useMemo(
    () => buildGroups(section.name, beforeObj, proposedObj),
    [section.name, beforeObj, proposedObj],
  );

  const modifiedFields = useMemo(() => {
    const list: FieldDiff[] = [];
    groups.forEach((g) => {
      g.fields.forEach((f) => {
        if (f.isModified) list.push(f);
      });
    });
    return list;
  }, [groups]);

  const totalModified = modifiedFields.length;

  const empName = employee?.name || `Employee #${request.employeeId.slice(0, 8)}`;
  const empCode = employee?.employeeNumber
    ? `EMP-${String(employee.employeeNumber).padStart(4, '0')}`
    : `ID-${request.employeeId.slice(0, 6)}`;

  const getSectionIcon = () => {
    switch (section.name) {
      case 'identity':
        return <ShieldCheck size={16} />;
      case 'employment':
        return <Briefcase size={16} />;
      case 'bank':
        return <CreditCard size={16} />;
      case 'medical':
        return <HeartPulse size={16} />;
      default:
        return <FileText size={16} />;
    }
  };

  const statusLabel = approvalLabels[request.state] ?? 'Pending Review';

  return (
    <div className="change-evidence-container">
      {/* Modal Top Header */}
      <div className="change-evidence-header">
        <div className="change-evidence-title-row">
          <div className="change-evidence-title-group">
            <div className="change-evidence-title-icon">{getSectionIcon()}</div>
            <div className="change-evidence-title-text">
              <h2>{section.label} Change Request</h2>
              <p>
                Employee: <strong>{empName}</strong> &bull; Code: <code>{empCode}</code> &bull;
                Request: <code>REQ-{request.id.slice(0, 8).toUpperCase()}</code>
              </p>
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span
              className={`hr-status-pill ${
                request.state === 1
                  ? 'hr-status-pill--approved'
                  : request.state === 3
                    ? 'hr-status-pill--rejected'
                    : request.state === 2
                      ? 'hr-status-pill--returned'
                      : 'hr-status-pill--pending'
              }`}
            >
              {statusLabel}
            </span>

            <button
              type="button"
              className="change-evidence-close-icon"
              onClick={onClose}
              aria-label="Close dialog"
              title="Close"
            >
              <X size={14} />
            </button>
          </div>
        </div>

        {/* Reason / Stated Justification Banner */}
        <div className="change-evidence-banner">
          <div className="change-evidence-reason">
            <span className="change-evidence-reason-label">Stated Justification / Reason</span>
            <span className="change-evidence-reason-value">
              &ldquo;{reason || 'No specific justification provided'}&rdquo;
            </span>
          </div>

          <div className="change-evidence-meta-badges">
            <div className="change-meta-pill" title="Effective Date">
              <Calendar size={12} style={{ color: '#2563eb' }} />
              <span>
                Effective: <strong>{effectiveFrom ?? 'Immediately upon approval'}</strong>
              </span>
            </div>
            <div className="change-meta-pill" title="Requested Date">
              <Clock size={12} style={{ color: '#64748b' }} />
              <span>Requested: {labelDate(request.requestedAt)}</span>
            </div>
            <div className="change-meta-pill" title="Requester">
              <User size={12} style={{ color: '#059669' }} />
              <span>
                Requester: <code>{request.requesterId.slice(0, 8)}</code>
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Diff Modifications Summary & View Mode Toggle */}
      <div className="change-evidence-controls">
        <div className="change-diff-stat-summary">
          {totalModified > 0 ? (
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
              <span className="change-diff-stat-tag change-diff-stat-tag--mod">
                <AlertTriangle size={11} /> {totalModified}{' '}
                {totalModified === 1 ? 'Field' : 'Fields'} Modified
              </span>
              <span className="change-diff-fields-text">
                {modifiedFields.map((f, i) => {
                  const bFmt = formatRequestValue(f.key, f.beforeVal);
                  const pFmt = formatRequestValue(f.key, f.proposedVal);
                  const prevText = isEmptyVal(f.beforeVal) ? 'None' : bFmt.text;
                  return (
                    <span key={f.key}>
                      <strong>{f.label}</strong> ({prevText} &rarr; <strong>{pFmt.text}</strong>)
                      {i < modifiedFields.length - 1 ? ', ' : ''}
                    </span>
                  );
                })}
              </span>
            </div>
          ) : (
            <span
              className="change-diff-stat-tag"
              style={{ background: '#f1f5f9', color: '#475569' }}
            >
              Identical Records &bull; No fields modified
            </span>
          )}
        </div>

        <div className="change-view-toggle">
          <button
            type="button"
            className={`change-view-btn ${viewMode === 'horizontal' ? 'change-view-btn--active' : ''}`}
            onClick={() => setViewMode('horizontal')}
          >
            <Columns2 size={12} /> Side-by-Side (Horizontal)
          </button>
          <button
            type="button"
            className={`change-view-btn ${viewMode === 'table' ? 'change-view-btn--active' : ''}`}
            onClick={() => setViewMode('table')}
          >
            <TableIcon size={12} /> Comparison Table
          </button>
        </div>
      </div>

      {/* Main Body with Side-by-Side Cards */}
      <div className="change-evidence-body">
        {viewMode === 'horizontal' ? (
          /* =========================================================
             HORIZONTAL SIDE-BY-SIDE VIEW (Before vs Proposed)
             ========================================================= */
          <div className="change-horizontal-grid">
            {/* Left Column: CURRENT RECORD (BEFORE) */}
            <div className="change-column-card change-column-card--before">
              <div className="change-column-header">
                <span className="change-column-badge change-column-badge--before">
                  <Undo2 size={12} /> Current Record (Before)
                </span>
                <span style={{ fontSize: '10.5px', color: '#64748b', fontWeight: 500 }}>
                  Active Record in System
                </span>
              </div>

              <div className="change-column-content">
                {groups.map((group) => (
                  <div key={group.title} className="change-section-block">
                    <div className="change-section-block-title">
                      <span>{group.title}</span>
                    </div>
                    <div className="change-fields-list">
                      {group.fields.map((f) => {
                        const fmt = formatRequestValue(f.key, f.beforeVal);
                        const isPriorEmpty = isEmptyVal(f.beforeVal);
                        return (
                          <div key={f.key} className="change-field-row">
                            <span className="change-field-label">{f.label}</span>
                            <span
                              className={`change-field-value ${
                                f.isModified
                                  ? isPriorEmpty
                                    ? 'change-field-value--empty-prior'
                                    : 'change-field-value--changed-old'
                                  : fmt.isMuted
                                    ? 'change-field-value--muted'
                                    : ''
                              } ${fmt.isCode ? 'change-code-chip' : ''}`}
                              title={String(f.beforeVal ?? '')}
                            >
                              {fmt.isDocType ? (
                                <span className="hr-doc-type-chip">{fmt.text}</span>
                              ) : isPriorEmpty && f.isModified ? (
                                'None'
                              ) : (
                                fmt.text
                              )}
                            </span>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Right Column: PROPOSED MODIFICATIONS (AFTER) */}
            <div className="change-column-card change-column-card--after">
              <div className="change-column-header">
                <span className="change-column-badge change-column-badge--after">
                  <CheckCircle2 size={12} /> Proposed Modifications (After)
                </span>
                <span style={{ fontSize: '10.5px', color: '#047857', fontWeight: 600 }}>
                  Awaiting Authorization
                </span>
              </div>

              <div className="change-column-content">
                {groups.map((group) => (
                  <div key={group.title} className="change-section-block">
                    <div className="change-section-block-title">
                      <span>{group.title}</span>
                    </div>
                    <div className="change-fields-list">
                      {group.fields.map((f) => {
                        const fmt = formatRequestValue(f.key, f.proposedVal);
                        return (
                          <div key={f.key} className="change-field-row">
                            <span className="change-field-label">{f.label}</span>
                            <span
                              className={`change-field-value ${
                                f.isModified
                                  ? 'change-field-value--changed-new'
                                  : fmt.isMuted
                                    ? 'change-field-value--muted'
                                    : ''
                              } ${fmt.isCode ? 'change-code-chip' : ''}`}
                              title={String(f.proposedVal ?? '')}
                            >
                              {fmt.isDocType ? (
                                <span className="hr-doc-type-chip">{fmt.text}</span>
                              ) : (
                                fmt.text
                              )}
                              {f.isModified && (
                                <CheckCircle2 size={11} style={{ color: '#10b981' }} />
                              )}
                            </span>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        ) : (
          /* =========================================================
             UNIFIED COMPARISON TABLE VIEW
             ========================================================= */
          <div className="change-unified-table-card">
            <table className="change-diff-table">
              <thead>
                <tr>
                  <th style={{ width: '25%' }}>Category / Field</th>
                  <th style={{ width: '35%' }}>Current Record (Before)</th>
                  <th style={{ width: '35%' }}>Proposed Modification (After)</th>
                  <th style={{ width: '5%', textAlign: 'center' }}>Diff</th>
                </tr>
              </thead>
              <tbody>
                {groups.map((group) => (
                  <RequestTableRows key={group.title} group={group} />
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Prior Decisions / Audit Trail */}
        {decisions.length > 0 && (
          <div
            style={{
              background: '#ffffff',
              border: '1px solid #e2e8f0',
              borderRadius: '12px',
              padding: '16px 20px',
            }}
          >
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                marginBottom: '12px',
                fontWeight: 700,
                fontSize: '14px',
                color: '#0f172a',
              }}
            >
              <History size={16} style={{ color: '#3b82f6' }} />
              <span>Audit Trail & Prior Decisions</span>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
              {decisions.map((d) => (
                <div
                  key={d.id}
                  style={{
                    padding: '12px 16px',
                    background: '#f8fafc',
                    border: '1px solid #e2e8f0',
                    borderRadius: '8px',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '4px',
                  }}
                >
                  <div
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                    }}
                  >
                    <strong style={{ fontSize: '13px', color: '#0f172a' }}>
                      {approvalLabels[d.decision]}
                    </strong>
                    <span style={{ fontSize: '12px', color: '#64748b' }}>
                      {labelDate(d.decidedAt)}
                    </span>
                  </div>
                  {d.reason && (
                    <p style={{ margin: '4px 0', fontSize: '13px', color: '#334155' }}>
                      {d.reason}
                    </p>
                  )}
                  <span style={{ fontSize: '11px', color: '#94a3b8' }}>
                    Decided by {d.roleCode} &bull; Actor ID: <code>{d.actorId.slice(0, 8)}</code>
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Admin Decision Form Slot */}
        {decisionFormSlot && (
          <div
            style={{
              background: '#ffffff',
              border: '1px solid #e2e8f0',
              borderRadius: '12px',
              padding: '20px',
              boxShadow: '0 1px 3px rgba(0, 0, 0, 0.05)',
            }}
          >
            {decisionFormSlot}
          </div>
        )}
      </div>

      {/* Footer */}
      <div className="change-evidence-footer">
        <div className="change-evidence-stamp">
          <ShieldCheck size={16} style={{ color: '#059669' }} />
          <span>Administrative Change Review &bull; Cryptographic Audit Trail</span>
        </div>

        <button
          type="button"
          onClick={onClose}
          style={{
            padding: '8px 20px',
            fontSize: '13px',
            fontWeight: 600,
            borderRadius: '6px',
            border: '1px solid #cbd5e1',
            background: '#ffffff',
            color: '#1e293b',
            cursor: 'pointer',
          }}
        >
          Close Window
        </button>
      </div>
    </div>
  );
}

function RequestTableRows({ group }: { group: GroupDiff }) {
  return (
    <>
      <tr className="change-row--section-header">
        <td colSpan={4}>{group.title}</td>
      </tr>
      {group.fields.map((f) => {
        const fmtB = formatRequestValue(f.key, f.beforeVal);
        const fmtP = formatRequestValue(f.key, f.proposedVal);
        const isPriorEmpty = isEmptyVal(f.beforeVal);
        return (
          <tr key={f.key} className={f.isModified ? 'change-row--modified' : ''}>
            <td className="change-table-field-label">{f.label}</td>
            <td>
              <span
                className={
                  f.isModified
                    ? isPriorEmpty
                      ? 'change-field-value--empty-prior'
                      : 'change-field-value--changed-old'
                    : fmtB.isMuted
                      ? 'change-field-value--muted'
                      : ''
                }
              >
                {isPriorEmpty && f.isModified ? 'None' : fmtB.text}
              </span>
            </td>
            <td>
              <span
                className={
                  f.isModified
                    ? 'change-field-value--changed-new'
                    : fmtP.isMuted
                      ? 'change-field-value--muted'
                      : ''
                }
              >
                {fmtP.text}
              </span>
            </td>
            <td style={{ textAlign: 'center' }}>
              {f.isModified ? (
                <span className="change-mod-tag">MOD</span>
              ) : (
                <span style={{ color: '#cbd5e1' }}>&mdash;</span>
              )}
            </td>
          </tr>
        );
      })}
    </>
  );
}
