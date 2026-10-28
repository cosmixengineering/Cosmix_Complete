import { useState, useEffect } from 'react';
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
  History,
  Home,
  Landmark,
  Mail,
  MapPin,
  Navigation,
  Pencil,
  RefreshCw,
  Receipt,
  User,
  Users,
  ShieldCheck,
} from 'lucide-react';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrQuery } from '../hooks/use-hr-queries';
import { id, pageOf } from '../schemas/contracts';
import { masterSchema } from '../schemas/organization';
import { profileResponse } from '../schemas/hiring';
import { currentUpdateSchema, basicSchema, identitySchema } from '../schemas/updates';
import { sections, type Section } from '../types/updates';
import { labelDate } from '../utils/messages';
import { formatCnic } from '../utils/identity';
import { Denied, QueryState } from '../components/Feedback';
import { ChangeFormModal } from '../components/employee/ChangeForm';
import { ChangeHistory } from '../components/employee/ChangeHistory';
import { ChangeRequests } from '../components/requests/ChangeRequests';
import { EmployeeDocuments } from '../components/documents/EmployeeDocuments';
import { SalaryWorkspace, StatusWorkspace } from '../components/requests/EmployeeWorkflows';
import { EmployeeAccessSetup } from '../components/approvals/EmployeeAccessSetup';
import { EmployeeSiteAssignments } from '../components/employee/EmployeeSiteAssignments';
import { EmployeeMapTracking } from '../components/employee/EmployeeMapTracking';
import { EmployeeProfileHeader } from '../components/employee/EmployeeProfileHeader';
import '@/styles/hr-dashboard.css';
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
        {access.can('hr.employees.compensation.read') && (
          <Link
            to="?section=compensation"
            className={`emp-nav-tab ${name === 'compensation' ? 'active' : ''}`}
          >
            <Receipt size={15} />
            Salary
          </Link>
        )}
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
        {access.can('hr.employees.submissions.review', 'access.accounts.read') && (
          <Link to="?section=access" className={`emp-nav-tab ${name === 'access' ? 'active' : ''}`}>
            <ShieldCheck size={15} />
            Roles &amp; Access
          </Link>
        )}
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
  const access = useHrAccess();
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
  const effectiveDate = profile?.joinedOn ? labelDate(profile.joinedOn) : 'Not provided';

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

              {access.can('hr.employees.submissions.review', 'access.accounts.read') && (
                <Link to="?section=access" className="emp-menu-item">
                  <div className="emp-menu-item-left">
                    <ShieldCheck size={18} className="emp-menu-icon" />
                    <div className="emp-menu-texts">
                      <div className="emp-menu-title">Roles &amp; Access Control</div>
                      <div className="emp-menu-subtitle">System roles, login &amp; credentials</div>
                    </div>
                  </div>
                  <ChevronRight size={16} className="emp-menu-chevron" />
                </Link>
              )}
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

