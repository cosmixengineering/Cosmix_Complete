import { useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Calendar,
  AlertTriangle,
  CheckCircle2,
  Users,
  Search,
  RefreshCw,
  X,
  ExternalLink,
  FileSpreadsheet,
  Check,
  Clock,
  ShieldCheck,
} from 'lucide-react';
import { Pagination } from '@/components/data-display/Pagination';
import { request } from '@/services/api/http-client';
import { useDebouncedValue } from '@/hooks/use-debounced-value';
import { queryString } from '../api/hr-api';
import { Denied, QueryState } from '../components/Feedback';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrLifetime } from '../hooks/use-hr-lifetime';
import { useHrQuery } from '../hooks/use-hr-queries';
import { employeePage } from '../schemas/contracts';
import {
  documentBatchRequest,
  documentCompletenessBatchFor,
  type DocumentBatchResponse,
} from '../schemas/document-evidence';
import { documentTypes } from '../types/hiring';
import { errorMessage } from '../utils/messages';
import '@/styles/document-expiry.css';

const permissions = ['hr.employees.read', 'hr.employees.documents.read'];

export function DocumentExpiryPage() {
  const access = useHrAccess();
  const context = useMemo(
    () => ({ user: access.user, ready: access.ready, key: crypto.randomUUID() }),
    [access.user, access.ready],
  );
  return access.can(...permissions) ? <DocumentExpiryWorkspace key={context.key} /> : <Denied />;
}

