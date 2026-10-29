import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Send,
  RefreshCw,
  CheckCircle2,
  AlertTriangle,
  ArrowRight,
  ShieldCheck,
  UserCheck,
  Phone,
  MapPin,
  Building2,
  Smartphone,
  Folder,
  CreditCard,
  HeartPulse,
  Briefcase,
  FileCheck,
  FileText,
  Clock,
  type LucideIcon,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import { useHrAccess } from '../../hooks/use-hr-access';
import { hiringDecisionsSchema, readinessSchema, documentsSchema } from '../../schemas/hiring';
import { writeResult } from '../../schemas/contracts';
import { ActionMessage, QueryState } from '../Feedback';
import { labelDate } from '../../utils/messages';

interface FieldMeta {
  label: string;
  category: string;
  stepName: string;
  tab: string;
  hash?: string;
  icon: LucideIcon;
}

const FIELD_REGISTRY: Record<string, FieldMeta> = {
  // 1. Personal Identity
  fullLegalName: {
    label: 'Full Legal Name',
    category: 'Personal & Legal Identity',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'identity',
    icon: UserCheck,
  },
  identityDocumentType: {
    label: 'Identity Document Type (CNIC / Passport)',
    category: 'Personal & Legal Identity',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'identity',
    icon: UserCheck,
  },
  identityDocumentNumber: {
    label: 'CNIC / Passport Number',
    category: 'Personal & Legal Identity',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'identity',
    icon: UserCheck,
  },
  identityDocumentExpiresOn: {
    label: 'Identity Document Expiry Date',
    category: 'Personal & Legal Identity',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'identity',
    icon: UserCheck,
  },
  dateOfBirth: {
    label: 'Date of Birth',
    category: 'Personal & Legal Identity',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'identity',
    icon: UserCheck,
  },
  gender: {
    label: 'Gender',
    category: 'Personal & Legal Identity',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'identity',
    icon: UserCheck,
  },
  maritalStatus: {
    label: 'Marital Status',
    category: 'Personal & Legal Identity',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'identity',
    icon: UserCheck,
  },
  fatherOrSpouseName: {
    label: 'Father or Spouse Name',
    category: 'Personal & Legal Identity',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'identity',
    icon: UserCheck,
  },

  // 2. Contact Details
  personalPhone: {
    label: 'Personal Mobile Phone (+92...)',
    category: 'Contact Details',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'contacts',
    icon: Phone,
  },
  companyPhone: {
    label: 'Company Assigned Phone',
    category: 'Contact Details',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'contacts',
    icon: Phone,
  },
  personalEmail: {
    label: 'Personal Email Address',
    category: 'Contact Details',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'contacts',
    icon: Phone,
  },
  officialEmail: {
    label: 'Official Corporate Email',
    category: 'Contact Details',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'contacts',
    icon: Phone,
  },

  // 3. Addresses
  currentAddress: {
    label: 'Current Residence Address (Street, City, Province)',
    category: 'Residential & Permanent Addresses',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'current-address',
    icon: MapPin,
  },
  'currentAddress.line1': {
    label: 'Current Residence Street Address (Line 1)',
    category: 'Residential & Permanent Addresses',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'current-address',
    icon: MapPin,
  },
  'currentAddress.province': {
    label: 'Current Residence Province',
    category: 'Residential & Permanent Addresses',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'current-address',
    icon: MapPin,
  },
  'currentAddress.city': {
    label: 'Current Residence City',
    category: 'Residential & Permanent Addresses',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'current-address',
    icon: MapPin,
  },
  permanentAddress: {
    label: 'Permanent Domicile Address (Street, City, Province)',
    category: 'Residential & Permanent Addresses',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'permanent-address',
    icon: MapPin,
  },
  'permanentAddress.line1': {
    label: 'Permanent Domicile Street Address (Line 1)',
    category: 'Residential & Permanent Addresses',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'permanent-address',
    icon: MapPin,
  },
  'permanentAddress.province': {
    label: 'Permanent Domicile Province',
    category: 'Residential & Permanent Addresses',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'permanent-address',
    icon: MapPin,
  },
  'permanentAddress.city': {
    label: 'Permanent Domicile City',
    category: 'Residential & Permanent Addresses',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'permanent-address',
    icon: MapPin,
  },

  // 4. Emergency Contacts
  'emergencyContact.name': {
    label: 'Emergency Contact Full Name',
    category: 'Emergency Contact Information',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'emergency-contact',
    icon: HeartPulse,
  },
  'emergencyContact.relationship': {
    label: 'Emergency Contact Relationship',
    category: 'Emergency Contact Information',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'emergency-contact',
    icon: HeartPulse,
  },
  'emergencyContact.phone': {
    label: 'Emergency Contact Phone Number',
    category: 'Emergency Contact Information',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'emergency-contact',
    icon: HeartPulse,
  },
  'emergencyContact.alternatePhone': {
    label: 'Emergency Alternate Phone',
    category: 'Emergency Contact Information',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'emergency-contact',
    icon: HeartPulse,
  },

  // 5. Workplace Placement & Organizational Setup
  departmentId: {
    label: 'Assigned Department',
    category: 'Workplace & Organizational Placement',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'employment',
    icon: Building2,
  },
  designationId: {
    label: 'Job Designation / Title',
    category: 'Workplace & Organizational Placement',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'employment',
    icon: Building2,
  },
  staffCategoryId: {
    label: 'Workplace Placement (Office-based vs Site/Field Operations)',
    category: 'Workplace & Organizational Placement',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'employment',
    icon: Building2,
  },
  employmentTypeId: {
    label: 'Employment Contract Type (Regular / Contractual / etc.)',
    category: 'Workplace & Organizational Placement',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'employment',
    icon: Building2,
  },
  branchId: {
    label: 'Assigned Office Branch (Mandatory for Office Staff)',
    category: 'Workplace & Organizational Placement',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'employment',
    icon: Building2,
  },
  reportingManagerEmployeeId: {
    label: 'Direct Reporting Manager',
    category: 'Workplace & Organizational Placement',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'employment',
    icon: Building2,
  },
  joinedOn: {
    label: 'Official Joining Date',
    category: 'Workplace & Organizational Placement',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'employment',
    icon: Building2,
  },
  probationEndsOn: {
    label: 'Probation End Date',
    category: 'Workplace & Organizational Placement',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'employment',
    icon: Building2,
  },
  proposedShiftId: {
    label: 'Allocated Working Shift',
    category: 'Workplace & Organizational Placement',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'employment',
    icon: Building2,
  },

  // 6. Mobile ERP Login
  loginPhoneNumber: {
    label: 'Proposed Mobile App Login Number',
    category: 'Mobile Access Setup',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'mobile',
    icon: Smartphone,
  },

  // 7. Documents
  'documents.profilePhoto': {
    label: 'Recent Passport-size Employee Photo',
    category: 'Identity Proof & Documents',
    stepName: 'Step 4: Documents',
    tab: 'documents',
    icon: Folder,
  },
  'documents.identity': {
    label: 'CNIC Scanned Copies (Front & Back) or Passport',
    category: 'Identity Proof & Documents',
    stepName: 'Step 4: Documents',
    tab: 'documents',
    icon: Folder,
  },
  documents: {
    label: 'Mandatory Compliance Documents',
    category: 'Identity Proof & Documents',
    stepName: 'Step 4: Documents',
    tab: 'documents',
    icon: Folder,
  },

  // 8. Financial
  financial: {
    label: 'Base Salary & Compensation Package',
    category: 'Financial & Wage Structure',
    stepName: 'Step 2: Financial',
    tab: 'financial',
    icon: CreditCard,
  },
  bank: {
    label: 'Bank Account Disbursement Information',
    category: 'Financial & Wage Structure',
    stepName: 'Step 2: Financial',
    tab: 'financial',
    icon: CreditCard,
  },

  // 9. Medical
  medical: {
    label: 'Medical Fitness & Clinical Disclosures',
    category: 'Medical History',
    stepName: 'Step 3: Medical',
    tab: 'medical',
    icon: HeartPulse,
  },
};

