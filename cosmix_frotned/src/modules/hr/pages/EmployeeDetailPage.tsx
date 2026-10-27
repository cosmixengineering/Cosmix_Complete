import { useState } from 'react';
import { Link, useParams, useSearchParams, useLocation } from 'react-router-dom';
import {
  ArrowLeft,
  ArrowRight,
  Briefcase,
  Building2,
  ChevronRight,
  Contact,
  FileText,
  Fingerprint,
  HeartPulse,
  Home,
  Landmark,
  Mail,
  MapPin,
  Navigation,
  Pencil,
  RefreshCw,
  User,
  Users,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { id } from '../schemas/contracts';
import { profileResponse } from '../schemas/hiring';
import { currentUpdateSchema } from '../schemas/updates';
import { sections, type Section } from '../types/updates';
import { labelDate } from '../utils/messages';
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
  const rawName = params.get('section');
  const name = rawName ?? 'overview';
  const section = sections.find((s) => s.name === name);

  if (!access.can('hr.employees.read')) return <Denied />;

  const isOverview = !rawName || rawName === 'overview';

  return (
    <div className="emp-page-container">
      {/* Top Breadcrumb Link */}
      <Link className="emp-back-link" to={`${basePath}/employees`}>
        <ArrowLeft size={16} />
        Back to employees
      </Link>

      {/* Main Profile Header Card */}
      <EmployeeProfileHeader employeeId={employeeId} />

      {/* Modern Tabs Bar */}
      <nav className="emp-nav-tabs" aria-label="Employee navigation">
        <Link to="?section=overview" className={`emp-nav-tab ${isOverview ? 'active' : ''}`}>
          <User size={15} />
          Overview
        </Link>
        <Link
          to="?section=basic"
          className={`emp-nav-tab ${name === 'basic' || name === 'identity' ? 'active' : ''}`}
        >
          <FileText size={15} />
          Personal details
        </Link>
        <Link
          to="?section=employment"
          className={`emp-nav-tab ${name === 'employment' ? 'active' : ''}`}
        >
          <Briefcase size={15} />
          Employment
        </Link>
        <Link to="?section=bank" className={`emp-nav-tab ${name === 'bank' ? 'active' : ''}`}>
          <Landmark size={15} />
          Bank details
        </Link>
        <Link
          to="?section=files"
          className={`emp-nav-tab ${name === 'files' || name === 'medical-files' ? 'active' : ''}`}
        >
          <FileText size={15} />
          Documents
        </Link>
        <Link
          to="?section=assignments"
          className={`emp-nav-tab ${name === 'assignments' || name === 'tracking' || name === 'biometrics' ? 'active' : ''}`}
        >
          <MapPin size={15} />
          Attendance & sites
        </Link>
      </nav>

      {/* Content Area */}
      {isOverview ? (
        <EmployeeOverview employeeId={employeeId} />
      ) : name === 'access' ? (
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
    </div>
  );
}

function EmployeeOverview({ employeeId }: { employeeId: string }) {
  const profileQuery = useHrQuery(`employees/${employeeId}`, profileResponse, [
    'hr.employees.read',
  ]);

  const profile = profileQuery.data?.profile;

  const currentAddress = profile?.currentAddress;
  const currentAddressStr = currentAddress?.line1
    ? `${currentAddress.line1}${currentAddress.city ? `, ${currentAddress.city}` : ''}${currentAddress.province ? `, ${currentAddress.province}` : ''}`
    : null;

  const permanentAddress = profile?.permanentAddress;
  const permanentAddressStr = permanentAddress?.line1
    ? `${permanentAddress.line1}${permanentAddress.city ? `, ${permanentAddress.city}` : ''}${permanentAddress.province ? `, ${permanentAddress.province}` : ''}`
    : null;

  const currentDate = labelDate(new Date().toISOString());
  const effectiveDate = profile?.joinedOn ? labelDate(profile.joinedOn) : '15 Jan 2026';

  return (
    <div className="emp-overview-container">
      {/* Timestamp line */}
      <div className="emp-overview-timestamp">
        Current as of {currentDate} &middot; Effective from {effectiveDate}
      </div>

      <div className="emp-overview-grid">
        {/* Left Column: Personal Information Card */}
        <div className="emp-overview-left">
          <div className="emp-card">
            {/* Header */}
            <div className="emp-card-header">
              <div className="emp-card-title">
                <User size={18} className="emp-card-title-icon" />
                <span>Personal information</span>
              </div>
              <Link to="?section=basic&edit=1" className="emp-card-edit-link">
                <Pencil size={13} />
                Edit
              </Link>
            </div>

            {/* Section: Addresses */}
            <div className="emp-section-heading">Addresses</div>
            <div className="emp-two-col-grid">
              {/* Current address */}
              <div className="emp-info-block">
                <div className="emp-block-title">
                  <MapPin size={15} className="emp-block-icon" />
                  <span>Current address</span>
                </div>
                <div className="emp-block-value">{currentAddressStr || 'Address not added'}</div>
                <Link to="?section=basic&edit=1" className="emp-block-action">
                  + Add address
                </Link>
              </div>

              {/* Permanent address */}
              <div className="emp-info-block">
                <div className="emp-block-title">
                  <Home size={15} className="emp-block-icon" />
                  <span>Permanent address</span>
                </div>
                <div className="emp-block-value">{permanentAddressStr || 'Address not added'}</div>
                <Link to="?section=basic&edit=1" className="emp-block-action">
                  + Add address
                </Link>
              </div>
            </div>

            {/* Section: Contact details */}
            <div className="emp-section-heading">Contact details</div>
            <div className="emp-two-col-grid">
              {/* Personal email */}
              <div className="emp-info-block">
                <div className="emp-block-title">
                  <Mail size={15} className="emp-block-icon" />
                  <span>Personal email</span>
                </div>
                <div className="emp-block-value">{profile?.personalEmail || 'Not added'}</div>
                <Link to="?section=basic&edit=1" className="emp-block-action">
                  + Add email
                </Link>
              </div>

              {/* Emergency contact */}
              <div className="emp-info-block">
                <div className="emp-block-title">
                  <Users size={15} className="emp-block-icon" />
                  <span>Emergency contact</span>
                </div>
                <div className="emp-emergency-table">
                  <div className="emp-emergency-row">
                    <span className="emp-emergency-label">Name</span>
                    <span className="emp-emergency-val">
                      {profile?.emergencyContact?.name || 'Not added'}
                    </span>
                  </div>
                  <div className="emp-emergency-row">
                    <span className="emp-emergency-label">Relationship</span>
                    <span className="emp-emergency-val">
                      {profile?.emergencyContact?.relationship || 'Not added'}
                    </span>
                  </div>
                </div>
                <Link to="?section=basic&edit=1" className="emp-block-action">
                  <Pencil size={12} />
                  Edit contact
                </Link>
              </div>
            </div>

            {/* Section: More personal details */}
            <div className="emp-more-heading">More personal details</div>
            <div className="emp-action-cards-grid">
              <Link to="?section=identity" className="emp-action-card">
                <div className="emp-action-card-left">
                  <Contact size={17} className="emp-action-card-icon" />
                  <span>Identity details</span>
                </div>
                <ArrowRight size={15} className="emp-action-card-arrow" />
              </Link>

              <Link to="?section=medical-files" className="emp-action-card">
                <div className="emp-action-card-left">
                  <HeartPulse size={17} className="emp-action-card-icon red" />
                  <span>Medical information</span>
                </div>
                <ArrowRight size={15} className="emp-action-card-arrow" />
              </Link>
            </div>
          </div>
        </div>

        {/* Right Column: Work & attendance + Profile records */}
        <div className="emp-overview-right">
          {/* Card 1: Work & attendance */}
          <div className="emp-card">
            <div className="emp-card-header simple">
              <div className="emp-card-title">
                <MapPin size={17} className="emp-card-title-icon" />
                <span>Work & attendance</span>
              </div>
            </div>

            <div className="emp-menu-list">
              <Link to="?section=assignments" className="emp-menu-item">
                <div className="emp-menu-item-left">
                  <Building2 size={18} className="emp-menu-icon" />
                  <div className="emp-menu-texts">
                    <div className="emp-menu-title">Site assignments</div>
                    <div className="emp-menu-subtitle">Assigned work locations</div>
                  </div>
                </div>
                <ChevronRight size={16} className="emp-menu-chevron" />
              </Link>

              <Link to="?section=biometrics" className="emp-menu-item">
                <div className="emp-menu-item-left">
                  <Fingerprint size={18} className="emp-menu-icon" />
                  <div className="emp-menu-texts">
                    <div className="emp-menu-title">Biometrics & Face ID</div>
                    <div className="emp-menu-subtitle">Enrollment and verification</div>
                  </div>
                </div>
                <ChevronRight size={16} className="emp-menu-chevron" />
              </Link>

              <Link to="?section=tracking" className="emp-menu-item">
                <div className="emp-menu-item-left">
                  <Navigation size={18} className="emp-menu-icon" />
                  <div className="emp-menu-texts">
                    <div className="emp-menu-title">Live map tracking</div>
                    <div className="emp-menu-subtitle">Employee location</div>
                  </div>
                </div>
                <ChevronRight size={16} className="emp-menu-chevron" />
              </Link>
            </div>
          </div>

          {/* Card 2: Profile records */}
          <div className="emp-card" style={{ marginTop: '16px' }}>
            <div className="emp-card-header simple">
              <div className="emp-card-title">
                <FileText size={17} className="emp-card-title-icon" />
                <span>Profile records</span>
              </div>
            </div>

            <div className="emp-menu-list">
              <Link to="?section=employment" className="emp-menu-item">
                <div className="emp-menu-item-left">
                  <Briefcase size={18} className="emp-menu-icon" />
                  <div className="emp-menu-texts">
                    <div className="emp-menu-title">Employment</div>
                    <div className="emp-menu-subtitle">Work and employment details</div>
                  </div>
                </div>
                <ChevronRight size={16} className="emp-menu-chevron" />
              </Link>

              <Link to="?section=bank" className="emp-menu-item">
                <div className="emp-menu-item-left">
                  <Landmark size={18} className="emp-menu-icon" />
                  <div className="emp-menu-texts">
                    <div className="emp-menu-title">Bank details</div>
                    <div className="emp-menu-subtitle">Employee bank information</div>
                  </div>
                </div>
                <ChevronRight size={16} className="emp-menu-chevron" />
              </Link>

              <Link to="?section=files" className="emp-menu-item">
                <div className="emp-menu-item-left">
                  <FileText size={18} className="emp-menu-icon" />
                  <div className="emp-menu-texts">
                    <div className="emp-menu-title">Documents</div>
                    <div className="emp-menu-subtitle">Records and uploaded files</div>
                  </div>
                </div>
                <ChevronRight size={16} className="emp-menu-chevron" />
              </Link>
            </div>
          </div>
        </div>
      </div>
    </div>
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

  const profileQuery = useHrQuery(`employees/${employeeId}`, profileResponse, [
    'hr.employees.read',
  ]);
  const isApproved = (profileQuery.data?.employee?.status ?? 2) === 2;

  const query = useHrQuery(
    `employees/${employeeId}/current/${section.name}`,
    currentUpdateSchema,
    ['hr.employees.read', ...section.read],
    isApproved,
  );
  const canWrite = access.can(...section.read, ...section.write);

  if (!isApproved && profileQuery.data?.employee) {
    const isDraft = profileQuery.data.employee.status === 0;
    const statusLabel = isDraft ? 'Draft (Incomplete)' : 'Pending Approval';
    return (
      <div className="emp-section-wrapper" style={{ marginTop: '16px' }}>
        <div className="hr-repeat-heading">
          <h2>{section.label}</h2>
        </div>
        <div
          style={{
            padding: '16px 20px',
            background: '#fefce8',
            border: '1px solid #fef08a',
            borderRadius: '8px',
            marginBottom: '18px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: '12px',
          }}
        >
          <div>
            <div
              style={{ fontWeight: 650, fontSize: '14px', color: '#854d0e', marginBottom: '3px' }}
            >
              Employee Onboarding in Progress ({statusLabel})
            </div>
            <div style={{ fontSize: '12.5px', color: '#a16207' }}>
              Formal version trees and live change requests become active after the employee
              onboarding is approved.
            </div>
          </div>
          <Link
            to={`/hr/hiring?employeeId=${employeeId}`}
            style={{
              padding: '7px 16px',
              borderRadius: '6px',
              background: '#0b1a3d',
              color: '#ffffff',
              fontSize: '12.5px',
              fontWeight: 600,
              textDecoration: 'none',
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
            }}
          >
            Edit / Continue Onboarding
          </Link>
        </div>
        <RecordView
          value={
            (section.name === 'basic' || section.name === 'identity') && profileQuery.data?.profile
              ? profileQuery.data.profile
              : { status: `Draft record pending onboarding completion (${statusLabel})` }
          }
        />
        <ChangeHistory employeeId={employeeId} section={section} key={historyRevision} />
      </div>
    );
  }

  return (
    <div className="emp-section-wrapper" style={{ marginTop: '16px' }}>
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
        <Link
          className="hr-back"
          to={`?section=${section.name}`}
          style={{ marginTop: '16px', display: 'inline-flex' }}
        >
          <ArrowLeft size={16} />
          Back to current details
        </Link>
      )}
    </div>
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
    <div className="emp-section-wrapper" style={{ marginTop: '16px' }}>
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
    </div>
  );
}
