import React, { useEffect, useState, useMemo } from 'react';
import {
  CalendarCog,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Clock,
  Copy,
  FileCheck2,
  FileText,
  Layers,
  Plus,
  Search,
  Shield,
  ShieldAlert,
  Tag,
} from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import {
  Command,
  Denied,
  MutationForm,
  PrivateWorkspace,
  QueryState,
} from '../../components/SchedulingUi';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { leaveType, leaveTypeInput, leaveTypes } from './contracts';
import { canAll, kindNames } from './inputs';
import { Choice, EvidenceRecord, Reload } from './Ui';
import { readableLeavePolicies, type PublishedLeavePolicy } from '../leave-benefits/api';
import { LeavePolicyPublisher } from './LeavePolicyPublisher';
import '@/styles/leave-settings.css';

export function LeaveSettingsPage() {
  return (
    <PrivateWorkspace resource="leave-settings">
      <LeaveSettings />
    </PrivateWorkspace>
  );
}
function LeaveSettings() {
  const access = useAttendanceAccess();
  const allowed = access.ready && canAll(access.user, 'attendance.leaves.configure');
  const types = useAttendanceQuery('/hr/leave-settings/types', leaveTypes, allowed);
  const policies = useAttendanceQuery(
    '/hr/leave-settings/policies',
    readableLeavePolicies,
    allowed,
  );
  const [form, setForm] = useState('');
  const [epoch, setEpoch] = useState(0);
  const [source, setSource] = useState<PublishedLeavePolicy | undefined>();
  const [searchQuery, setSearchQuery] = useState('');
  const [expandedTypeId, setExpandedTypeId] = useState<string | null>(null);
  const [expandedPolicyId, setExpandedPolicyId] = useState<string | null>(null);

  useEffect(() => {
    if (policies.isError) {
      setSource(undefined);
      setForm((current) => (current.startsWith('policy') ? '' : current));
    }
  }, [policies.isError]);

  const publicationBlock = policies.isError
    ? 'Publishing is disabled because the published settings could not be read. Reload the history before publishing.'
    : policies.isPending || !policies.data
      ? 'Publishing is disabled until the published settings have loaded.'
      : policies.isFetching
        ? 'Publishing is disabled while the published settings are being refreshed.'
        : '';

  const filteredTypes = useMemo(() => {
    const list = types.data ?? [];
    if (!searchQuery.trim()) return list;
    const q = searchQuery.toLowerCase();
    return list.filter(
      (t) =>
        t.code.toLowerCase().includes(q) ||
        t.name.toLowerCase().includes(q) ||
        (kindNames[t.kind] || '').toLowerCase().includes(q),
    );
  }, [types.data, searchQuery]);

  function reload() {
    setForm('');
    setSource(undefined);
    setEpoch((e) => e + 1);
    void types.refetch();
    void policies.refetch();
  }
  if (!allowed)
    return <Denied>Leave settings require the explicit All-scoped configure permission.</Denied>;
  return (
    <div className="leave-settings-container">
      {/* 1. Banner Header */}
      <div className="leave-settings-banner">
        <div className="leave-settings-banner-top">
          <div className="leave-settings-title-group">
            <h1>
              <CalendarCog size={22} style={{ color: '#2563eb' }} />
              Leave Settings &amp; Entitlement Policies
            </h1>
            <p>
              Manage organizational leave types, quota rules, and version-controlled immutable
              policy snapshots.
            </p>
          </div>
          <Reload
            onClick={reload}
            disabled={Boolean(form) || types.isFetching || policies.isFetching}
          />
        </div>
      </div>

      {/* 2. 4 Executive KPI Cards (56px) */}
      {!form && (
        <div className="leave-settings-kpi-grid">
          <div className="leave-settings-kpi-card">
            <div className="leave-kpi-icon types">
              <Layers size={18} />
            </div>
            <div className="leave-kpi-info">
              <span className="leave-kpi-label">Configured Types</span>
              <span className="leave-kpi-value">{types.data?.length ?? 0} Types</span>
              <span className="leave-kpi-sub">System categories</span>
            </div>
          </div>

          <div className="leave-settings-kpi-card">
            <div className="leave-kpi-icon active">
              <CheckCircle2 size={18} />
            </div>
            <div className="leave-kpi-info">
              <span className="leave-kpi-label">Active For Staff</span>
              <span className="leave-kpi-value" style={{ color: '#059669' }}>
                {types.data?.filter((t) => t.isActive).length ?? 0} Active
              </span>
              <span className="leave-kpi-sub">Eligible for requests</span>
            </div>
          </div>

          <div className="leave-settings-kpi-card">
            <div className="leave-kpi-icon policies">
              <FileCheck2 size={18} />
            </div>
            <div className="leave-kpi-info">
              <span className="leave-kpi-label">Published Policies</span>
              <span className="leave-kpi-value" style={{ color: '#7c3aed' }}>
                {policies.data?.length ?? 0} Versions
              </span>
              <span className="leave-kpi-sub">Immutable snapshots</span>
            </div>
          </div>

          <div className="leave-settings-kpi-card">
            <div className="leave-kpi-icon guard">
              <Shield size={18} />
            </div>
            <div className="leave-kpi-info">
              <span className="leave-kpi-label">Rule Standard</span>
              <span className="leave-kpi-value" style={{ color: '#d97706' }}>
                Enterprise v4
              </span>
              <span className="leave-kpi-sub">Entitlement Guarded</span>
            </div>
          </div>
        </div>
      )}

      {/* 3. Publication Block Warning if any */}
      {publicationBlock && (
        <div
          className="leave-publication-warning"
          id="leave-policy-publication-status"
          role="status"
        >
          <ShieldAlert size={16} />
          <span>{publicationBlock}</span>
        </div>
      )}

      {/* 4. Active Form (Leave Type Form OR Policy Publisher) */}
      {form === 'type' ? (
        <LeaveTypeForm key={epoch} onSaved={reload} onCancel={reload} />
      ) : form.startsWith('policy') && !policies.isError ? (
        <LeavePolicyPublisher
          key={epoch}
          source={source}
          baseOnly={form === 'policy-base'}
          publicationBlock={publicationBlock}
          onSaved={reload}
          onCancel={reload}
        />
      ) : (
        <div className="leave-settings-actions-bar">
          <Command icon={Plus} onClick={() => setForm('type')}>
            Create leave type
          </Command>
          <Command
            icon={Plus}
            disabled={Boolean(publicationBlock)}
            aria-describedby={publicationBlock ? 'leave-policy-publication-status' : undefined}
            onClick={() => {
              if (!publicationBlock) {
                setSource(undefined);
                setForm('policy-base');
              }
            }}
          >
            Publish base leave settings
          </Command>
          <Command
            icon={Plus}
            disabled={Boolean(publicationBlock)}
            aria-describedby={publicationBlock ? 'leave-policy-publication-status' : undefined}
            onClick={() => {
              if (!publicationBlock) {
                setSource(undefined);
                setForm('policy');
              }
            }}
          >
            Publish v4 leave settings
          </Command>
        </div>
      )}

      {/* 5. Section: Current Types (High-Density Table) */}
      {!form && (
        <section className="leave-settings-card">
          <div className="leave-card-header">
            <div className="leave-card-title">
              <h2>
                <Tag size={16} style={{ color: '#2563eb' }} />
                Current Leave Types
              </h2>
              <span className="leave-card-subtitle">
                System leave categories, approval workflows, and balance deduction behaviors.
              </span>
            </div>
            <div className="leave-search-input">
              <Search size={14} style={{ color: '#94a3b8' }} />
              <input
                type="text"
                placeholder="Search leave types..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
          </div>

          <QueryState query={types} empty={types.data?.length === 0} />

          {!types.isError && types.data && types.data.length > 0 && (
            <div className="leave-types-table-wrap">
              <table className="leave-types-table">
                <thead>
                  <tr>
                    <th>Code &amp; Name</th>
                    <th>Kind / Category</th>
                    <th>Reduces Regular Balance</th>
                    <th>Advance Approval</th>
                    <th>Evidence Required</th>
                    <th>Status</th>
                    <th style={{ textAlign: 'right' }}>Audit Evidence</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredTypes.map((row) => {
                    const isExpanded = expandedTypeId === row.id;
                    const kindLabel = kindNames[row.kind] || 'General';
                    const kindClass =
                      row.code.toLowerCase().includes('sick') ||
                      kindLabel.toLowerCase().includes('sick')
                        ? 'sick'
                        : row.code.toLowerCase().includes('casual') ||
                            kindLabel.toLowerCase().includes('casual')
                          ? 'casual'
                          : row.code.toLowerCase().includes('annual')
                            ? 'annual'
                            : row.code.toLowerCase().includes('maternity')
                              ? 'maternity'
                              : row.code.toLowerCase().includes('unpaid')
                                ? 'unpaid'
                                : '';

                    return (
                      <React.Fragment key={row.id}>
                        <tr>
                          <td>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                              <span className="leave-code-chip">{row.code}</span>
                              <strong style={{ color: '#0f172a' }}>{row.name}</strong>
                            </div>
                          </td>
                          <td>
                            <span className={`leave-kind-pill ${kindClass}`}>{kindLabel}</span>
                          </td>
                          <td>
                            <span
                              className={`leave-flag-badge ${row.reducesRegularBalance ? 'yes' : 'no'}`}
                            >
                              {row.reducesRegularBalance
                                ? '✓ Deducts Balance'
                                : '— Does Not Deduct'}
                            </span>
                          </td>
                          <td>
                            <span
                              className={`leave-flag-badge ${row.requiresAdvanceApproval ? 'yes' : 'no'}`}
                            >
                              {row.requiresAdvanceApproval ? '✓ Mandatory' : '— Optional'}
                            </span>
                          </td>
                          <td>
                            <span
                              className={`leave-flag-badge ${row.requiresEvidence ? 'yes' : 'no'}`}
                            >
                              {row.requiresEvidence ? '✓ Required' : '— Not Required'}
                            </span>
                          </td>
                          <td>
                            <span
                              className={`leave-status-pill ${row.isActive ? 'active' : 'inactive'}`}
                            >
                              <span
                                style={{
                                  width: '6px',
                                  height: '6px',
                                  borderRadius: '50%',
                                  background: row.isActive ? '#10b981' : '#94a3b8',
                                }}
                              />
                              {row.isActive ? 'Active' : 'Inactive'}
                            </span>
                          </td>
                          <td style={{ textAlign: 'right' }}>
                            <button
                              type="button"
                              className="leave-view-btn"
                              onClick={() => setExpandedTypeId(isExpanded ? null : row.id)}
                            >
                              {isExpanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
                              {isExpanded ? 'Hide' : 'Inspect'}
                            </button>
                          </td>
                        </tr>
                        {isExpanded && (
                          <tr className="leave-expanded-row">
                            <td colSpan={7}>
                              <div className="leave-detail-box">
                                <strong
                                  style={{
                                    display: 'block',
                                    marginBottom: '4px',
                                    color: '#475569',
                                  }}
                                >
                                  Raw Evidence Record &amp; Version Hash (v{row.version})
                                </strong>
                                <EvidenceRecord value={{ ...row, kind: kindLabel }} />
                              </div>
                            </td>
                          </tr>
                        )}
                      </React.Fragment>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}

      {/* 6. Section: Immutable Published Settings */}
      {!form && (
        <section className="leave-settings-card">
          <div className="leave-card-header">
            <div className="leave-card-title">
              <h2>
                <Shield size={16} style={{ color: '#7c3aed' }} />
                Immutable Published Policy Settings
              </h2>
              <span className="leave-card-subtitle">
                Version-controlled legal leave snapshots used for employee quota balances and
                payroll calculations.
              </span>
            </div>
          </div>

          <QueryState query={policies} empty={false} />

          {!policies.isError && policies.data && policies.data.length === 0 && (
            <div className="leave-empty-state">
              <div className="leave-empty-icon">
                <FileText size={18} />
              </div>
              <h3>No Immutable Policy Snapshots Published Yet</h3>
              <p>
                Published leave policies define statutory quotas (e.g. 18d Annual, 12d Casual, 6d
                Sick), holiday treatments, and staff category eligibility. Use the buttons above to
                publish your first baseline or v4 leave policy.
              </p>
              <div style={{ marginTop: '8px' }}>
                <Command
                  icon={Plus}
                  disabled={Boolean(publicationBlock)}
                  onClick={() => {
                    if (!publicationBlock) {
                      setSource(undefined);
                      setForm('policy');
                    }
                  }}
                >
                  Publish Baseline v4 Policy
                </Command>
              </div>
            </div>
          )}

          {!policies.isError && policies.data && policies.data.length > 0 && (
            <div className="leave-policies-grid">
              {policies.data.map((row) => {
                const isExpanded = expandedPolicyId === row.versionId;
                const vNum = row.settings.settingsVersion ?? 1;

                return (
                  <div key={row.versionId} className="leave-policy-card">
                    <div className="leave-policy-card-top">
                      <div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                          <span className="leave-policy-code">{row.code}</span>
                          <span className="leave-policy-version">v{vNum}.0</span>
                        </div>
                        <div className="leave-policy-dates">
                          <Clock size={12} style={{ color: '#94a3b8' }} />
                          <span>
                            {row.effectiveFrom} → {row.effectiveTo ?? 'Open / Permanent'}
                          </span>
                        </div>
                      </div>

                      <Command
                        icon={Copy}
                        variant="secondary"
                        disabled={Boolean(form) || Boolean(publicationBlock)}
                        onClick={() => {
                          if (!form && !publicationBlock) {
                            setSource(structuredClone(row));
                            setForm(vNum <= 2 ? 'policy-base' : 'policy');
                          }
                        }}
                      >
                        Copy
                      </Command>
                    </div>

                    <div className="leave-policy-rules-summary">
                      <div>
                        <strong>Annual:</strong> {String(row.settings.annualDays ?? 18)}d
                      </div>
                      <div>
                        <strong>Casual:</strong> {String(row.settings.casualDays ?? 12)}d
                      </div>
                      <div>
                        <strong>Sick:</strong> {String(row.settings.sickDays ?? 6)}d
                      </div>
                      <div>
                        <strong>Day:</strong>{' '}
                        {String(Math.round((row.settings.standardDayMinutes ?? 480) / 60))}h
                      </div>
                    </div>

                    <div>
                      <button
                        type="button"
                        className="leave-view-btn"
                        onClick={() => setExpandedPolicyId(isExpanded ? null : row.versionId)}
                      >
                        {isExpanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
                        {isExpanded ? 'Hide Parameters' : 'View Full Policy Parameters'}
                      </button>
                    </div>

                    {isExpanded && (
                      <div className="leave-detail-box">
                        <EvidenceRecord value={row} />
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </section>
      )}
    </div>
  );
}
function LeaveTypeForm({ onSaved, onCancel }: { onSaved: () => void; onCancel: () => void }) {
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [kind, setKind] = useState('');
  const [evidence, setEvidence] = useState('');
  const [advance, setAdvance] = useState('');
  const [regular, setRegular] = useState('');
  const [inputReason, setReason] = useState('');
  const options = [
    { value: 'true', label: 'Yes' },
    { value: 'false', label: 'No' },
  ];
  function boolean(value: string) {
    if (!value) throw new Error('Review all leave-type flags explicitly.');
    return value === 'true';
  }
  return (
    <MutationForm
      title="Create explicit leave type"
      onSaved={onSaved}
      onCancel={onCancel}
      build={() => ({
        path: '/hr/leave-settings/types',
        method: 'POST',
        schema: leaveType,
        body: leaveTypeInput.parse({
          code,
          name,
          kind: kind ? Number(kind) : null,
          requiresEvidence: boolean(evidence),
          requiresAdvanceApproval: boolean(advance),
          reducesRegularBalance: boolean(regular),
          reason: inputReason,
        }),
      })}
    >
      <div className="attendance-grid">
        <TextInput
          label="Code"
          required
          maxLength={40}
          value={code}
          onChange={(e) => setCode(e.target.value)}
        />
        <TextInput
          label="Name"
          required
          maxLength={200}
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </div>
      <Choice
        label="Kind"
        value={kind}
        options={kindNames.map((label, i) => ({ value: String(i), label }))}
        onChange={setKind}
      />
      <Choice label="Requires evidence" value={evidence} options={options} onChange={setEvidence} />
      <Choice
        label="Requires advance approval"
        value={advance}
        options={options}
        onChange={setAdvance}
      />
      <Choice
        label="Reduces regular balance"
        value={regular}
        options={options}
        onChange={setRegular}
      />
      <TextInput
        label="Reason"
        maxLength={1000}
        required
        value={inputReason}
        onChange={(e) => setReason(e.target.value)}
      />
    </MutationForm>
  );
}