function resolveFieldMeta(rawField: string): FieldMeta {
  if (FIELD_REGISTRY[rawField]) {
    return FIELD_REGISTRY[rawField]!;
  }

  // Handle prefix patterns
  if (rawField.startsWith('currentAddress')) {
    return {
      label: `Current Address (${rawField.replace('currentAddress.', '')})`,
      category: 'Residential & Permanent Addresses',
      stepName: 'Step 1: Profile Info',
      tab: 'profile',
      hash: 'current-address',
      icon: MapPin,
    };
  }
  if (rawField.startsWith('permanentAddress')) {
    return {
      label: `Permanent Address (${rawField.replace('permanentAddress.', '')})`,
      category: 'Residential & Permanent Addresses',
      stepName: 'Step 1: Profile Info',
      tab: 'profile',
      hash: 'permanent-address',
      icon: MapPin,
    };
  }
  if (rawField.startsWith('emergencyContact')) {
    return {
      label: `Emergency Contact (${rawField.replace('emergencyContact.', '')})`,
      category: 'Emergency Contact Information',
      stepName: 'Step 1: Profile Info',
      tab: 'profile',
      hash: 'emergency-contact',
      icon: HeartPulse,
    };
  }
  if (rawField.startsWith('previousEmployment')) {
    return {
      label: 'Previous Employment Details (Employer, Role, Dates)',
      category: 'Career History',
      stepName: 'Step 1: Profile Info',
      tab: 'profile',
      hash: 'previous-employment',
      icon: Briefcase,
    };
  }
  if (rawField.startsWith('certifications')) {
    return {
      label: 'Professional Certification Record',
      category: 'Certifications',
      stepName: 'Step 1: Profile Info',
      tab: 'profile',
      hash: 'certifications',
      icon: FileCheck,
    };
  }
  if (rawField.startsWith('assets')) {
    return {
      label: 'Company Hardware / Asset Allocation',
      category: 'Company Assets',
      stepName: 'Step 1: Profile Info',
      tab: 'profile',
      hash: 'assets',
      icon: Briefcase,
    };
  }
  if (rawField.startsWith('documents')) {
    return {
      label: `Required Document (${rawField.replace('documents.', '')})`,
      category: 'Identity Proof & Documents',
      stepName: 'Step 4: Documents',
      tab: 'documents',
      icon: Folder,
    };
  }

  // Fallback cleanly formatted
  const formatted = rawField
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/[._]/g, ' ')
    .replace(/\b\w/g, (c) => c.toUpperCase());

  return {
    label: formatted,
    category: 'General Profile & Placement',
    stepName: 'Step 1: Profile Info',
    tab: 'profile',
    hash: 'identity',
    icon: UserCheck,
  };
}