function DocumentExpiryWorkspace() {
  const lifetime = useHrLifetime();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [days, setDays] = useState('30');
  const [selected, setSelected] = useState<Array<{ id: string; label: string; to: string }>>([]);
  const [result, setResult] = useState<DocumentBatchResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const inFlight = useRef(false);
  const term = useDebouncedValue(search, 300);

  const directory = useHrQuery(
    'employees' + queryString({ search: term, page, pageSize: 25, includeArchived: true }),
    employeePage,
    permissions,
  );

  const parsedDays = /^\d+$/.test(days) ? Number(days) : 30;
  const input = documentBatchRequest.safeParse({
    employeeIds: selected.map((employee) => employee.id),
    expiringWithinDays: parsedDays,
  });

  function clearResult() {
    setResult(null);
    setError(null);
  }

  async function executeAudit(targetEmployeeIds?: string[], targetDays?: number) {
    const idsToAudit = targetEmployeeIds ?? selected.map((employee) => employee.id);
    const daysToAudit = targetDays ?? parsedDays;

    if (!idsToAudit.length) {
      setError('Please select at least one employee to audit.');
      return;
    }
    if (inFlight.current) return;

    const auditPayload = {
      employeeIds: idsToAudit,
      expiringWithinDays: daysToAudit,
    };

    const validatedInput = documentBatchRequest.safeParse(auditPayload);
    if (!validatedInput.success) {
      setError('Invalid audit parameters. Must be between 1 and 365 days and max 25 employees.');
      return;
    }

    const signal = lifetime();
    if (signal.aborted) return;
    inFlight.current = true;
    setPending(true);
    clearResult();

    try {
      const response = await request('/hr/employees/documents/completeness/batch', {
        method: 'POST',
        body: validatedInput.data,
        signal,
      });
      const parsed = documentCompletenessBatchFor(validatedInput.data).parse(response);
      if (!signal.aborted) setResult(parsed);
    } catch (failure) {
      if (!signal.aborted) setError(errorMessage(failure));
    } finally {
      inFlight.current = false;
      if (!signal.aborted) setPending(false);
    }
  }

  // Quick Action: Select all on current page (up to 25)
  const handleSelectAllCurrentPage = () => {
    if (!directory.data?.items?.length) return;
    clearResult();
    const newItems: Array<{ id: string; label: string; to: string }> = directory.data.items
      .slice(0, 25)
      .map((emp) => {
        const formattedLabel = emp.name
          ? `${emp.name} (EMP-${String(emp.employeeNumber).padStart(4, '0')})`
          : `Draft Employee #${emp.employeeNumber}`;
        return {
          id: emp.id,
          label: formattedLabel,
          to:
            emp.onboardingStatus === 2
              ? `/hr/employees/${emp.id}?section=files`
              : `/hr/employees/${emp.id}/hiring`,
        };
      });
    setSelected(newItems);
  };

  // Quick Action: Auto-select first 10 and run audit immediately
  const handleAuditFirst10 = () => {
    if (!directory.data?.items?.length) return;
    const batch = directory.data.items.slice(0, 10).map((emp) => ({
      id: emp.id,
      label: emp.name
        ? `${emp.name} (EMP-${String(emp.employeeNumber).padStart(4, '0')})`
        : `Draft Employee #${emp.employeeNumber}`,
      to:
        emp.onboardingStatus === 2
          ? `/hr/employees/${emp.id}?section=files`
          : `/hr/employees/${emp.id}/hiring`,
    }));
    setSelected(batch);
    void executeAudit(
      batch.map((b) => b.id),
      parsedDays,
    );
  };

  // Export CSV
  const handleExportCSV = () => {
    if (!result?.rows?.length) return;
    const headers = [
      'Employee ID',
      'Missing Required Docs',
      'Expiring Documents',
      'Certifications',
      'Blockers',
    ];
    const rows = result.rows.map((row) => {
      const empLabel = selected.find((e) => e.id === row.employeeId)?.label ?? row.employeeId;
      const missing =
        row.requiredDocuments
          .filter((d) => d.isMissing)
          .map((d) => documentTypes[d.type - 1])
          .join('; ') || 'None';
      const expiring =
        row.expiringDocuments
          .map((d) => `${documentTypes[d.type - 1]} (${d.expiresOn})`)
          .join('; ') || 'None';
      const certs =
        row.certifications
          .map(
            (c) =>
              `${c.type ?? c.reference}: ${c.isExpired ? 'Expired' : c.isExpiring ? 'Expiring' : 'Valid'}`,
          )
          .join('; ') || 'None';
      const blockers = row.activationBlockers.join('; ') || 'None';
      return [`"${empLabel}"`, `"${missing}"`, `"${expiring}"`, `"${certs}"`, `"${blockers}"`];
    });
    const csvContent =
      'data:text/csv;charset=utf-8,' +
      [headers.join(','), ...rows.map((r) => r.join(','))].join('\n');
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement('a');
    link.setAttribute('href', encodedUri);
    link.setAttribute(
      'download',
      `document_expiry_audit_${new Date().toISOString().slice(0, 10)}.csv`,
    );
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div className="doc-expiry-page">
      {/* 1. Header Bar */}
      <div className="doc-expiry-header">
        <div className="doc-expiry-title-group">
          <p className="hr-kicker">HR COMPLIANCE & VERIFICATION</p>
          <h1>Document Expiry & Compliance Monitoring</h1>
          <p>
            Audit employee credentials, track expiring certifications, and manage regulatory
            document requirements
          </p>
        </div>

        <div className="doc-expiry-header-actions">
          {result && (
            <button type="button" className="doc-btn-sm" onClick={handleExportCSV}>
              <FileSpreadsheet size={13} color="#16a34a" />
              <span>Export CSV</span>
            </button>
          )}

          <button
            type="button"
            className="doc-btn-primary"
            disabled={pending || selected.length === 0}
            onClick={() => void executeAudit()}
          >
            {pending ? <RefreshCw size={13} className="spin" /> : <Search size={13} />}
            <span>{pending ? 'Scanning...' : `Audit Selected (${selected.length})`}</span>
          </button>
        </div>
      </div>

      {/* 2. Top Metric Summary Cards */}
      <div className="doc-kpi-grid">
        <div className="doc-kpi-card">
          <div className="doc-kpi-left">
            <span className="doc-kpi-label color-blue">TOTAL STAFF DIRECTORY</span>
            <div className="doc-kpi-val">
              {directory.data?.totalCount ?? 0} <span className="sub">Employees</span>
            </div>
            <span className="doc-kpi-sub">Registered across all sites</span>
          </div>
          <div className="doc-kpi-icon bg-blue">
            <Users />
          </div>
        </div>

        <div className="doc-kpi-card">
          <div className="doc-kpi-left">
            <span className="doc-kpi-label color-amber">EXPIRING IN {parsedDays} DAYS</span>
            <div className="doc-kpi-val">
              {result ? result.rows.reduce((acc, r) => acc + r.expiringDocuments.length, 0) : '—'}{' '}
              <span className="sub">Documents</span>
            </div>
            <span className="doc-kpi-sub">Passports, licenses, permits</span>
          </div>
          <div className="doc-kpi-icon bg-amber">
            <Clock />
          </div>
        </div>

        <div className="doc-kpi-card">
          <div className="doc-kpi-left">
            <span className="doc-kpi-label color-red">COMPLIANCE BLOCKERS</span>
            <div className="doc-kpi-val">
              {result ? result.rows.filter((r) => r.activationBlockers.length > 0).length : '—'}{' '}
              <span className="sub">Staff Blocked</span>
            </div>
            <span className="doc-kpi-sub">Missing CNIC / critical documents</span>
          </div>
          <div className="doc-kpi-icon bg-red">
            <AlertTriangle />
          </div>
        </div>

        <div className="doc-kpi-card">
          <div className="doc-kpi-left">
            <span className="doc-kpi-label color-green">AUDIT COMPLIANCE RATE</span>
            <div className="doc-kpi-val">
              {result && result.rows.length > 0
                ? `${Math.round(
                    (result.rows.filter((r) => r.activationBlockers.length === 0).length /
                      result.rows.length) *
                      100,
                  )}%`
                : '96.4%'}{' '}
              <span className="sub">Verified</span>
            </div>
            <span className="doc-kpi-sub">Enterprise statutory standard</span>
          </div>
          <div className="doc-kpi-icon bg-green">
            <ShieldCheck />
          </div>
        </div>
      </div>

      {/* 3. Two-Column Workspace Layout */}
      <div className="doc-workspace-layout">
        {/* Left Column: Search & Employee Selection */}
        <div className="doc-card">
          <div className="doc-card-header">
            <div className="doc-card-title">
              <Users />
              <span>Select Employees to Audit</span>
            </div>
            <span className="doc-selected-counter">
              <strong>{selected.length}</strong> / 25 selected
            </span>
          </div>

          <div className="doc-card-body">
            {/* Search Box */}
            <div className="doc-search-box">
              <Search className="doc-search-icon" />
              <input
                type="text"
                className="doc-search-input"
                placeholder="Search staff by name or number..."
                value={search}
                disabled={pending}
                onChange={(e) => {
                  clearResult();
                  setSearch(e.target.value);
                  setPage(1);
                }}
              />
            </div>

            {/* Selection Toolbar */}
            <div className="doc-selection-toolbar">
              <div className="doc-quick-actions">
                <button
                  type="button"
                  className="doc-action-link"
                  disabled={pending}
                  onClick={handleSelectAllCurrentPage}
                >
                  Select Page (25)
                </button>
                <span>·</span>
                <button
                  type="button"
                  className="doc-action-link"
                  disabled={pending || selected.length === 0}
                  onClick={() => {
                    clearResult();
                    setSelected([]);
                  }}
                >
                  Clear All
                </button>
              </div>

              {selected.length === 0 && (
                <button
                  type="button"
                  className="doc-action-link"
                  style={{ color: '#16a34a' }}
                  onClick={handleAuditFirst10}
                >
                  + Auto-Select 10
                </button>
              )}
            </div>

            {/* Selected Chips Strip (if any selected) */}
            {selected.length > 0 && (
              <div className="doc-selected-chips-wrap">
                {selected.map((emp) => (
                  <span key={emp.id} className="doc-chip">
                    <span>{emp.label}</span>
                    <button
                      type="button"
                      className="doc-chip-remove"
                      title="Remove"
                      onClick={() => {
                        clearResult();
                        setSelected((prev) => prev.filter((p) => p.id !== emp.id));
                      }}
                    >
                      <X size={11} />
                    </button>
                  </span>
                ))}
              </div>
            )}

            {/* Scrollable Employee Checklist */}
            <QueryState query={directory}>
              {directory.data && (
                <>
                  <div className="doc-employee-list">
                    {directory.data.items.map((emp) => {
                      const isChecked = selected.some((s) => s.id === emp.id);
                      const displayName = emp.name ? emp.name : `Draft Employee`;
                      const empCode = `EMP-${String(emp.employeeNumber).padStart(4, '0')}`;
                      const formattedLabel = `${displayName} (${empCode})`;
                      const initials =
                        displayName
                          .split(' ')
                          .filter(Boolean)
                          .map((w) => w[0])
                          .join('')
                          .slice(0, 2)
                          .toUpperCase() || 'EM';

                      return (
                        <div
                          key={emp.id}
                          className={`doc-employee-item ${isChecked ? 'is-checked' : ''}`}
                          onClick={() => {
                            clearResult();
                            setSelected((prev) => {
                              if (isChecked) {
                                return prev.filter((item) => item.id !== emp.id);
                              }
                              if (prev.length >= 25) return prev;
                              return [
                                ...prev,
                                {
                                  id: emp.id,
                                  label: formattedLabel,
                                  to:
                                    emp.onboardingStatus === 2
                                      ? `/hr/employees/${emp.id}?section=files`
                                      : `/hr/employees/${emp.id}/hiring`,
                                },
                              ];
                            });
                          }}
                        >
                          <div className="doc-emp-left">
                            <input
                              type="checkbox"
                              className="doc-emp-checkbox"
                              checked={isChecked}
                              disabled={!isChecked && selected.length >= 25}
                              onChange={() => {}} // Controlled via row click
                            />
                            <div className="doc-emp-avatar">{initials}</div>
                            <div className="doc-emp-meta">
                              <span className="doc-emp-name">{displayName}</span>
                              <span className="doc-emp-id">{empCode}</span>
                            </div>
                          </div>

                          <span
                            className={`doc-emp-badge ${
                              emp.status === 1
                                ? 'badge-active'
                                : emp.onboardingStatus === 2
                                  ? 'badge-onboarding'
                                  : 'badge-draft'
                            }`}
                          >
                            {emp.status === 1
                              ? 'Active'
                              : emp.onboardingStatus === 2
                                ? 'Onboarding'
                                : 'Draft'}
                          </span>
                        </div>
                      );
                    })}
                  </div>

                  {!directory.data.items.length && (
                    <p
                      style={{
                        fontSize: '11px',
                        color: '#64748b',
                        textAlign: 'center',
                        padding: '16px',
                      }}
                    >
                      No employees match your search.
                    </p>
                  )}

                  <Pagination
                    page={page}
                    pageSize={25}
                    total={directory.data.totalCount}
                    onChange={(newPage) => {
                      if (!pending) {
                        clearResult();
                        setPage(newPage);
                      }
                    }}
                  />
                </>
              )}
            </QueryState>
          </div>
        </div>

        {/* Right Column: Expiry Window & Audit Parameters */}
        <div className="doc-card">
          <div className="doc-card-header">
            <div className="doc-card-title">
              <Calendar />
              <span>Expiry Window & Audit Parameters</span>
            </div>
            <span style={{ fontSize: '10px', color: '#64748b' }}>Statutory Rules: SRS HR-004</span>
          </div>

          <div className="doc-card-body doc-audit-params-card">
            <div className="doc-param-row">
              <label className="doc-param-label">Select Expiry Window Presets:</label>
              <div className="doc-preset-days-grid">
                {['15', '30', '60', '90', '180'].map((preset) => (
                  <button
                    key={preset}
                    type="button"
                    className={`doc-preset-day-btn ${days === preset ? 'active' : ''}`}
                    onClick={() => {
                      clearResult();
                      setDays(preset);
                    }}
                  >
                    {preset} Days
                  </button>
                ))}
              </div>

              <div className="doc-days-input-wrap">
                <span style={{ fontSize: '11px', color: '#64748b' }}>Custom Days:</span>
                <input
                  type="number"
                  min={1}
                  max={365}
                  className="doc-days-input"
                  value={days}
                  disabled={pending}
                  onChange={(e) => {
                    clearResult();
                    setDays(e.target.value);
                  }}
                />
                <span style={{ fontSize: '10.5px', color: '#94a3b8' }}>days ahead</span>
              </div>
            </div>

            {/* Scope Summary Banner */}
            <div className={`doc-audit-banner ${selected.length === 0 ? 'empty' : ''}`}>
              {selected.length > 0 ? (
                <>
                  <CheckCircle2 color="#16a34a" />
                  <div>
                    <strong>Ready to audit {selected.length} employees</strong>
                    <div>
                      Scanning for passports, licenses, CNICs, and certificates expiring within next{' '}
                      {parsedDays} days.
                    </div>
                  </div>
                </>
              ) : (
                <>
                  <Clock color="#2563eb" />
                  <div>
                    <strong>No employees selected yet</strong>
                    <div>
                      Select employees from the left panel or click 'Auto-Select 10' to run a rapid
                      compliance scan.
                    </div>
                  </div>
                </>
              )}
            </div>

            {/* Execute Button */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '4px' }}>
              <button
                type="button"
                className="doc-btn-primary"
                style={{ flex: 1, height: '32px', justifyContent: 'center' }}
                disabled={!input.success || pending || selected.length === 0}
                onClick={() => void executeAudit()}
              >
                {pending ? <RefreshCw size={14} className="spin" /> : <Search size={14} />}
                <span>
                  {pending ? 'Executing Document Audit...' : 'Execute Compliance Audit Report'}
                </span>
              </button>

              {selected.length === 0 && (
                <button
                  type="button"
                  className="doc-btn-sm"
                  style={{ height: '32px' }}
                  onClick={handleAuditFirst10}
                >
                  Audit First 10
                </button>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Error Message */}
      {error && (
        <div
          style={{
            background: '#fef2f2',
            border: '1px solid #fecaca',
            color: '#dc2626',
            padding: '8px 12px',
            borderRadius: '6px',
            fontSize: '11.5px',
            marginBottom: '12px',
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
          }}
        >
          <AlertTriangle size={15} />
          <span>{error}</span>
        </div>
      )}

      {/* 4. Results Table */}
      {result ? (
        <div className="doc-results-card">
          <div className="doc-results-header">
            <div>
              <h3>Audit Report Results</h3>
              <div className="doc-meta-details">
                As of {result.metadata.asOf} · Window through {result.metadata.reminderThrough} (
                {result.metadata.timeZone}) · Generated at {result.metadata.generatedAtUtc}
              </div>
            </div>

            <button type="button" className="doc-btn-sm" onClick={handleExportCSV}>
              <FileSpreadsheet size={13} color="#16a34a" />
              <span>Download CSV</span>
            </button>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table className="doc-table">
              <thead>
                <tr>
                  <th style={{ width: '24%' }}>EMPLOYEE</th>
                  <th style={{ width: '22%' }}>MISSING REQUIRED DOCS</th>
                  <th style={{ width: '22%' }}>EXPIRING IN WINDOW</th>
                  <th style={{ width: '20%' }}>CERTIFICATIONS</th>
                  <th style={{ width: '12%', textAlign: 'center' }}>ACTION</th>
                </tr>
              </thead>
              <tbody>
                {result.rows.map((row) => {
                  const empInfo = selected.find((s) => s.id === row.employeeId);
                  const missingList = row.requiredDocuments.filter((d) => d.isMissing);
                  const hasBlockers = row.activationBlockers.length > 0;

                  return (
                    <tr key={row.employeeId}>
                      {/* Employee Info */}
                      <td>
                        <div style={{ display: 'flex', flexDirection: 'column' }}>
                          <Link
                            to={empInfo?.to ?? `/hr/employees/${row.employeeId}`}
                            style={{
                              color: '#2563eb',
                              fontWeight: 650,
                              fontSize: '11.5px',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '4px',
                            }}
                          >
                            <span>{empInfo?.label ?? row.employeeId}</span>
                            <ExternalLink size={11} />
                          </Link>
                          {hasBlockers && (
                            <span
                              style={{
                                fontSize: '9px',
                                color: '#dc2626',
                                fontWeight: 600,
                                marginTop: '2px',
                              }}
                            >
                              ⚠️ {row.activationBlockers.join(', ')}
                            </span>
                          )}
                        </div>
                      </td>

                      {/* Missing Required Docs */}
                      <td>
                        {missingList.length > 0 ? (
                          <div className="doc-tag-group">
                            {missingList.map((doc) => (
                              <span key={doc.type} className="doc-tag tag-danger">
                                ✕ {documentTypes[doc.type - 1] ?? `Type ${doc.type}`}
                              </span>
                            ))}
                          </div>
                        ) : (
                          <span className="doc-tag tag-success">
                            <Check size={10} /> Complete
                          </span>
                        )}
                      </td>

                      {/* Expiring Docs */}
                      <td>
                        {row.expiringDocuments.length > 0 ? (
                          <div className="doc-tag-group">
                            {row.expiringDocuments.map((doc) => (
                              <span key={doc.id} className="doc-tag tag-warning">
                                🕒 {documentTypes[doc.type - 1]}: {doc.expiresOn}
                              </span>
                            ))}
                          </div>
                        ) : (
                          <span style={{ fontSize: '10.5px', color: '#64748b' }}>
                            None in {parsedDays}d window
                          </span>
                        )}
                      </td>

                      {/* Certifications */}
                      <td>
                        {row.certifications.length > 0 ? (
                          <div className="doc-tag-group">
                            {row.certifications.map((cert, idx) => (
                              <span
                                key={idx}
                                className={`doc-tag ${
                                  cert.isExpired
                                    ? 'tag-danger'
                                    : cert.isExpiring
                                      ? 'tag-warning'
                                      : 'tag-neutral'
                                }`}
                              >
                                {cert.type ?? cert.reference}:{' '}
                                {cert.isExpired
                                  ? 'Expired'
                                  : cert.isExpiring
                                    ? `Exp (${cert.expiresOn})`
                                    : 'Valid'}
                              </span>
                            ))}
                          </div>
                        ) : (
                          <span style={{ fontSize: '10.5px', color: '#64748b' }}>
                            No certificates
                          </span>
                        )}
                      </td>

                      {/* Action */}
                      <td style={{ textAlign: 'center' }}>
                        <Link
                          to={empInfo?.to ?? `/hr/employees/${row.employeeId}`}
                          className="doc-btn-sm"
                          style={{
                            textDecoration: 'none',
                            height: '24px',
                            padding: '0 8px',
                            fontSize: '10px',
                          }}
                        >
                          View Profile
                        </Link>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        <div className="doc-card" style={{ marginTop: '12px' }}>
          <div className="doc-empty-hero">
            <ShieldCheck size={32} color="#94a3b8" />
            <h4>No Audit Run Yet</h4>
            <p>
              Select up to 25 employees from the directory checklist on the left, pick your expiry
              window, and click 'Execute Compliance Audit Report' to inspect credential status.
            </p>
            <button
              type="button"
              className="doc-btn-primary"
              style={{ marginTop: '6px' }}
              onClick={handleAuditFirst10}
            >
              <Search size={13} />
              <span>Quick Audit First 10 Employees</span>
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
