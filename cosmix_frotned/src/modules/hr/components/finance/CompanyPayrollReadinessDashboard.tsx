import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { useState } from 'react';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import { useFinanceQuery } from '../../hooks/use-finance-queries';
import { financeError } from '../../api/finance-messages';
import {
  companyPayrollReadinessSchema,
  currentReadinessReport,
} from '../../schemas/payroll-readiness';

export const CompanyPayrollReadinessDashboard = () => {
  const [year, setYear] = useState<number>(new Date().getFullYear());
  const [month, setMonth] = useState<number>(new Date().getMonth() + 1);
  const [activeTab, setActiveTab] = useState<'blocking' | 'warnings' | 'coverage'>('blocking');

  const access = useFinanceAccess();
  const allowed = access.can('attendance.payroll-salary.read');
  const validPeriod = Number.isInteger(year) && year >= 1 && year <= 9998;
  const period = `${String(year).padStart(4, '0')}-${String(month).padStart(2, '0')}-01`;
  const query = useFinanceQuery(
    `/hr/payroll/salary/readiness?year=${year}&month=${month}`,
    companyPayrollReadinessSchema,
    allowed,
    validPeriod,
  );
  const report = currentReadinessReport(query, period, allowed && validPeriod);
  const loading = query.isFetching;
  const error = !validPeriod
    ? 'Enter a year between 1 and 9998.'
    : query.isError
      ? financeError(query.error)
      : null;
  if (!access.ready) return <p role="status">Loading access…</p>;
  if (!allowed)
    return <p role="alert">You do not have permission to view company payroll readiness.</p>;

  return (
    <div
      className="company-payroll-readiness"
      style={{ padding: '1.5rem', fontFamily: 'system-ui, sans-serif' }}
    >
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: '1.5rem',
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: '1.5rem', color: '#111827' }}>
            Company Payroll Configuration & Readiness Audit
          </h2>
          <p style={{ margin: '0.25rem 0 0 0', color: '#6b7280', fontSize: '0.875rem' }}>
            Authoritative pre-payroll master-data and policy verification. Confidential salary
            amounts remain masked.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
          <label style={{ fontSize: '0.875rem', fontWeight: 500, color: '#374151' }}>Period:</label>
          <select
            value={month}
            onChange={(e) => setMonth(Number(e.target.value))}
            style={{
              padding: '0.375rem 0.75rem',
              borderRadius: '0.375rem',
              border: '1px solid #d1d5db',
            }}
          >
            {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
              <option key={m} value={m}>
                {new Date(2000, m - 1).toLocaleString('default', { month: 'long' })}
              </option>
            ))}
          </select>
          <input
            type="number"
            aria-label="Payroll year"
            min={1}
            max={9998}
            value={year}
            onChange={(e) => setYear(Number(e.target.value))}
            style={{
              width: '80px',
              padding: '0.375rem 0.75rem',
              borderRadius: '0.375rem',
              border: '1px solid #d1d5db',
            }}
          />
          <button
            onClick={() => void query.refetch()}
            disabled={loading || !validPeriod}
            style={{
              padding: '0.375rem 1rem',
              backgroundColor: '#2563eb',
              color: '#ffffff',
              border: 'none',
              borderRadius: '0.375rem',
              cursor: 'pointer',
              fontWeight: 500,
            }}
          >
            {loading ? 'Evaluating...' : 'Refresh'}
          </button>
        </div>
      </div>

      {error && (
        <div
          style={{
            padding: '1rem',
            backgroundColor: '#fee2e2',
            color: '#991b1b',
            borderRadius: '0.5rem',
            marginBottom: '1.5rem',
          }}
        >
          <strong>Error:</strong> {error}
        </div>
      )}

      {loading && <p role="status">Evaluating the selected payroll period…</p>}
      {report && (
        <>
          {/* Status Alert Banner */}
          <div
            style={{
              padding: '1.25rem',
              borderRadius: '0.5rem',
              marginBottom: '1.5rem',
              backgroundColor: report.isReady ? '#f0fdf4' : '#fef2f2',
              border: `1px solid ${report.isReady ? '#bbf7d0' : '#fecaca'}`,
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
            }}
          >
            <div>
              <h3
                style={{
                  margin: 0,
                  fontSize: '1.125rem',
                  color: report.isReady ? '#166534' : '#991b1b',
                }}
              >
                {report.isReady
                  ? '✓ PAYROLL CONFIGURATION CHECKS PASSED'
                  : '⚠ BLOCKED: PAYROLL CONFIGURATION INCOMPLETE'}
              </h3>
              <p
                style={{
                  margin: '0.25rem 0 0 0',
                  fontSize: '0.875rem',
                  color: report.isReady ? '#15803d' : '#b91c1c',
                }}
              >
                {report.isReady
                  ? 'Configured salary, shift and Finance checks passed. Attendance month closure, independent approvals and payment checks still apply.'
                  : `Identified ${report.blockingIssues.length} blocking configuration issue(s) that must be resolved before payroll preparation.`}
              </p>
            </div>
            <div style={{ textAlign: 'right' }}>
              <span
                style={{
                  display: 'inline-block',
                  padding: '0.375rem 0.75rem',
                  fontSize: '0.75rem',
                  fontWeight: 700,
                  textTransform: 'uppercase',
                  borderRadius: '9999px',
                  backgroundColor: report.isReady ? '#22c55e' : '#ef4444',
                  color: '#ffffff',
                }}
              >
                {report.isReady ? 'Ready' : 'Action Required'}
              </span>
            </div>
          </div>

          {/* Metric KPI Cards */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
              gap: '1rem',
              marginBottom: '1.5rem',
            }}
          >
            <div
              style={{
                padding: '1rem',
                backgroundColor: '#ffffff',
                borderRadius: '0.5rem',
                border: '1px solid #e5e7eb',
              }}
            >
              <div
                style={{
                  color: '#6b7280',
                  fontSize: '0.75rem',
                  textTransform: 'uppercase',
                  fontWeight: 600,
                }}
              >
                Active Employees
              </div>
              <div
                style={{
                  fontSize: '1.75rem',
                  fontWeight: 700,
                  color: '#111827',
                  marginTop: '0.25rem',
                }}
              >
                {report.totalActiveEmployees}
              </div>
              <div style={{ fontSize: '0.75rem', color: '#10b981', marginTop: '0.25rem' }}>
                {report.readyEmployeesCount} Ready for Payroll
              </div>
            </div>

            <div
              style={{
                padding: '1rem',
                backgroundColor: '#ffffff',
                borderRadius: '0.5rem',
                border: '1px solid #e5e7eb',
              }}
            >
              <div
                style={{
                  color: '#6b7280',
                  fontSize: '0.75rem',
                  textTransform: 'uppercase',
                  fontWeight: 600,
                }}
              >
                Salary Structure Coverage
              </div>
              <div
                style={{
                  fontSize: '1.75rem',
                  fontWeight: 700,
                  color: '#111827',
                  marginTop: '0.25rem',
                }}
              >
                {report.coverage.employeesWithSalaryCount} / {report.totalActiveEmployees}
              </div>
              <div style={{ fontSize: '0.75rem', color: '#6b7280', marginTop: '0.25rem' }}>
                {report.totalActiveEmployees > 0
                  ? Math.round(
                      (report.coverage.employeesWithSalaryCount / report.totalActiveEmployees) *
                        100,
                    )
                  : 0}
                % Covered
              </div>
            </div>

            <div
              style={{
                padding: '1rem',
                backgroundColor: '#ffffff',
                borderRadius: '0.5rem',
                border: '1px solid #e5e7eb',
              }}
            >
              <div
                style={{
                  color: '#6b7280',
                  fontSize: '0.75rem',
                  textTransform: 'uppercase',
                  fontWeight: 600,
                }}
              >
                Shift / Roster Coverage
              </div>
              <div
                style={{
                  fontSize: '1.75rem',
                  fontWeight: 700,
                  color: '#111827',
                  marginTop: '0.25rem',
                }}
              >
                {report.coverage.employeesWithShiftCount} / {report.totalActiveEmployees}
              </div>
              <div style={{ fontSize: '0.75rem', color: '#6b7280', marginTop: '0.25rem' }}>
                {report.totalActiveEmployees > 0
                  ? Math.round(
                      (report.coverage.employeesWithShiftCount / report.totalActiveEmployees) * 100,
                    )
                  : 0}
                % Assigned
              </div>
            </div>

            <div
              style={{
                padding: '1rem',
                backgroundColor: '#ffffff',
                borderRadius: '0.5rem',
                border: '1px solid #e5e7eb',
              }}
            >
              <div
                style={{
                  color: '#6b7280',
                  fontSize: '0.75rem',
                  textTransform: 'uppercase',
                  fontWeight: 600,
                }}
              >
                Posting Profiles (Finance)
              </div>
              <div
                style={{
                  fontSize: '1.75rem',
                  fontWeight: 700,
                  color: '#111827',
                  marginTop: '0.25rem',
                }}
              >
                {report.coverage.activePostingProfilesCount}
              </div>
              <div
                style={{
                  fontSize: '0.75rem',
                  color: report.coverage.activePostingProfilesCount > 0 ? '#10b981' : '#ef4444',
                  marginTop: '0.25rem',
                }}
              >
                {report.coverage.activePostingProfilesCount > 0
                  ? 'Active & Approved'
                  : 'Missing Profile'}
              </div>
            </div>
          </div>

          {/* Tab Navigation */}
          <div
            style={{
              borderBottom: '1px solid #e5e7eb',
              marginBottom: '1rem',
              display: 'flex',
              gap: '1.5rem',
            }}
          >
            <button
              onClick={() => setActiveTab('blocking')}
              style={{
                padding: '0.5rem 0',
                border: 'none',
                background: 'none',
                cursor: 'pointer',
                fontWeight: activeTab === 'blocking' ? 600 : 400,
                color: activeTab === 'blocking' ? '#ef4444' : '#6b7280',
                borderBottom: activeTab === 'blocking' ? '2px solid #ef4444' : 'none',
              }}
            >
              Blocking Issues ({report.blockingIssues.length})
            </button>
            <button
              onClick={() => setActiveTab('warnings')}
              style={{
                padding: '0.5rem 0',
                border: 'none',
                background: 'none',
                cursor: 'pointer',
                fontWeight: activeTab === 'warnings' ? 600 : 400,
                color: activeTab === 'warnings' ? '#f59e0b' : '#6b7280',
                borderBottom: activeTab === 'warnings' ? '2px solid #f59e0b' : 'none',
              }}
            >
              Non-Blocking Warnings ({report.warnings.length})
            </button>
            <button
              onClick={() => setActiveTab('coverage')}
              style={{
                padding: '0.5rem 0',
                border: 'none',
                background: 'none',
                cursor: 'pointer',
                fontWeight: activeTab === 'coverage' ? 600 : 400,
                color: activeTab === 'coverage' ? '#2563eb' : '#6b7280',
                borderBottom: activeTab === 'coverage' ? '2px solid #2563eb' : 'none',
              }}
            >
              Configuration Coverage Summary
            </button>
          </div>

          {/* Tab Content */}
          {activeTab === 'blocking' && (
            <div
              style={{
                backgroundColor: '#ffffff',
                borderRadius: '0.5rem',
                border: '1px solid #e5e7eb',
                overflow: 'hidden',
              }}
            >
              {report.blockingIssues.length === 0 ? (
                <div style={{ padding: '2rem', textAlign: 'center', color: '#10b981' }}>
                  ✓ No blocking issues detected. System configuration is complete for this period.
                </div>
              ) : (
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
                  <thead style={{ backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb' }}>
                    <tr>
                      <th style={{ padding: '0.75rem 1rem', textAlign: 'left', color: '#4b5563' }}>
                        Category
                      </th>
                      <th style={{ padding: '0.75rem 1rem', textAlign: 'left', color: '#4b5563' }}>
                        Code
                      </th>
                      <th style={{ padding: '0.75rem 1rem', textAlign: 'left', color: '#4b5563' }}>
                        Employee / Entity
                      </th>
                      <th style={{ padding: '0.75rem 1rem', textAlign: 'left', color: '#4b5563' }}>
                        Message & Required Action
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.blockingIssues.map((issue, idx) => (
                      <tr key={idx} style={{ borderBottom: '1px solid #f3f4f6' }}>
                        <td style={{ padding: '0.75rem 1rem', fontWeight: 600 }}>
                          {issue.category}
                        </td>
                        <td
                          style={{
                            padding: '0.75rem 1rem',
                            fontFamily: 'monospace',
                            color: '#ef4444',
                          }}
                        >
                          {issue.code}
                        </td>
                        <td style={{ padding: '0.75rem 1rem' }}>
                          {issue.employeeId || issue.employeeCode ? (
                            <EmployeeReference
                              employeeId={issue.employeeId}
                              employeeNumber={issue.employeeCode}
                              employeeName={issue.employeeName}
                              showName
                            />
                          ) : (
                            'System Wide'
                          )}
                        </td>
                        <td style={{ padding: '0.75rem 1rem', color: '#374151' }}>
                          {issue.message}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          )}

          {activeTab === 'warnings' && (
            <div
              style={{
                backgroundColor: '#ffffff',
                borderRadius: '0.5rem',
                border: '1px solid #e5e7eb',
                overflow: 'hidden',
              }}
            >
              {report.warnings.length === 0 ? (
                <div style={{ padding: '2rem', textAlign: 'center', color: '#6b7280' }}>
                  No warnings recorded for this period.
                </div>
              ) : (
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
                  <thead style={{ backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb' }}>
                    <tr>
                      <th style={{ padding: '0.75rem 1rem', textAlign: 'left', color: '#4b5563' }}>
                        Category
                      </th>
                      <th style={{ padding: '0.75rem 1rem', textAlign: 'left', color: '#4b5563' }}>
                        Code
                      </th>
                      <th style={{ padding: '0.75rem 1rem', textAlign: 'left', color: '#4b5563' }}>
                        Employee / Entity
                      </th>
                      <th style={{ padding: '0.75rem 1rem', textAlign: 'left', color: '#4b5563' }}>
                        Message
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.warnings.map((warn, idx) => (
                      <tr key={idx} style={{ borderBottom: '1px solid #f3f4f6' }}>
                        <td style={{ padding: '0.75rem 1rem', fontWeight: 600 }}>
                          {warn.category}
                        </td>
                        <td
                          style={{
                            padding: '0.75rem 1rem',
                            fontFamily: 'monospace',
                            color: '#f59e0b',
                          }}
                        >
                          {warn.code}
                        </td>
                        <td style={{ padding: '0.75rem 1rem' }}>
                          {warn.employeeId || warn.employeeCode ? (
                            <EmployeeReference
                              employeeId={warn.employeeId}
                              employeeNumber={warn.employeeCode}
                              employeeName={warn.employeeName}
                              showName
                            />
                          ) : (
                            'System Wide'
                          )}
                        </td>
                        <td style={{ padding: '0.75rem 1rem', color: '#374151' }}>
                          {warn.message}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          )}

          {activeTab === 'coverage' && (
            <div
              style={{
                backgroundColor: '#ffffff',
                padding: '1.5rem',
                borderRadius: '0.5rem',
                border: '1px solid #e5e7eb',
              }}
            >
              <h4 style={{ margin: '0 0 1rem 0', fontSize: '1rem', color: '#111827' }}>
                Configuration Coverage Breakdown
              </h4>
              <ul
                style={{
                  listStyle: 'none',
                  padding: 0,
                  margin: 0,
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '0.75rem',
                }}
              >
                <li
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    padding: '0.5rem 0',
                    borderBottom: '1px solid #f3f4f6',
                  }}
                >
                  <span>Employees with Approved Base Compensation</span>
                  <strong>{report.coverage.employeesWithSalaryCount}</strong>
                </li>
                <li
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    padding: '0.5rem 0',
                    borderBottom: '1px solid #f3f4f6',
                  }}
                >
                  <span>Employees with Active Shift/Roster Assignment</span>
                  <strong>{report.coverage.employeesWithShiftCount}</strong>
                </li>
                <li
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    padding: '0.5rem 0',
                    borderBottom: '1px solid #f3f4f6',
                  }}
                >
                  <span>Employees with Verified Bank Details</span>
                  <strong>{report.coverage.employeesWithBankCount}</strong>
                </li>
                <li
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    padding: '0.5rem 0',
                    borderBottom: '1px solid #f3f4f6',
                  }}
                >
                  <span>Employees with Statutory Coverage (EOBI / PESSI)</span>
                  <strong>{report.coverage.employeesWithStatutoryCount}</strong>
                </li>
                <li
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    padding: '0.5rem 0',
                    borderBottom: '1px solid #f3f4f6',
                  }}
                >
                  <span>Active Posting Profiles</span>
                  <strong>{report.coverage.activePostingProfilesCount}</strong>
                </li>
                <li
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    padding: '0.5rem 0',
                    borderBottom: '1px solid #f3f4f6',
                  }}
                >
                  <span>Active Ledger Accounts</span>
                  <strong>{report.coverage.activeCostCentersCount}</strong>
                </li>
                <li
                  style={{ display: 'flex', justifyContent: 'space-between', padding: '0.5rem 0' }}
                >
                  <span>Pending Policy Approvals</span>
                  <strong
                    style={{
                      color:
                        report.coverage.pendingPolicyApprovalsCount > 0 ? '#f59e0b' : '#10b981',
                    }}
                  >
                    {report.coverage.pendingPolicyApprovalsCount}
                  </strong>
                </li>
              </ul>
            </div>
          )}
        </>
      )}
    </div>
  );
};