export function SubmissionReview({
  employeeId,
  version,
  editable,
  selected,
  onSaved,
}: {
  employeeId: string;
  version: string;
  editable: boolean;
  selected: string[];
  onSaved: () => void;
}) {
  const access = useHrAccess();
  const action = useHrAction();
  const [submitting, setSubmitting] = useState(false);

  const readiness = useHrQuery(
    `employees/${employeeId}/submission-validation`,
    readinessSchema,
    ['hr.employees.read', 'hr.employees.submit'],
    editable,
  );

  const decisions = useHrQuery(`employees/${employeeId}/decisions`, hiringDecisionsSchema, [
    'hr.employees.read',
  ]);

  const docs = useHrQuery(`employees/${employeeId}/documents`, documentsSchema, [
    'hr.employees.read',
    'hr.employees.documents.read',
  ]);

  const isReady = readiness.data?.canSubmit === true;
  const missingList = readiness.data?.missingFields ?? [];
  const unavailableDocCount = readiness.data?.unavailableDocumentIds?.length ?? 0;

  // Group missing fields into categories
  const groupedCategories = missingList.reduce<
    Record<
      string,
      {
        title: string;
        stepName: string;
        tab: string;
        hash?: string;
        icon: LucideIcon;
        items: string[];
      }
    >
  >((acc, field) => {
    const meta = resolveFieldMeta(field);
    if (!acc[meta.category]) {
      acc[meta.category] = {
        title: meta.category,
        stepName: meta.stepName,
        tab: meta.tab,
        hash: meta.hash,
        icon: meta.icon,
        items: [],
      };
    }
    acc[meta.category]!.items.push(meta.label);
    return acc;
  }, {});

  // Add unavailable documents to groupedCategories if any exist
  if (unavailableDocCount > 0) {
    const docCatKey = 'Identity Proof & Documents';
    if (!groupedCategories[docCatKey]) {
      groupedCategories[docCatKey] = {
        title: docCatKey,
        stepName: 'Step 4: Documents',
        tab: 'documents',
        icon: Folder,
        items: [],
      };
    }
    groupedCategories[docCatKey]!.items.push(
      `${unavailableDocCount} selected document(s) are pending file verification or unavailable`,
    );
  }

  const categoriesArray = Object.values(groupedCategories);
  const totalMissingCount = missingList.length + (unavailableDocCount > 0 ? 1 : 0);

  const handleSubmit = async () => {
    if (
      !window.confirm('Submit this completed employee profile to Admin for final authorization?')
    ) {
      return;
    }
    setSubmitting(true);
    try {
      const r = await action.run(
        `employees/${employeeId}/submit`,
        { expectedVersion: version },
        'POST',
        writeResult,
      );
      if (r) {
        onSaved();
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="submission-readiness-wrapper">
      {editable && access.can('hr.employees.submit') && (
        <>
          {/* Main Hero Card: Ready vs Incomplete */}
          <div className={`readiness-hero-card ${isReady ? 'is-ready' : 'is-incomplete'}`}>
            <div className="readiness-hero-header">
              <div className="readiness-hero-left">
                <div className="readiness-hero-icon">
                  {isReady ? <ShieldCheck size={28} /> : <AlertTriangle size={28} />}
                </div>
                <div className="readiness-hero-meta">
                  <span className="readiness-status-tag">
                    {isReady ? (
                      <>
                        <CheckCircle2 size={12} /> 100% Ready for Submission
                      </>
                    ) : (
                      <>
                        <AlertTriangle size={12} /> Action Required • {totalMissingCount} Items
                        Missing
                      </>
                    )}
                  </span>
                  <h2 className="readiness-hero-title">
                    {isReady
                      ? 'Employee Portfolio is Fully Verified'
                      : 'Draft Incomplete — Mandatory Requirements Missing'}
                  </h2>
                  <p className="readiness-hero-desc">
                    {isReady
                      ? 'All mandatory identity records, workplace placement, residential addresses, and required document scans have passed validation rules. This candidate can now be forwarded to Admin for approval.'
                      : 'Please review and resolve the incomplete categories below. The draft cannot be authorized until all statutory and organizational compliance items are fulfilled.'}
                  </p>
                </div>
              </div>

              <div className="readiness-hero-actions">
                <Button
                  title="Re-validate compliance rules"
                  aria-label="Refresh validation"
                  variant="secondary"
                  disabled={readiness.isFetching}
                  onClick={() => void readiness.refetch()}
                >
                  <RefreshCw size={15} className={readiness.isFetching ? 'animate-spin' : ''} />
                  <span>{readiness.isFetching ? 'Auditing...' : 'Re-verify'}</span>
                </Button>

                <Button
                  variant="success"
                  disabled={!isReady || readiness.isFetching || action.pending || submitting}
                  onClick={() => void handleSubmit()}
                >
                  <Send size={15} />
                  <span>{submitting || action.pending ? 'Submitting...' : 'Submit to Admin'}</span>
                </Button>
              </div>
            </div>
          </div>

          <ActionMessage action={{ error: action.error, success: null }} />

          {/* KPI Summary Cards */}
          <div className="readiness-kpi-grid">
            <div className="readiness-kpi-card">
              <div
                className={`readiness-kpi-icon ${
                  !categoriesArray.some((c) => c.title.includes('Identity')) ? 'success' : 'warning'
                }`}
              >
                <UserCheck size={20} />
              </div>
              <div className="readiness-kpi-info">
                <h4>Identity & Bio</h4>
                <p>
                  {!categoriesArray.some((c) => c.title.includes('Identity'))
                    ? 'Verified ✓'
                    : 'Details Needed'}
                </p>
              </div>
            </div>

            <div className="readiness-kpi-card">
              <div
                className={`readiness-kpi-icon ${
                  !categoriesArray.some((c) => c.title.includes('Workplace'))
                    ? 'success'
                    : 'warning'
                }`}
              >
                <Building2 size={20} />
              </div>
              <div className="readiness-kpi-info">
                <h4>Placement</h4>
                <p>
                  {!categoriesArray.some((c) => c.title.includes('Workplace'))
                    ? 'Assigned ✓'
                    : 'Unassigned'}
                </p>
              </div>
            </div>

            <div className="readiness-kpi-card">
              <div
                className={`readiness-kpi-icon ${
                  !categoriesArray.some((c) => c.title.includes('Addresses'))
                    ? 'success'
                    : 'warning'
                }`}
              >
                <MapPin size={20} />
              </div>
              <div className="readiness-kpi-info">
                <h4>Address Proof</h4>
                <p>
                  {!categoriesArray.some((c) => c.title.includes('Addresses'))
                    ? 'Complete ✓'
                    : 'Address Missing'}
                </p>
              </div>
            </div>

            <div className="readiness-kpi-card">
              <div className={`readiness-kpi-icon ${isReady ? 'success' : 'warning'}`}>
                {isReady ? <CheckCircle2 size={20} /> : <Clock size={20} />}
              </div>
              <div className="readiness-kpi-info">
                <h4>Submission Status</h4>
                <p>{isReady ? 'Approved to Submit' : `${totalMissingCount} Items Incomplete`}</p>
              </div>
            </div>
          </div>

          {/* Actionable Missing Categories */}
          {!isReady && categoriesArray.length > 0 && (
            <div style={{ marginTop: '6px' }}>
              <div className="readiness-sections-header" style={{ marginBottom: '14px' }}>
                <h3 className="readiness-sections-title">
                  <AlertTriangle size={18} color="#d97706" />
                  Incomplete Onboarding Sections ({categoriesArray.length})
                </h3>
                <span style={{ fontSize: '12px', color: '#64748b' }}>
                  Click on any section to jump directly and complete details
                </span>
              </div>

              <div className="readiness-categories-grid">
                {categoriesArray.map((cat) => {
                  const CatIcon = cat.icon;
                  const linkTarget = `?tab=${cat.tab}${cat.hash ? `#${cat.hash}` : ''}`;

                  return (
                    <div className="readiness-category-card" key={cat.title}>
                      <div>
                        <div className="readiness-cat-header">
                          <div className="readiness-cat-icon">
                            <CatIcon size={20} />
                          </div>
                          <div className="readiness-cat-meta">
                            <span className="readiness-cat-tag">{cat.stepName}</span>
                            <h4 className="readiness-cat-title">{cat.title}</h4>
                          </div>
                          <span className="readiness-cat-badge">
                            {cat.items.length} {cat.items.length === 1 ? 'item' : 'items'}
                          </span>
                        </div>

                        <ul className="readiness-items-list" style={{ marginTop: '14px' }}>
                          {cat.items.map((item, idx) => (
                            <li className="readiness-item-row" key={idx}>
                              <span className="readiness-item-bullet" />
                              <span>{item}</span>
                            </li>
                          ))}
                        </ul>
                      </div>

                      <div className="readiness-cat-footer">
                        <Link to={linkTarget} className="readiness-fix-btn">
                          <span>Complete in {cat.stepName.split(':')[0]}</span>
                          <ArrowRight size={13} />
                        </Link>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </>
      )}

      {/* Selected Attachments Card */}
      <div className="submission-card">
        <h3 className="submission-card-title">
          <Folder size={18} color="#2563eb" />
          Attached Employee Documents ({selected.length})
        </h3>
        {selected.length > 0 ? (
          <ul className="submission-doc-list">
            {selected.map((docId) => {
              const matchedDoc = docs.data?.find((d) => d.id === docId);
              const isAvailable = matchedDoc?.isAvailable;

              return (
                <li className="submission-doc-item" key={docId}>
                  <div className="submission-doc-info">
                    <FileText size={16} color="#64748b" style={{ flexShrink: 0 }} />
                    <span
                      className="submission-doc-name"
                      title={matchedDoc?.fileName ?? 'Protected document'}
                    >
                      {matchedDoc?.fileName ?? 'Protected Document Reference'}
                    </span>
                  </div>
                  <span
                    className={`submission-doc-status ${isAvailable ? 'available' : 'pending'}`}
                  >
                    {isAvailable ? 'Verified ✓' : 'Processing'}
                  </span>
                </li>
              );
            })}
          </ul>
        ) : (
          <p style={{ fontSize: '13px', color: '#64748b', margin: 0 }}>
            No compliance or identity documents have been selected for this employee draft yet.
          </p>
        )}
      </div>

      {/* Hiring & Authorization Decision History */}
      <div className="submission-card">
        <h3 className="submission-card-title">
          <Clock size={18} color="#64748b" />
          Administrative Authorization History
        </h3>
        <QueryState query={decisions}>
          {!decisions.data?.length ? (
            <p style={{ fontSize: '13px', color: '#64748b', margin: 0 }}>
              No previous administrative decisions recorded. This is a fresh onboarding draft.
            </p>
          ) : (
            decisions.data.map((d) => {
              const decisionLabel =
                d.decision === 1
                  ? 'Approved'
                  : d.decision === 2
                    ? 'Returned for Correction'
                    : d.decision === 3
                      ? 'Rejected'
                      : 'Decision Recorded';

              const badgeClass =
                d.decision === 1 ? 'approved' : d.decision === 2 ? 'returned' : 'rejected';

              return (
                <div className="submission-decision-item" key={d.id}>
                  <div className="submission-decision-header">
                    <span className={`submission-decision-badge ${badgeClass}`}>
                      {decisionLabel}
                    </span>
                    <span className="submission-decision-meta">
                      {labelDate(d.decidedAt)} &middot; Reviewer #{d.actorAccountId ?? 'System'}
                    </span>
                  </div>
                  <p className="submission-decision-reason">
                    {d.reason ? `"${d.reason}"` : 'No reviewer remarks recorded.'}
                  </p>
                </div>
              );
            })
          )}
        </QueryState>
      </div>
    </div>
  );
}
