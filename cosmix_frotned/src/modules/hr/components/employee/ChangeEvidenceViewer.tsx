import { useState, useMemo } from 'react';
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
  X,
} from 'lucide-react';
import type { Section } from '../../types/updates';
import { labelDate } from '../../utils/messages';
import { evidenceRecord, sectionEvidence } from '../../utils/evidence-record';
import '@/styles/change-evidence.css';

interface ChangeEvidenceViewerProps {
  data: {
    change: {
      id: string;
      employeeId: string;
      section: number;
      effectiveFrom: string;
      approvalRequestId: string | null;
      actorId: string;
      recordedAt: string;
    };
    before: {
      recordId: string | null;
      effectiveFrom: string;
      effectiveTo: string | null;
      data: Record<string, unknown>;
      bankVerified: boolean | null;
    };
    after: {
      recordId: string | null;
      effectiveFrom: string;
      effectiveTo: string | null;
      data: Record<string, unknown>;
      bankVerified: boolean | null;
    };
    reason: string;
  };
  section: Section;
  onClose: () => void;
}

interface FieldDiff {
  key: string;
  label: string;
  beforeVal: unknown;
  afterVal: unknown;
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

function formatValue(val: unknown): { text: string; isMuted: boolean; isCode?: boolean } {
  if (isEmptyVal(val)) {
    return { text: 'Not set', isMuted: true };
  }
  if (typeof val === 'boolean') {
    return { text: val ? 'Yes' : 'No', isMuted: false };
  }
  if (
    typeof val === 'string' &&
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(val)
  ) {
    return { text: `${val.slice(0, 8)}...${val.slice(-4)}`, isMuted: false, isCode: true };
  }
  return { text: String(val), isMuted: false };
}

export function ChangeEvidenceViewer({ data, section, onClose }: ChangeEvidenceViewerProps) {
  const [viewMode, setViewMode] = useState<'horizontal' | 'table'>('horizontal');

  const beforeRaw = data.before?.data;
  const afterRaw = data.after?.data;
  const beforeObj = sectionEvidence(beforeRaw, section.name);
  const afterObj = sectionEvidence(afterRaw, section.name);

  // Build structured groups of field diffs
  const groups: GroupDiff[] = useMemo(() => {
    const result: GroupDiff[] = [];
    const allKeys = Array.from(
      new Set([...Object.keys(beforeObj), ...Object.keys(afterObj)]),
    ).filter((k) => !SECTION_NAMES.has(k) || k === section.name);

    const topLevelFields: FieldDiff[] = [];

    for (const key of allKeys) {
      const bVal = beforeObj[key];
      const aVal = afterObj[key];

      // Check if nested object (like currentAddress, permanentAddress, emergencyContact)
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
          const isMod = !areEqualValues(svB, svA);
          return {
            key: `${key}.${subKey}`,
            label: humanLabel(subKey),
            beforeVal: svB,
            afterVal: svA,
            isModified: isMod,
          };
        });

        result.push({
          title: humanLabel(key),
          fields: subFields,
        });
      } else {
        const isMod = !areEqualValues(bVal, aVal);
        topLevelFields.push({
          key,
          label: humanLabel(key),
          beforeVal: bVal,
          afterVal: aVal,
          isModified: isMod,
        });
      }
    }

    if (topLevelFields.length > 0) {
      result.push({
        title: `${humanLabel(section.name)} Details`,
        fields: topLevelFields,
      });
    }

