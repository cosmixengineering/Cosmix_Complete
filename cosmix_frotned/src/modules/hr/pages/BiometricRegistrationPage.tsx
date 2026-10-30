import { employeeSearchTerm, formatEmployeeCode } from '@/utils/employee-code';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Camera, Fingerprint, Search, ShieldCheck, ShieldAlert, X } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Pagination } from '@/components/data-display/Pagination';
import { queryString } from '../api/hr-api';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { employeePage, type EmployeeSummary } from '../schemas/contracts';
import { Denied, QueryState } from '../components/Feedback';
import { EmployeeBiometricSection } from '../components/employee/EmployeeBiometricSection';
import { FaceCameraRegistration } from '../components/employee/FaceCameraRegistration';
import '@/styles/biometric-registration.css';

export function BiometricRegistrationPage() {
  const access = useHrAccess();
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<EmployeeSummary | null>(null);
  const grants = (access.user?.grants ?? [])
    .map((g) => `${g.permission}:${g.scopeKind}:${g.targetId}:${g.roleAssignmentId}`)
    .sort()
    .join('|');
  const canUse =
    access.ready &&
    access.user?.grants.some(
      (g) => g.permission === 'attendance.face.manage' || g.permission === 'attendance.face.read',
    );
  const employees = useHrQuery(
    `employees${queryString({ page, pageSize: 10, search: employeeSearchTerm(term) })}`,
    employeePage,
    ['hr.employees.read'],
    Boolean(canUse),
  );
  if (!access.can('hr.employees.read') || !canUse) return <Denied />;

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(1);
    setTerm(search.trim());
  };

  const handleClearSearch = () => {
    setSearch('');
    setTerm('');
    setPage(1);
  };

  return (
    <div className="bioreg">
      <header className="bioreg-heading">
        <div>
          <p className="hr-kicker">HUMAN RESOURCES</p>
          <h1>Biometric Registration</h1>
          <p>Find an employee, capture their face and register their attendance identity.</p>
        </div>
        <Fingerprint size={20} className="bioreg-heading-icon" aria-hidden="true" />
      </header>

      <div className="bioreg-notice">
        <ShieldCheck size={15} aria-hidden="true" />
        <p>
          Complete hiring approval and mobile device registration first. Face registration does not
          approve an employee or mark attendance.
        </p>
      </div>

      <div className="bioreg-layout">
        <section className="bioreg-panel bioreg-sidebar" aria-label="Find employee">
          <div className="bioreg-sidebar-header">
            <h2>Find Employee</h2>
            {employees.data && (
              <span className="bioreg-count-badge">{employees.data.totalCount} total</span>
            )}
          </div>

          <form className="bioreg-search-form" onSubmit={handleSearchSubmit}>
            <div className="bioreg-search-box">
              <Search size={13} className="bioreg-search-icon" aria-hidden="true" />
              <input
                type="text"
                className="bioreg-search-input"
                placeholder="Search name or EMP ID..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                maxLength={150}
              />
              {search && (
                <button
                  type="button"
                  className="bioreg-search-clear"
                  onClick={handleClearSearch}
                  aria-label="Clear search"
                >
                  <X size={12} />
                </button>
              )}
            </div>
            <button type="submit" className="bioreg-search-btn">
              Search
            </button>
          </form>

          {employees.isError ? (
            <div className="bioreg-notice bioreg-notice--amber" role="alert">
              <div>
                <p>Employee search could not load. Check API connection.</p>
                <Button variant="secondary" onClick={() => void employees.refetch()}>
                  Retry search
                </Button>
              </div>
            </div>
          ) : (
            <QueryState query={employees}>
              {employees.data && (
                <>
                  <ul className="bioreg-people">
                    {employees.data.items.map((employee) => {
                      const isApproved = employee.onboardingStatus === 2;
                      return (
                        <li key={employee.id}>
                          <button
                            type="button"
                            className={`bioreg-employee-item ${selected?.id === employee.id ? 'is-selected' : ''}`}
                            aria-pressed={selected?.id === employee.id}
                            onClick={() => setSelected(employee)}
                          >
                            <div className="bioreg-employee-info">
                              <span
                                className="bioreg-employee-name"
                                title={employee.name || 'Unnamed employee'}
                              >
                                {employee.name || 'Unnamed employee'}
                              </span>
                              <span className="bioreg-employee-meta">
                                {formatEmployeeCode(employee.employeeNumber)}
                              </span>
                            </div>
                            <span
                              className={`bio-status-chip ${isApproved ? 'bio-status-chip--approved' : 'bio-status-chip--pending'}`}
                            >
                              {isApproved ? 'Approved' : 'Pending'}
                            </span>
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                  {employees.data.items.length === 0 && (
                    <p className="bioreg-empty-list">No employees match this search.</p>
                  )}
                  <Pagination
                    page={page}
                    pageSize={10}
                    total={employees.data.totalCount}
                    onChange={setPage}
                  />
                </>
              )}
            </QueryState>
          )}
        </section>

        <section className="bioreg-panel bioreg-main-panel" aria-label="Camera registration">
          {selected ? (
            <EmployeeRegistration
              key={`${access.subject}:${grants}:${selected.id}`}
              employee={selected}
            />
          ) : (
            <div className="bioreg-empty">
              <div className="bioreg-empty-icon">
                <Camera size={22} aria-hidden="true" />
              </div>
              <h3>Select an employee</h3>
              <p>
                Choose an employee from the directory on the left to review verified devices and
                register face attendance.
              </p>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

function EmployeeRegistration({ employee }: { employee: EmployeeSummary }) {
  const access = useHrAccess();
  const [generation, setGeneration] = useState(0);
  const [historyOpen, setHistoryOpen] = useState(false);
  const canManage = access.user?.grants.some((g) => g.permission === 'attendance.face.manage');
  const canRead = access.user?.grants.some((g) => g.permission === 'attendance.face.read');
  const isApproved = employee.onboardingStatus === 2;

  return (
    <div className="bioreg-employee-detail-card">
      <header className="bioreg-person">
        <div className="bioreg-person-info">
          <h2>{employee.name || 'Employee'}</h2>
          <div className="bioreg-person-badge">
            <span className="bioreg-empid-pill">{formatEmployeeCode(employee.employeeNumber)}</span>
            <span
              className={`bio-status-chip ${isApproved ? 'bio-status-chip--approved' : 'bio-status-chip--pending'}`}
            >
              {isApproved ? 'Hiring Approved' : 'Hiring Not Approved'}
            </span>
          </div>
        </div>
        <Link to={`/hr/employees/${employee.id}`} className="bioreg-details-link">
          Employee details →
        </Link>
      </header>

      {!isApproved ? (
        <div className="bioreg-notice bioreg-notice--amber">
          <ShieldAlert size={15} aria-hidden="true" />
          <p>
            Hiring approval is required before face registration. Complete the employee's hiring
            review first in Employee Master.
          </p>
        </div>
      ) : !canManage || !canRead ? (
        <div className="bioreg-notice bioreg-notice--amber">
          <ShieldAlert size={15} aria-hidden="true" />
          <p>
            Registration requires scoped face-management and face-read access. Contact your access
            administrator.
          </p>
        </div>
      ) : (
        <FaceCameraRegistration
          key={generation}
          employeeId={employee.id}
          onReset={() => setGeneration((g) => g + 1)}
        />
      )}

      <details
        className="bioreg-history"
        onToggle={(e) => setHistoryOpen((e.target as HTMLDetailsElement).open)}
      >
        <summary>
          <span>Enrollment & Verification History</span>
        </summary>
        {historyOpen && (
          <div className="bioreg-history-content">
            <EmployeeBiometricSection
              key={`${employee.id}:${generation}`}
              employeeId={employee.id}
            />
          </div>
        )}
      </details>
    </div>
  );
}
