import { useEffect, useRef, useState } from 'react';
import {
  Link,
  Navigate,
  useNavigate,
  useParams,
  useSearchParams,
  useLocation,
} from 'react-router-dom';
import {
  ArrowLeft,
  ChevronLeft,
  ChevronRight,
  CreditCard,
  FileCheck,
  Folder,
  HeartPulse,
  Plus,
  Send,
  User,
  Lock,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { CardSkeleton } from '@/components/ui/Skeleton';
import { useHrAccess } from '../hooks/use-hr-access';
import { useHrAction, useHrQuery } from '../hooks/use-hr-queries';
import { profileResponse } from '../schemas/hiring';
import { currentUpdateSchema } from '../schemas/updates';
import { ApiError } from '@/services/api/api-error';
import { id, writeResult } from '../schemas/contracts';
import { onboardingLabels } from '../types/contracts';
import type { Profile } from '../types/hiring';
import { ActionMessage, Denied, QueryState } from '../components/Feedback';
import { ProfileForm } from '../components/hiring/ProfileForm';
import { SensitiveSection } from '../components/hiring/SensitiveForms';
import { EmployeeDocuments } from '../components/documents/EmployeeDocuments';
import { SubmissionReview } from '../components/hiring/SubmissionReview';
import '@/styles/onboarding-stepper.css';

export function isStep1Complete(p: Profile | null | undefined): boolean {
  if (!p) return false;
  if (!p.fullLegalName?.trim()) return false;
  if (!p.identityDocumentType) return false;
  if (!p.identityDocumentNumber?.trim()) return false;
  if (!p.dateOfBirth) return false;
  if (!p.gender?.trim()) return false;
  if (!p.personalPhone?.trim()) return false;
  if (!p.currentAddress?.line1?.trim()) return false;
  if (!p.permanentAddress?.line1?.trim()) return false;
  if (!p.emergencyContact?.name?.trim()) return false;
  if (!p.emergencyContact?.phone?.trim()) return false;
  if (!p.departmentId) return false;
  if (!p.designationId) return false;
  if (!p.staffCategoryId) return false;
  if (!p.employmentTypeId) return false;
  if (!p.reportingManagerEmployeeId) return false;
  if (!p.joinedOn) return false;
  return true;
}

const ONBOARDING_STEPS = [
  {
    key: 'profile',
    stepNumber: 1,
    stepTag: 'STEP 01',
    title: 'Profile & Workplace Placement',
    shortTitle: '1. Profile Info',
    description:
      'Enter basic legal identity, contact numbers, address, and select whether employee is Office-based or Field/Site-based.',
    icon: User,
  },
  {
    key: 'financial',
    stepNumber: 2,
    stepTag: 'STEP 02',
    title: 'Financial & Wage Structure',
    shortTitle: '2. Financial',
    description:
      'Configure salary scale, pay frequency, currency, tax rules, and bank account for payroll disbursements.',
    icon: CreditCard,
  },
  {
    key: 'medical',
    stepNumber: 3,
    stepTag: 'STEP 03',
    title: 'Medical & Fitness History',
    shortTitle: '3. Medical',
    description:
      'Record blood group, health disclosures, fitness clearances, emergency medical conditions, and insurance coverage.',
    icon: HeartPulse,
  },
  {
    key: 'documents',
    stepNumber: 4,
    stepTag: 'STEP 04',
    title: 'CNIC & Identity Documents',
    shortTitle: '4. Documents',
    description:
      'Upload verified scanned identity proof (CNIC front/back, passport), academic degrees, and experience certificates.',
    icon: Folder,
  },
  {
    key: 'medical-documents',
    stepNumber: 5,
    stepTag: 'STEP 05',
    title: 'Medical Clearance Reports',
    shortTitle: '5. Medical Docs',
    description:
      'Attach certified doctor fit-to-work letters, clinical diagnostic reports, and occupational lab test results.',
    icon: FileCheck,
  },
  {
    key: 'submission',
    stepNumber: 6,
    stepTag: 'STEP 06',
    title: 'Review & Send for Admin Approval',
    shortTitle: '6. Review & Submit',
    description:
      'Review complete employee portfolio, audit required attachments, and transmit draft to Admin for final authorization.',
    icon: Send,
  },
] as const;

export function NewEmployeePage() {
  const access = useHrAccess();
  const action = useHrAction();
  const navigate = useNavigate();
  const location = useLocation();
  const basePath = location.pathname.startsWith('/admin') ? '/admin' : '/hr';
  const createdRef = useRef(false);

  useEffect(() => {
    if (access.can('hr.employees.read', 'hr.employees.drafts.manage') && !createdRef.current) {
      createdRef.current = true;
      void action.run('employees', undefined, 'POST', writeResult).then((r) => {
        if (r) void navigate(`${basePath}/employees/${r.employeeId}/hiring`, { replace: true });
      });
    }
  }, [access, action, navigate, basePath]);

  if (!access.can('hr.employees.read', 'hr.employees.drafts.manage')) return <Denied />;
  return (
    <>
      <Link className="hr-back" to={`${basePath}/employees`}>
        <ArrowLeft size={16} />
        Employees
      </Link>
      <div className="hr-page-heading">
        <h1>Opening employee form...</h1>
      </div>
      <ActionMessage action={action} />
      {action.error ? (
        <Button
          disabled={action.pending}
          onClick={() => {
            createdRef.current = false;
            void action.run('employees', undefined, 'POST', writeResult).then((r) => {
              if (r)
                void navigate(`${basePath}/employees/${r.employeeId}/hiring`, { replace: true });
            });
          }}
        >
          <Plus size={16} />
          {action.pending ? 'Opening...' : 'Retry opening employee form'}
        </Button>
      ) : (
        <CardSkeleton />
      )}
    </>
  );
}
export function EmployeeHiringPage() {
  const { employeeId = '' } = useParams();
  return id.safeParse(employeeId).success ? (
    <HiringWorkspace key={employeeId} employeeId={employeeId} />
  ) : (
    <p role="alert">Invalid employee reference.</p>
  );
}
function HiringWorkspace({ employeeId }: { employeeId: string }) {
  const access = useHrAccess();
  const navigate = useNavigate();
  const location = useLocation();
  const basePath = location.pathname.startsWith('/admin') ? '/admin' : '/hr';
  const [params] = useSearchParams();
  const tab = params.get('tab') === 'biometrics' ? 'submission' : (params.get('tab') ?? 'profile');
  const [saved, setSaved] = useState(false);
  const needsStatusCheck = !access.can('hr.employees.identity.read');
  const current = useHrQuery(
    `employees/${employeeId}/current/basic`,
    currentUpdateSchema,
    ['hr.employees.read'],
    needsStatusCheck,
  );
  const unapproved =
    current.error instanceof ApiError && current.error.code === 'employee_not_approved';
  const query = useHrQuery(
    `employees/${employeeId}`,
    profileResponse,
    ['hr.employees.read'],
    !needsStatusCheck || unapproved,
  );
  const canShowHiring =
    access.can('hr.employees.read') &&
    (!needsStatusCheck || (!current.data && unapproved)) &&
    query.data?.employee.onboardingStatus !== 2;

  useEffect(() => {
    if (canShowHiring && query.data && tab !== 'profile' && !isStep1Complete(query.data.profile)) {
      navigate('?tab=profile', { replace: true });
    }
  }, [canShowHiring, query.data, tab, navigate]);

  if (!access.can('hr.employees.read')) return <Denied />;
  if (needsStatusCheck && current.data)
    return <Navigate to={`${basePath}/employees/${employeeId}`} replace />;
  if (needsStatusCheck && !unapproved) return <QueryState query={current}>{null}</QueryState>;
  if (query.data?.employee.onboardingStatus === 2)
    return <Navigate to={`${basePath}/employees/${employeeId}`} replace />;
  const tabs = [
    { key: 'profile', label: 'Profile', allowed: true },
    { key: 'financial', label: 'Financial', allowed: access.can('hr.employees.financial.read') },
    { key: 'medical', label: 'Medical', allowed: access.can('hr.employees.medical.read') },
    { key: 'documents', label: 'Documents', allowed: access.can('hr.employees.documents.read') },
    {
      key: 'medical-documents',
      label: 'Medical documents',
      allowed: access.can('hr.employees.documents.read', 'hr.employees.medical.read'),
    },
    { key: 'submission', label: 'Submission', allowed: true },
  ];
  const onSaved = (nextTab?: string) => {
    setSaved(true);
    void query.refetch();
    if (nextTab) {
      navigate(`?tab=${nextTab}`);
    }
  };

  const step1Done = query.data ? isStep1Complete(query.data.profile) : false;

  const stepIndex = ONBOARDING_STEPS.findIndex((s) => s.key === tab);
  const prevStep = stepIndex > 0 ? ONBOARDING_STEPS[stepIndex - 1] : null;
  const nextStep = stepIndex < ONBOARDING_STEPS.length - 1 ? ONBOARDING_STEPS[stepIndex + 1] : null;

  return (
    <>
      <Link className="hr-back" to={`${basePath}/employees`}>
        <ArrowLeft size={16} />
        Employees
      </Link>
      <QueryState query={query}>
        {query.data &&
          (() => {
            const { employee, profile } = query.data;
            const editable = employee.status === 0 && [0, 3].includes(employee.onboardingStatus);
            return (
              <>
                <div className="hr-page-heading">
                  <div>
                    <p className="hr-kicker">EMPLOYEE {employee.employeeNumber}</p>
                    <h1>{employee.name ?? 'Employee draft'}</h1>
                  </div>
                  <span className="hr-status hr-status--neutral">
                    {onboardingLabels[employee.onboardingStatus]}
                  </span>
                </div>
                {saved && (
                  <p className="hr-message" role="status">
                    Saved successfully.
                  </p>
                )}

                <div className="erp-wizard-container">
                  <nav className="erp-steps-bar" role="tablist" aria-label="Onboarding Steps">
                    {ONBOARDING_STEPS.map((s, idx) => {
                      const allowed = tabs.find((t) => t.key === s.key)?.allowed ?? false;
                      const isLocked = idx > 0 && !step1Done;
                      const isClickable = allowed && !isLocked;
                      const isActive = tab === s.key;
                      const isCompleted = stepIndex > idx || (idx === 0 && step1Done);

                      return (
                        <Link
                          key={s.key}
                          to={isClickable ? `?tab=${s.key}` : '#'}
                          title={s.title}
                          className={`erp-step-tab ${isActive ? 'is-active' : ''} ${isCompleted ? 'is-completed' : ''} ${isLocked ? 'is-locked' : ''}`}
                          onClick={(e) => {
                            if (!isClickable) e.preventDefault();
                            else setSaved(false);
                          }}
                        >
                          <span className="erp-step-title">Step {s.stepNumber}</span>
                          {isLocked && <Lock size={12} className="erp-step-lock" />}
                        </Link>
                      );
                    })}
                  </nav>
                </div>

                <div className="onboarding-step-content-box" key={tab}>
                  {!tabs.some((t) => t.key === tab && t.allowed) ? (
                    <Denied />
                  ) : tab === 'profile' ? (
                    <ProfileForm
                      key={employee.version}
                      employeeId={employeeId}
                      initial={profile}
                      version={employee.version}
                      editable={editable && access.can('hr.employees.drafts.manage')}
                      onSaved={onSaved}
                    />
                  ) : tab === 'financial' || tab === 'medical' ? (
                    <>
                      <SensitiveSection
                        key={tab}
                        employeeId={employeeId}
                        section={tab}
                        editable={editable}
                        profile={profile}
                        onSaved={() => onSaved()}
                      />
                      <div className="stepper-nav-footer">
                        <div>
                          {prevStep && (
                            <Button
                              type="button"
                              variant="secondary"
                              onClick={() => {
                                setSaved(false);
                                navigate(`?tab=${prevStep.key}`);
                              }}
                            >
                              <ChevronLeft size={16} />
                              <span>
                                Previous: Step {prevStep.stepNumber} - {prevStep.shortTitle}
                              </span>
                            </Button>
                          )}
                        </div>
                        <div className="stepper-nav-right">
                          {nextStep && (
                            <Button
                              type="button"
                              variant="success"
                              onClick={() => {
                                setSaved(false);
                                navigate(`?tab=${nextStep.key}`);
                              }}
                            >
                              <span>
                                Continue to Step {nextStep.stepNumber}: {nextStep.shortTitle}
                              </span>
                              <ChevronRight size={16} />
                            </Button>
                          )}
                        </div>
                      </div>
                    </>
                  ) : tab === 'documents' || tab === 'medical-documents' ? (
                    <>
                      <EmployeeDocuments
                        key={tab}
                        employeeId={employeeId}
                        version={employee.version}
                        editable={editable}
                        medical={tab === 'medical-documents'}
                        onSaved={() => onSaved()}
                      />
                      <div className="stepper-nav-footer">
                        <div>
                          {prevStep && (
                            <Button
                              type="button"
                              variant="secondary"
                              onClick={() => {
                                setSaved(false);
                                navigate(`?tab=${prevStep.key}`);
                              }}
                            >
                              <ChevronLeft size={16} />
                              <span>
                                Previous: Step {prevStep.stepNumber} - {prevStep.shortTitle}
                              </span>
                            </Button>
                          )}
                        </div>
                        <div className="stepper-nav-right">
                          {nextStep && (
                            <Button
                              type="button"
                              variant="success"
                              onClick={() => {
                                setSaved(false);
                                navigate(`?tab=${nextStep.key}`);
                              }}
                            >
                              <span>
                                Continue to Step {nextStep.stepNumber}: {nextStep.shortTitle}
                              </span>
                              <ChevronRight size={16} />
                            </Button>
                          )}
                        </div>
                      </div>
                    </>
                  ) : (
                    <>
                      <SubmissionReview
                        employeeId={employeeId}
                        version={employee.version}
                        editable={editable}
                        selected={profile.documentIds}
                        onSaved={() => onSaved()}
                      />
                      <div className="stepper-nav-footer">
                        <div>
                          {prevStep && (
                            <Button
                              type="button"
                              variant="secondary"
                              onClick={() => {
                                setSaved(false);
                                navigate(`?tab=${prevStep.key}`);
                              }}
                            >
                              <ChevronLeft size={16} />
                              <span>
                                Previous: Step {prevStep.stepNumber} - {prevStep.shortTitle}
                              </span>
                            </Button>
                          )}
                        </div>
                      </div>
                    </>
                  )}
                </div>
              </>
            );
          })()}
      </QueryState>
    </>
  );
}