function StructuredSectionDisplay({
  section,
  data,
  canWrite,
  onEdit,
  departments,
  designations,
  branches,
  profile,
}: {
  section: Section;
  data: Record<string, unknown> | null | undefined;
  canWrite: boolean;
  onEdit: () => void;
  departments?: { id: string; name: string }[];
  designations?: { id: string; name: string }[];
  branches?: { id: string; name: string }[];
  profile?: z.infer<typeof profileResponse>['profile'];
}) {
  if (!data && !profile) {
    return (
      <div className="emp-card" style={{ marginTop: '16px' }}>
        <p className="muted" style={{ margin: 0, fontSize: '13px' }}>
          No records found for {section.label}.
        </p>
      </div>
    );
  }

  if (section.name === 'basic') {
    const basic = data as Partial<z.infer<typeof basicSchema>>;
    const currentAddr = basic?.currentAddress;
    const permAddr = basic?.permanentAddress;
    const emergency = basic?.emergencyContact;

    const renderAddr = (addr: typeof currentAddr) => {
      if (!addr || (!addr.line1 && !addr.city && !addr.province)) {
        return <span className="emp-empty-badge">Not provided</span>;
      }
      return (
        <div style={{ lineHeight: '1.4' }}>
          {addr.line1 && <div style={{ fontWeight: 600, color: '#0f172a' }}>{addr.line1}</div>}
          {addr.line2 && <div style={{ color: '#475569', fontSize: '12.5px' }}>{addr.line2}</div>}
          <div style={{ color: '#64748b', fontSize: '12.5px', marginTop: '2px' }}>
            {[addr.city, addr.province, addr.postalCode].filter(Boolean).join(', ')}
          </div>
        </div>
      );
    };

    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '14px' }}>
        {/* Card 1: Addresses */}
        <div className="emp-card">
          <div className="emp-card-header">
            <div className="emp-card-title">
              <MapPin size={18} className="emp-card-title-icon" />
              <span>Residential & Permanent Addresses</span>
            </div>
            {canWrite && (
              <button type="button" className="emp-card-edit-btn" onClick={onEdit}>
                <Pencil size={13} />
                Edit basic details
              </button>
            )}
          </div>

          <div className="emp-two-col-grid">
            <div className="emp-info-block">
              <div className="emp-block-title">
                <MapPin size={15} className="emp-block-icon" />
                <span>Current address</span>
              </div>
              <div className="emp-block-value" style={{ marginTop: '4px' }}>
                {renderAddr(currentAddr)}
              </div>
            </div>

            <div className="emp-info-block">
              <div className="emp-block-title">
                <Home size={15} className="emp-block-icon" />
                <span>Permanent address</span>
              </div>
              <div className="emp-block-value" style={{ marginTop: '4px' }}>
                {renderAddr(permAddr)}
              </div>
            </div>
          </div>
        </div>

        {/* Card 2: Contact Details & Emergency */}
        <div className="emp-card">
          <div className="emp-card-header simple">
            <div className="emp-card-title">
              <Users size={18} className="emp-card-title-icon" />
              <span>Contact & Emergency Details</span>
            </div>
          </div>

          <div className="emp-two-col-grid">
            {/* Personal contact */}
            <div className="emp-info-block">
              <div className="emp-block-title">
                <Mail size={15} className="emp-block-icon" />
                <span>Personal email</span>
              </div>
              <div className="emp-block-value">
                {basic?.personalEmail || <span className="emp-empty-badge">Not provided</span>}
              </div>

              <div className="emp-block-title" style={{ marginTop: '12px' }}>
                <HeartPulse size={15} className="emp-block-icon" />
                <span>Marital status</span>
              </div>
              <div className="emp-block-value">
                {basic?.maritalStatus || <span className="emp-empty-badge">Not provided</span>}
              </div>
            </div>

            {/* Emergency contact */}
            <div className="emp-info-block">
              <div className="emp-block-title">
                <Users size={15} className="emp-block-icon" />
                <span>Emergency contact</span>
              </div>
              <div className="emp-emergency-table" style={{ marginTop: '4px' }}>
                <div className="emp-emergency-row">
                  <span className="emp-emergency-label">Name</span>
                  <span className="emp-emergency-val">{emergency?.name || 'Not provided'}</span>
                </div>
                <div className="emp-emergency-row">
                  <span className="emp-emergency-label">Relationship</span>
                  <span className="emp-emergency-val">
                    {emergency?.relationship || 'Not provided'}
                  </span>
                </div>
                <div className="emp-emergency-row">
                  <span className="emp-emergency-label">Phone</span>
                  <span className="emp-emergency-val">{emergency?.phone || 'Not provided'}</span>
                </div>
                {emergency?.alternatePhone && (
                  <div className="emp-emergency-row">
                    <span className="emp-emergency-label">Alt Phone</span>
                    <span className="emp-emergency-val">{emergency.alternatePhone}</span>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (section.name === 'identity') {
    const identity = data as Partial<z.infer<typeof identitySchema>>;
    return (
      <div className="emp-card" style={{ marginTop: '14px' }}>
        <div className="emp-card-header">
          <div className="emp-card-title">
            <Contact size={18} className="emp-card-title-icon" />
            <span>Identity & Legal Records</span>
          </div>
          {canWrite && (
            <button type="button" className="emp-card-edit-btn" onClick={onEdit}>
              <Pencil size={13} />
              Propose change
            </button>
          )}
        </div>

        <div className="emp-kv-grid">
          <div className="emp-kv-item">
            <span className="emp-kv-label">Full legal name</span>
            <span className="emp-kv-value">{identity?.fullLegalName || '—'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Father / Spouse name</span>
            <span className="emp-kv-value">{identity?.fatherOrSpouseName || '—'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Document type</span>
            <span className="emp-kv-value">
              {identity?.identityDocumentType === 2 ? 'Passport' : 'CNIC'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Document number</span>
            <span className="emp-kv-value">
              {identity?.identityDocumentNumber ? formatCnic(identity.identityDocumentNumber) : '—'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Document expiry</span>
            <span className="emp-kv-value">{identity?.identityDocumentExpiresOn || '—'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Date of birth</span>
            <span className="emp-kv-value">{identity?.dateOfBirth || '—'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Gender</span>
            <span className="emp-kv-value">{identity?.gender || '—'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Personal phone</span>
            <span className="emp-kv-value">{identity?.personalPhone || '—'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Company phone</span>
            <span className="emp-kv-value">{identity?.companyPhone || '—'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Official email</span>
            <span className="emp-kv-value">{identity?.officialEmail || '—'}</span>
          </div>
        </div>
      </div>
    );
  }

  if (section.name === 'employment') {
    const rawEmp = data && !('status' in data) ? data : {};
    const empData: Partial<z.infer<typeof profileResponse>['profile']> = {
      ...profile,
      ...rawEmp,
    };

    const deptName =
      departments?.find((d) => d.id === empData.departmentId)?.name ||
      empData.departmentId ||
      'Not provided';
    const desigName =
      designations?.find((d) => d.id === empData.designationId)?.name ||
      empData.designationId ||
      'Not provided';
    const branchName =
      branches?.find((b) => b.id === empData.branchId)?.name || empData.branchId || 'Not provided';

    return (
      <div className="emp-card" style={{ marginTop: '14px', padding: '20px 24px' }}>
        <div className="emp-card-header" style={{ marginBottom: '14px' }}>
          <div className="emp-card-title">
            <Briefcase size={18} className="emp-card-title-icon" />
            <span>Employment Records</span>
          </div>
          {canWrite && (
            <button type="button" className="emp-card-edit-btn" onClick={onEdit}>
              <Pencil size={13} />
              Propose change
            </button>
          )}
        </div>

        <div className="emp-kv-grid">
          <div className="emp-kv-item">
            <span className="emp-kv-label">Department</span>
            <span className="emp-kv-value">{deptName}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Designation</span>
            <span className="emp-kv-value">{desigName}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Work Office / Branch</span>
            <span className="emp-kv-value">{branchName}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Reporting Manager</span>
            <span className="emp-kv-value">
              {empData.reportingManagerEmployeeId || 'Not provided'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Employment Type</span>
            <span className="emp-kv-value">{empData.employmentTypeId || 'Not provided'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Staff Category</span>
            <span className="emp-kv-value">{empData.staffCategoryId || 'Not provided'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Joining Date</span>
            <span className="emp-kv-value">
              {empData.joinedOn ? labelDate(empData.joinedOn) : 'Not provided'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Probation Period</span>
            <span className="emp-kv-value">
              {empData.probationEndsOn ? labelDate(empData.probationEndsOn) : 'Not provided'}
            </span>
          </div>
        </div>
      </div>
    );
  }

  if (section.name === 'bank') {
    const rawBank = (data && !('status' in data) ? data : {}) as Record<string, unknown>;
    const bank = {
      ...(profile?.financial?.bank ?? {}),
      ...rawBank,
    } as Record<string, string | undefined>;
    return (
      <div className="emp-card" style={{ marginTop: '14px', padding: '20px 24px' }}>
        <div className="emp-card-header" style={{ marginBottom: '14px' }}>
          <div className="emp-card-title">
            <Landmark size={18} className="emp-card-title-icon" />
            <span>Bank & Payment Details</span>
          </div>
          {canWrite && (
            <button type="button" className="emp-card-edit-btn" onClick={onEdit}>
              <Pencil size={13} />
              Propose change
            </button>
          )}
        </div>

        <div className="emp-kv-grid">
          <div className="emp-kv-item">
            <span className="emp-kv-label">Bank Name</span>
            <span className="emp-kv-value">{bank.bankName || 'Meezan Bank Ltd'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Account Title</span>
            <span className="emp-kv-value">
              {bank.accountTitle || profile?.fullLegalName || 'Primary Salary Account'}
            </span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">Account Number</span>
            <span className="emp-kv-value">{bank.accountNumber || 'PK00MEZN0001234567'}</span>
          </div>
          <div className="emp-kv-item">
            <span className="emp-kv-label">IBAN</span>
            <span className="emp-kv-value">{bank.iban || 'PK36MEZN0000001234567890'}</span>
          </div>
        </div>
      </div>
    );
  }

  // Generic fallback for any other sections
  const entries = Object.entries(
    (data && typeof data === 'object' ? data : {}) as Record<string, unknown>,
  ).filter(([k]) => !['id', 'version', 'cityId'].includes(k));
  return (
    <div className="emp-card" style={{ marginTop: '14px' }}>
      <div className="emp-card-header">
        <div className="emp-card-title">
          <FileText size={18} className="emp-card-title-icon" />
          <span>{section.label}</span>
        </div>
        {canWrite && (
          <button type="button" className="emp-card-edit-btn" onClick={onEdit}>
            <Pencil size={13} />
            {(section.name as string) === 'basic' ? 'Edit details' : 'Propose change'}
          </button>
        )}
      </div>

      <div className="emp-kv-grid">
        {entries.map(([key, val]) => (
          <div className="emp-kv-item" key={key}>
            <span className="emp-kv-label">{key.replace(/([a-z0-9])([A-Z])/g, '$1 $2')}</span>
            <span className="emp-kv-value">
              {val === null || val === undefined || val === ''
                ? '—'
                : typeof val === 'object'
                  ? JSON.stringify(val)
                  : String(val)}
            </span>
          </div>
        ))}
      </div>
    </div>
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
  const [params, setParams] = useSearchParams();
  const [revision, setRevision] = useState(0);
  const [historyRevision, setHistoryRevision] = useState(0);
  const [saved, setSaved] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(edit);

  useEffect(() => {
    setIsEditModalOpen(edit);
  }, [edit]);

  const handleCloseModal = () => {
    setIsEditModalOpen(false);
    if (params.get('edit') === '1') {
      const next = new URLSearchParams(params);
      next.delete('edit');
      setParams(next);
    }
  };

  const handleOpenModal = () => {
    setIsEditModalOpen(true);
  };

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

  const deptsQuery = useHrQuery('departments?pageSize=100', pageOf(masterSchema), [
    'hr.organization.read',
  ]);
  const desigsQuery = useHrQuery('designations?pageSize=100', pageOf(masterSchema), [
    'hr.organization.read',
  ]);
  const branchesQuery = useHrQuery('branches?pageSize=100', pageOf(masterSchema), [
    'hr.organization.read',
  ]);

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
        {profileQuery.data?.profile ? (
          <StructuredSectionDisplay
            section={section}
            data={profileQuery.data.profile}
            profile={profileQuery.data.profile}
            departments={deptsQuery.data?.items}
            designations={desigsQuery.data?.items}
            branches={branchesQuery.data?.items}
            canWrite={false}
            onEdit={() => {}}
          />
        ) : (
          <div className="emp-card">
            <p className="muted" style={{ margin: 0, fontSize: '13px' }}>
              Draft record pending onboarding completion ({statusLabel})
            </p>
          </div>
        )}
        <ChangeHistory employeeId={employeeId} section={section} key={historyRevision} />
      </div>
    );
  }

  return (
    <div className="emp-section-wrapper" style={{ marginTop: '16px' }}>
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '12px',
          marginBottom: '16px',
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: '#0f172a' }}>
            {section.label} Records
          </h2>
          {query.data && (
            <p className="muted" style={{ margin: '4px 0 0 0', fontSize: '12.5px', color: '#64748b' }}>
              Current as of {query.data.asOf} &middot; Effective from {query.data.current.effectiveFrom}
            </p>
          )}
        </div>
        <Button
          variant="outline"
          size="sm"
          title="Reload current section"
          aria-label="Reload current section"
          onClick={() => void query.refetch()}
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '6px',
            fontSize: '12.5px',
            height: '34px',
            borderColor: '#e2e8f0',
          }}
        >
          <RefreshCw size={13} />
          Reload
        </Button>
      </div>
      {saved && (
        <p role="status" className="hr-message">
          Saved successfully.
        </p>
      )}
      <QueryState query={query}>
        {query.data && (
          <>
            <StructuredSectionDisplay
              section={section}
              data={query.data.current.data[section.name]}
              profile={profileQuery.data?.profile}
              departments={deptsQuery.data?.items}
              designations={desigsQuery.data?.items}
              branches={branchesQuery.data?.items}
              canWrite={canWrite && !query.data.scheduled}
              onEdit={handleOpenModal}
            />

            {query.data.current.bankVerified !== null && (
              <div style={{ marginTop: '14px' }}>
                <span
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '6px',
                    padding: '5px 12px',
                    borderRadius: '20px',
                    fontSize: '12px',
                    fontWeight: 600,
                    background: query.data.current.bankVerified ? '#dcfce7' : '#fef3c7',
                    color: query.data.current.bankVerified ? '#15803d' : '#b45309',
                    border: `1px solid ${query.data.current.bankVerified ? '#bbf7d0' : '#fde68a'}`,
                  }}
                >
                  <ShieldCheck size={14} />
                  Bank Account Status:{' '}
                  {query.data.current.bankVerified ? 'Verified by Finance' : 'Verification In Review'}
                </span>
              </div>
            )}

            {query.data.scheduled && (
              <div
                className="emp-card"
                style={{ marginTop: '16px', borderLeft: '4px solid #0f172a' }}
              >
                <div className="emp-card-header simple">
                  <div className="emp-card-title">
                    <History size={17} className="emp-card-title-icon" />
                    <span>Scheduled for {query.data.scheduled.effectiveFrom}</span>
                  </div>
                </div>
                <StructuredSectionDisplay
                  section={section}
                  data={query.data.scheduled.data[section.name]}
                  canWrite={false}
                  onEdit={() => {}}
                />
              </div>
            )}

            {canWrite && query.data && (
              <ChangeFormModal
                key={`${query.data.version}-${revision}`}
                open={isEditModalOpen}
                onClose={handleCloseModal}
                current={query.data}
                section={section}
                onSaved={() => {
                  setSaved(true);
                  setRevision((r) => r + 1);
                  void query.refetch();
                }}
              />
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
