import { useState } from 'react';
import { Link, useParams, useSearchParams, useLocation } from 'react-router-dom';
import {
  Activity,
  ArrowLeft,
  Award,
  Briefcase,
  Building2,
  CreditCard,
  DollarSign,
  FileText,
  HeartPulse,
  KeyRound,
  Laptop,
  Navigation,
  Pencil,
  RefreshCw,
  ShieldCheck,
  Smartphone,
  User,
  Fingerprint,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { id } from '../schemas/contracts';
import { currentUpdateSchema } from '../schemas/updates';
import { sections, type Section } from '../types/updates';
import { Denied, QueryState } from '../components/Feedback';
import { RecordView } from '../components/RecordView';
import { ChangeForm } from '../components/employee/ChangeForm';
import { ChangeHistory } from '../components/employee/ChangeHistory';
import { ChangeRequests } from '../components/requests/ChangeRequests';
import { EmployeeDocuments } from '../components/documents/EmployeeDocuments';
import { SalaryWorkspace, StatusWorkspace } from '../components/requests/EmployeeWorkflows';
import { EmployeeAccessSetup } from '../components/approvals/EmployeeAccessSetup';
import { EmployeeSiteAssignments } from '../components/employee/EmployeeSiteAssignments';
import { EmployeeMapTracking } from '../components/employee/EmployeeMapTracking';
import { EmployeeProfileHeader } from '../components/employee/EmployeeProfileHeader';
import { EmployeeBiometricSection } from '../components/employee/EmployeeBiometricSection';
import { recordFamilies, recordPermissions } from '../employee-records';

function getSectionIcon(sectionName: string) {
  switch (sectionName) {
    case 'basic':
      return <User size={14} />;
    case 'identity':
      return <ShieldCheck size={14} />;
    case 'employment':
      return <Briefcase size={14} />;
    case 'bank':
      return <CreditCard size={14} />;
    default:
      return null;
  }
}

function getFamilyIcon(kind: string) {
  switch (kind) {
    case 'assets':
      return <Laptop size={14} />;
    case 'sims':
      return <Smartphone size={14} />;
    case 'certifications':
      return <Award size={14} />;
    default:
      return null;
  }
}

export function EmployeeDetailPage() {
  const { employeeId = '' } = useParams();
  return id.safeParse(employeeId).success ? (
    <EmployeeWorkspace key={employeeId} employeeId={employeeId} />
  ) : (
    <p role="alert">Invalid employee reference.</p>
  );
}
function EmployeeWorkspace({ employeeId }: { employeeId: string }) {
  const access = useHrAccess();
  const location = useLocation();
  const basePath = location.pathname.startsWith('/admin') ? '/admin' : '/hr';
  const [params] = useSearchParams();
  const name = params.get('section') ?? 'basic';
  const section = sections.find((s) => s.name === name);
  if (!access.can('hr.employees.read')) return <Denied />;
  return (
    <>
      <Link className="hr-back" to={`${basePath}/employees`}>
        <ArrowLeft size={16} />
        Back to Employees
      </Link>
      <EmployeeProfileHeader employeeId={employeeId} />
      <nav
        className="hr-tabs"
        aria-label="Employee sections"
        style={{
          display: 'flex',
          gap: '6px',
          overflowX: 'auto',
          paddingBottom: '2px',
          borderBottom: '2px solid #e2e8f0',
          marginBottom: '24px',
        }}
      >
        <Link
          className={name === 'tracking' ? 'active' : ''}
          to="?section=tracking"
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '6px',
            color: name === 'tracking' ? '#047857' : undefined,
            fontWeight: name === 'tracking' ? 700 : undefined,
          }}
        >
          <Navigation size={14} />
          Map tracking
        </Link>
        <Link
          className={name === 'biometrics' ? 'active' : ''}
          to="?section=biometrics"
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '6px',
            color: name === 'biometrics' ? '#2563eb' : undefined,
            fontWeight: name === 'biometrics' ? 700 : undefined,
          }}
        >
          <Fingerprint size={14} />
          Biometrics & Face ID
        </Link>
        {access.can('hr.sites.read') && (
          <Link
            className={name === 'assignments' ? 'active' : ''}
            to="?section=assignments"
            style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
          >
            <Building2 size={14} />
            Site assignments
          </Link>
        )}
        {sections
          .filter((s) => access.can(...s.read))
          .map((s) => (
            <Link
              key={s.name}
              className={name === s.name ? 'active' : ''}
              to={`?section=${s.name}`}
              style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
            >
              {getSectionIcon(s.name)}
              {s.label}
            </Link>
          ))}
        {access.can('hr.employees.documents.read') && (
          <Link
            className={name === 'files' ? 'active' : ''}
            to="?section=files"
            style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
          >
            <FileText size={14} />
            Document files
          </Link>
        )}
        {access.can('hr.employees.documents.read', 'hr.employees.medical.read') && (
          <Link
            className={name === 'medical-files' ? 'active' : ''}
            to="?section=medical-files"
            style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
          >
            <HeartPulse size={14} />
            Medical files
          </Link>
        )}
        {access.can('hr.employees.compensation.read') && (
          <Link
            className={name === 'compensation' ? 'active' : ''}
            to="?section=compensation"
            style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
          >
            <DollarSign size={14} />
            Salary
          </Link>
        )}
        {access.can('hr.employees.lifecycle.read') && (
          <Link
            className={name === 'lifecycle' ? 'active' : ''}
            to="?section=lifecycle"
            style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
          >
            <Activity size={14} />
            Employment status
          </Link>
        )}
        {access.can('hr.employees.submissions.review', 'access.accounts.read') && (
          <Link
            className={name === 'access' ? 'active' : ''}
            to="?section=access"
            style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
          >
            <KeyRound size={14} />
            Account access
          </Link>
        )}
        {recordFamilies
          .filter((family) => access.can(...recordPermissions(family.kind)))
          .map((family) => (
            <Link
              key={family.kind}
              to={`${basePath}/employees/${employeeId}/${family.segment}`}
              style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}
            >
              {getFamilyIcon(family.kind)}
              {family.label}
            </Link>
          ))}
      </nav>
      {name === 'access' ? (
        access.can('hr.employees.submissions.review', 'access.accounts.read') ? (
          <CurrentAccess key={employeeId} employeeId={employeeId} />
        ) : (
          <Denied />
        )
      ) : name === 'assignments' ? (
        access.can('hr.sites.read') ? (
          <EmployeeSiteAssignments key={employeeId} employeeId={employeeId} />
        ) : (
          <Denied />
        )
      ) : name === 'tracking' ? (
        <EmployeeMapTracking key={employeeId} employeeId={employeeId} />
      ) : name === 'biometrics' ? (
        <EmployeeBiometricSection key={employeeId} employeeId={employeeId} />
      ) : section ? (
        access.can(...section.read) ? (
          <CurrentSection
            key={section.name}
            employeeId={employeeId}
            section={section}
            edit={params.get('edit') === '1'}
          />
        ) : (
          <Denied />
        )
      ) : name === 'compensation' ? (
        <SalaryWorkspace key={name} employeeId={employeeId} edit={params.get('edit') === '1'} />
      ) : name === 'lifecycle' ? (
        <StatusWorkspace key={name} employeeId={employeeId} edit={params.get('edit') === '1'} />
      ) : name === 'files' || name === 'medical-files' ? (
        <CurrentFiles key={name} employeeId={employeeId} medical={name === 'medical-files'} />
      ) : (
        <p role="alert">Employee section not found.</p>
      )}
    </>
  );
}
function CurrentAccess({ employeeId }: { employeeId: string }) {
  const query = useHrQuery(`employees/${employeeId}/current/basic`, currentUpdateSchema, [
    'hr.employees.read',
  ]);
  return (
    <QueryState query={query}>
      {query.data && <EmployeeAccessSetup employeeId={employeeId} approved />}
    </QueryState>
  );
}
function CurrentSection({
  employeeId,
  section,
  edit,
}: {
  employeeId: string;
  section: Section;
  edit: boolean;
}) {
  const access = useHrAccess();
  const [revision, setRevision] = useState(0);
  const [historyRevision, setHistoryRevision] = useState(0);
  const [saved, setSaved] = useState(false);
  const query = useHrQuery(`employees/${employeeId}/current/${section.name}`, currentUpdateSchema, [
    'hr.employees.read',
    ...section.read,
  ]);
  const canWrite = access.can(...section.read, ...section.write);
  return (
    <>
      <div className="hr-repeat-heading">
        <h2>{section.label}</h2>
        {!edit && (
          <Button
            variant="ghost"
            title="Reload current section"
            aria-label="Reload current section"
            onClick={() => void query.refetch()}
          >
            <RefreshCw size={16} />
          </Button>
        )}
      </div>
      {saved && (
        <p role="status" className="hr-message">
          Saved successfully.
        </p>
      )}
      <QueryState query={query}>
        {query.data && (
          <>
            <p className="muted">
              Current as of {query.data.asOf} &middot; Effective from{' '}
              {query.data.current.effectiveFrom}
            </p>
            {edit ? (
              canWrite ? (
                <ChangeForm
                  key={query.data.version}
                  current={query.data}
                  section={section}
                  onSaved={() => {
                    setSaved(true);
                    setRevision((r) => r + 1);
                    void query.refetch();
                  }}
                />
              ) : (
                <Denied />
              )
            ) : (
              <>
                <RecordView
                  value={query.data.current.data[section.name] ?? { status: 'No current record' }}
                />
                {query.data.current.bankVerified !== null && (
                  <p>
                    Bank verification:{' '}
                    {query.data.current.bankVerified ? 'Verified' : 'Not verified'}
                  </p>
                )}
                {query.data.scheduled && (
                  <section className="hr-form-section">
                    <h3>Scheduled for {query.data.scheduled.effectiveFrom}</h3>
                    <RecordView value={query.data.scheduled.data[section.name] ?? {}} />
                  </section>
                )}
                {canWrite && !query.data.scheduled && (
                  <div className="hr-actions">
                    <Link
                      className="button button--secondary"
                      to={`?section=${section.name}&edit=1`}
                    >
                      <Pencil size={16} />
                      {section.name === 'basic' ? 'Edit basic details' : 'New change request'}
                    </Link>
                  </div>
                )}
              </>
            )}
          </>
        )}
      </QueryState>
      <ChangeHistory
        key={`history-${revision}-${historyRevision}`}
        employeeId={employeeId}
        section={section}
      />
      {section.name !== 'basic' && (
        <ChangeRequests
          key={`requests-${revision}`}
          employeeId={employeeId}
          section={section}
          onDecided={() => {
            void query.refetch();
            setHistoryRevision((v) => v + 1);
          }}
        />
      )}
      {edit && (
        <Link className="hr-back" to={`?section=${section.name}`}>
          <ArrowLeft size={16} />
          Back to current details
        </Link>
      )}
    </>
  );
}
function CurrentFiles({ employeeId, medical }: { employeeId: string; medical: boolean }) {
  const access = useHrAccess();
  const [saved, setSaved] = useState(false);
  const permissions = [
    'hr.employees.read',
    'hr.employees.documents.read',
    ...(medical ? ['hr.employees.medical.read'] : []),
  ];
  const query = useHrQuery(
    `employees/${employeeId}/current/basic`,
    currentUpdateSchema,
    permissions,
  );
  if (!access.can(...permissions)) return <Denied />;
  return (
    <QueryState query={query}>
      {query.data && (
        <>
          {saved && (
            <p role="status" className="hr-message">
              Document evidence updated.
            </p>
          )}
          <EmployeeDocuments
            employeeId={employeeId}
            version={query.data.version}
            editable={access.can('hr.employees.changes.request')}
            approved
            medical={medical}
            onSaved={() => {
              setSaved(true);
              void query.refetch();
            }}
          />
        </>
      )}
    </QueryState>
  );
}