    return result;
  }, [beforeObj, afterObj, section.name]);

  const totalModified = useMemo(() => {
    return groups.reduce((acc, g) => acc + g.fields.filter((f) => f.isModified).length, 0);
  }, [groups]);

  return (
    <div className="change-evidence-container">
      {/* Top Header */}
      <div className="change-evidence-header">
        <div className="change-evidence-title-row">
          <div className="change-evidence-title-group">
            <div className="change-evidence-title-icon">
              <FileText size={16} />
            </div>
            <div className="change-evidence-title-text">
              <h2>Change Evidence & Audit Slip</h2>
              <p>
                Section: <strong>{section.label}</strong> &bull; Employee ID:{' '}
                <code>{data.change.employeeId.slice(0, 8).toUpperCase()}</code>
              </p>
            </div>
          </div>

          <button
            type="button"
            className="change-evidence-close-icon"
            onClick={onClose}
            aria-label="Close dialog"
            title="Close dialog"
          >
            <X size={14} />
          </button>
        </div>

        {/* Reason Banner / Callout */}
        <div className="change-evidence-banner">
          <div className="change-evidence-reason">
            <span className="change-evidence-reason-label">Reason for Modification</span>
            <span className="change-evidence-reason-value">
              &ldquo;{data.reason || 'No specific reason provided'}&rdquo;
            </span>
          </div>

          <div className="change-evidence-meta-badges">
            <div className="change-meta-pill" title="Effective Date">
              <Calendar size={12} style={{ color: '#2563eb' }} />
              <span>
                Effective: <strong>{data.change.effectiveFrom}</strong>
              </span>
            </div>
            <div className="change-meta-pill" title="Recorded Timestamp">
              <Clock size={12} style={{ color: '#64748b' }} />
              <span>{labelDate(data.change.recordedAt)}</span>
            </div>
            <div className="change-meta-pill" title="Changed By Actor">
              <User size={12} style={{ color: '#059669' }} />
              <span>
                By: <code>{data.change.actorId.slice(0, 8)}</code>
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Sub-bar / View Toggle */}
      <div className="change-evidence-controls">
        <div className="change-diff-stat-summary">
          <span>Summary of audit modifications:</span>
          {totalModified > 0 ? (
            <span className="change-diff-stat-tag change-diff-stat-tag--mod">
              <AlertTriangle size={11} /> {totalModified} {totalModified === 1 ? 'Field' : 'Fields'}{' '}
              Modified
            </span>
          ) : (
            <span
              className="change-diff-stat-tag"
              style={{ background: '#f1f5f9', color: '#475569' }}
            >
              Identical Records
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

      {/* Body Area */}
      <div className="change-evidence-body">
        {viewMode === 'horizontal' ? (
          /* =========================================================
             HORIZONTAL SIDE-BY-SIDE VIEW (Before on Left, After on Right)
             ========================================================= */
          <div className="change-horizontal-grid">
            {/* Left: BEFORE CARD */}
            <div className="change-column-card change-column-card--before">
              <div className="change-column-header">
                <span className="change-column-badge change-column-badge--before">
                  <Undo2 size={12} /> Previous State (Before)
                </span>
                <span style={{ fontSize: '10.5px', color: '#64748b' }}>Original Record</span>
              </div>

              <div className="change-column-content">
                {groups.map((group) => (
                  <div key={group.title} className="change-section-block">
                    <div className="change-section-block-title">
                      <span>{group.title}</span>
                    </div>
                    <div className="change-fields-list">
                      {group.fields.map((f) => {
                        const fmt = formatValue(f.beforeVal);
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
                              {isPriorEmpty && f.isModified ? 'None' : fmt.text}
                            </span>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Right: AFTER CARD */}
            <div className="change-column-card change-column-card--after">
              <div className="change-column-header">
                <span className="change-column-badge change-column-badge--after">
                  <CheckCircle2 size={12} /> Updated State (After)
                </span>
                <span style={{ fontSize: '10.5px', color: '#047857', fontWeight: 600 }}>
                  Active Version
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
                        const fmt = formatValue(f.afterVal);
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
                              title={String(f.afterVal ?? '')}
                            >
                              {fmt.text}
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
                  <th style={{ width: '35%' }}>Previous Record (Before)</th>
                  <th style={{ width: '35%' }}>Updated Record (After)</th>
                  <th style={{ width: '5%', textAlign: 'center' }}>Diff</th>
                </tr>
              </thead>
              <tbody>
                {groups.map((group) => (
                  <GroupTableRows key={group.title} group={group} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Footer / Verification Stamp */}
      <div className="change-evidence-footer">
        <div className="change-evidence-stamp">
          <ShieldCheck size={13} style={{ color: '#059669' }} />
          <span>Cryptographically sealed HR change audit slip &bull; Immutable record</span>
        </div>

        <button
          type="button"
          onClick={onClose}
          style={{
            padding: '4px 14px',
            fontSize: '11.5px',
            fontWeight: 600,
            borderRadius: '4px',
            border: '1px solid #cbd5e1',
            background: '#ffffff',
            color: '#1e293b',
            cursor: 'pointer',
          }}
        >
          Close Slip
        </button>
      </div>
    </div>
  );
}

function GroupTableRows({ group }: { group: GroupDiff }) {
  return (
    <>
      <tr className="change-row--section-header">
        <td colSpan={4}>{group.title}</td>
      </tr>
      {group.fields.map((f) => {
        const fmtB = formatValue(f.beforeVal);
        const fmtA = formatValue(f.afterVal);
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
                    : fmtA.isMuted
                      ? 'change-field-value--muted'
                      : ''
                }
              >
                {fmtA.text}
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
