import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Camera, Fingerprint, Search, ShieldCheck } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
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
    `employees${queryString({ page, pageSize: 10, search: term })}`,
    employeePage,
    ['hr.employees.read'],
    Boolean(canUse),
  );
  if (!access.can('hr.employees.read') || !canUse) return <Denied />;
  return (
    <main className="bioreg">
      <header className="bioreg-heading">
        <div>
          <p className="hr-kicker">HUMAN RESOURCES</p>
          <h1>Biometric registration</h1>
          <p>Find an employee, capture their face and register their attendance identity.</p>
        </div>
        <Fingerprint size={30} aria-hidden="true" />
      </header>
      <div className="bioreg-notice">
        <ShieldCheck size={20} aria-hidden="true" />
        <p>
          Complete hiring approval and mobile device registration first. Face registration does not
          approve an employee or mark attendance.
        </p>
      </div>
      <div className="bioreg-layout">
        <section className="bioreg-panel" aria-label="Find employee">
          <h2>Find employee</h2>
          <form
            className="bioreg-search"
            onSubmit={(e) => {
              e.preventDefault();
              setPage(1);
              setTerm(search.trim());
            }}
          >
            <TextInput
              label="Name or employee number"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              maxLength={150}
            />
            <Button type="submit" variant="secondary">
              <Search size={16} /> Search
            </Button>
          </form>
          {employees.isError ? (
            <div className="bioreg-notice" role="alert">
              <div>
                <p>
                  Employee search could not load. Check the API connection and retry. No
                  registration was submitted.
                </p>
                <Button variant="secondary" onClick={() => void employees.refetch()}>
                  Retry employee search
                </Button>
              </div>
            </div>
          ) : (
            <QueryState query={employees}>
              {employees.data && (
                <>
                  <ul className="bioreg-people">
                    {employees.data.items.map((employee) => (
                      <li key={employee.id}>
                        <button
                          type="button"
                          aria-pressed={selected?.id === employee.id}
                          onClick={() => setSelected(employee)}
                        >
                          <strong>{employee.name || 'Unnamed employee'}</strong>
                          <span>
                            EMP-{employee.employeeNumber} ·{' '}
                            {employee.onboardingStatus === 2
                              ? 'Hiring approved'
                              : 'Hiring not approved'}
                          </span>
                        </button>
                      </li>
                    ))}
                  </ul>
                  {employees.data.items.length === 0 && <p>No employees match this search.</p>}
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
        <section className="bioreg-panel" aria-label="Camera registration">
          {selected ? (
            <EmployeeRegistration
              key={`${access.subject}:${grants}:${selected.id}`}
              employee={selected}
            />
          ) : (
            <div className="bioreg-empty">
              <Camera size={38} aria-hidden="true" />
              <h2>Select an employee</h2>
              <p>
                Their registered device and enrollment status will appear here. The camera starts
                only when you choose to open it.
              </p>
            </div>
          )}
        </section>
      </div>
    </main>
  );
}

function EmployeeRegistration({ employee }: { employee: EmployeeSummary }) {
  const access = useHrAccess();
  const [generation, setGeneration] = useState(0);
  const canManage = access.user?.grants.some((g) => g.permission === 'attendance.face.manage');
  const canRead = access.user?.grants.some((g) => g.permission === 'attendance.face.read');
  return (
    <>
      <header className="bioreg-person">
        <div>
          <h2>{employee.name || 'Employee'}</h2>
          <p>EMP-{employee.employeeNumber}</p>
        </div>
        <Link to={`/hr/employees/${employee.id}`}>Employee details</Link>
      </header>
      {employee.onboardingStatus !== 2 ? (
        <p className="bioreg-notice">
          Hiring approval is required before face registration. Complete the employee's hiring
          review first.
        </p>
      ) : !canManage || !canRead ? (
        <p className="bioreg-notice">
          Registration requires scoped face-management and face-read access. Contact your access
          administrator.
        </p>
      ) : (
        <FaceCameraRegistration
          key={generation}
          employeeId={employee.id}
          onReset={() => setGeneration((g) => g + 1)}
        />
      )}
      <details className="bioreg-history">
        <summary>Enrollment and verification history</summary>
        <EmployeeBiometricSection key={`${employee.id}:${generation}`} employeeId={employee.id} />
      </details>
    </>
  );
}
