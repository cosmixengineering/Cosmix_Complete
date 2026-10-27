import { useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import {
  AlertCircle,
  ArrowRight,
  Building2,
  CheckCircle2,
  Info,
  MapPin,
  Plus,
  Save,
  Trash2,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { SelectInput } from '@/components/forms/SelectInput';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { pageOf, writeResult } from '../../schemas/contracts';
import { documentsSchema } from '../../schemas/hiring';
import { masterSchema } from '../../schemas/organization';
import { ActionMessage } from '../Feedback';
import { ReferenceField } from '../ReferenceField';
import {
  AddressFields,
  DocumentChoice,
  EmergencyFields,
  EnumField,
  Field,
  PhoneField,
  FormSection,
  LongField,
} from './FormFields';
import { formatCnic, validateCnic } from '../../utils/identity';
import type { Profile } from '../../types/hiring';

const GENDER_OPTIONS = [
  { value: '', label: 'Select Gender...' },
  { value: 'Male', label: 'Male' },
  { value: 'Female', label: 'Female' },
  { value: 'Other', label: 'Other' },
] as const;

const MARITAL_STATUS_OPTIONS = [
  { value: '', label: 'Select Marital Status...' },
  { value: 'Single', label: 'Single' },
  { value: 'Married', label: 'Married' },
  { value: 'Divorced', label: 'Divorced' },
  { value: 'Widowed', label: 'Widowed' },
] as const;

function validateProfile(data: Profile, isOfficeCategory: boolean): Record<string, string> {
  const errors: Record<string, string> = {};

  // Identity
  if (!data.fullLegalName?.trim()) {
    errors.fullLegalName = 'Full legal name is required.';
  }

  if (!data.identityDocumentType) {
    errors.identityDocumentType = 'Identity document type is required.';
  }

  if (!data.identityDocumentNumber?.trim()) {
    errors.identityDocumentNumber = 'Identity document number is required.';
  } else if (data.identityDocumentType === 1) {
    const cnicErr = validateCnic(data.identityDocumentNumber);
    if (cnicErr) {
      errors.identityDocumentNumber = cnicErr;
    }
  } else if (data.identityDocumentType === 2) {
    if (data.identityDocumentNumber.trim().length < 6) {
      errors.identityDocumentNumber = 'Passport number must be at least 6 characters.';
    }
  }

  if (!data.dateOfBirth) {
    errors.dateOfBirth = 'Date of birth is required.';
  } else {
    const dob = new Date(data.dateOfBirth);
    const today = new Date();
    if (dob >= today) {
      errors.dateOfBirth = 'Date of birth must be in the past.';
    }
  }

  if (!data.gender?.trim()) {
    errors.gender = 'Gender is required.';
  }

  // Contact details
  const phonePattern = /^\+92\d{10}$/;
  if (!data.personalPhone?.trim()) {
    errors.personalPhone = 'Personal phone is required.';
  } else {
    const normalized = data.personalPhone.replace(/[\s\-().]/g, '');
    if (!phonePattern.test(normalized)) {
      errors.personalPhone = 'Personal phone must be 10 digits after +92 (e.g. +923001234567).';
    }
  }

  if (data.companyPhone?.trim()) {
    const normalized = data.companyPhone.replace(/[\s\-().]/g, '');
    if (!phonePattern.test(normalized)) {
      errors.companyPhone = 'Company phone must be 10 digits after +92 (e.g. +923001234567).';
    }
  }

  const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (data.personalEmail?.trim() && !emailPattern.test(data.personalEmail.trim())) {
    errors.personalEmail = 'Personal email must be a valid email address.';
  }
  if (data.officialEmail?.trim() && !emailPattern.test(data.officialEmail.trim())) {
    errors.officialEmail = 'Official email must be a valid email address.';
  }

  // Current Address
  if (!data.currentAddress?.line1?.trim()) {
    errors['currentAddress.line1'] = 'Current address line 1 is required.';
  }
  if (!data.currentAddress?.province?.trim()) {
    errors['currentAddress.province'] = 'Current address province is required.';
  }
  if (!data.currentAddress?.cityId && !data.currentAddress?.city?.trim()) {
    errors['currentAddress.city'] = 'Current address city is required.';
  }

  // Permanent Address
  if (!data.permanentAddress?.line1?.trim()) {
    errors['permanentAddress.line1'] = 'Permanent address line 1 is required.';
  }
  if (!data.permanentAddress?.province?.trim()) {
    errors['permanentAddress.province'] = 'Permanent address province is required.';
  }
  if (!data.permanentAddress?.cityId && !data.permanentAddress?.city?.trim()) {
    errors['permanentAddress.city'] = 'Permanent address city is required.';
  }

  // Emergency Contact
  if (!data.emergencyContact?.name?.trim()) {
    errors['emergencyContact.name'] = 'Emergency contact name is required.';
  }
  if (!data.emergencyContact?.relationship?.trim()) {
    errors['emergencyContact.relationship'] = 'Emergency contact relationship is required.';
  }
  if (!data.emergencyContact?.phone?.trim()) {
    errors['emergencyContact.phone'] = 'Emergency contact phone is required.';
  } else {
    const normalized = data.emergencyContact.phone.replace(/[\s\-().]/g, '');
    if (!phonePattern.test(normalized)) {
      errors['emergencyContact.phone'] =
        'Emergency phone must be 10 digits after +92 (e.g. +923001234567).';
    }
  }

  if (data.emergencyContact?.alternatePhone?.trim()) {
    const normalized = data.emergencyContact.alternatePhone.replace(/[\s\-().]/g, '');
    if (!phonePattern.test(normalized)) {
      errors['emergencyContact.alternatePhone'] =
        'Emergency alternate phone must be 10 digits after +92 (e.g. +923001234567).';
    }
  }

  // Employment
  if (!data.departmentId) {
    errors.departmentId = 'Department is required.';
  }
  if (!data.designationId) {
    errors.designationId = 'Designation is required.';
  }
  if (!data.staffCategoryId) {
    errors.staffCategoryId =
      'Please select Workplace Placement (Office Based Staff or Site / Field Operations).';
  }
  if (!data.employmentTypeId) {
    errors.employmentTypeId = 'Employment type is required.';
  }
  if (isOfficeCategory && !data.branchId) {
    errors.branchId = 'Office branch is required for office staff.';
  }
  if (!data.reportingManagerEmployeeId) {
    errors.reportingManagerEmployeeId = 'Reporting manager is required.';
  }
  if (!data.joinedOn) {
    errors.joinedOn = 'Joining date is required.';
  }
  if (data.joinedOn && data.probationEndsOn) {
    if (new Date(data.probationEndsOn) < new Date(data.joinedOn)) {
      errors.probationEndsOn = 'Probation end date cannot be earlier than joining date.';
    }
  }

  // Mobile
  if (data.mobileAccessRequested) {
    if (!data.loginPhoneNumber?.trim()) {
      errors.loginPhoneNumber =
        'Proposed mobile login phone is required when mobile access is requested.';
    } else {
      const normalized = data.loginPhoneNumber.replace(/[\s\-().]/g, '');
      if (!phonePattern.test(normalized)) {
        errors.loginPhoneNumber =
          'Proposed mobile phone must be 10 digits after +92 (e.g. +923001234567).';
      }
    }
  }

  // Previous employment array items validation
  data.previousEmployment?.forEach((row, i) => {
    if (!row.employerName?.trim()) {
      errors[`previousEmployment[${i}].employerName`] = 'Employer name is required.';
    }
    if (!row.roleTitle?.trim()) {
      errors[`previousEmployment[${i}].roleTitle`] = 'Role title is required.';
    }
    if (!row.startedOn) {
      errors[`previousEmployment[${i}].startedOn`] = 'Employment started on date is required.';
    }
  });

  // Certifications array items validation
  data.certifications?.forEach((row, i) => {
    if (!row.type?.trim()) errors[`certifications[${i}].type`] = 'Certification type is required.';
    if (!row.number?.trim())
      errors[`certifications[${i}].number`] = 'Certificate number is required.';
    if (!row.issuer?.trim()) errors[`certifications[${i}].issuer`] = 'Issuer is required.';
    if (!row.issuedOn) errors[`certifications[${i}].issuedOn`] = 'Issued on date is required.';
    if (!row.documentId)
      errors[`certifications[${i}].documentId`] = 'Certificate evidence document is required.';
  });

  // Assets array items validation
  data.assets?.forEach((row, i) => {
    if (!row.assetReference?.trim())
      errors[`assets[${i}].assetReference`] = 'Asset reference is required.';
    if (!row.issuedOn) errors[`assets[${i}].issuedOn`] = 'Asset issued on date is required.';
    if (!row.issueCondition?.trim())
      errors[`assets[${i}].issueCondition`] = 'Issue condition is required.';
  });

  return errors;
}

const SERVER_ERROR_TO_FIELD: Record<string, { field: string; message: string }> = {
  ineligible_reporting_manager: {
    field: 'reportingManagerEmployeeId',
    message: 'Selected reporting manager is ineligible. Please select an active and approved manager (e.g. Engr. Imran Saeed).',
  },
  invalid_employee_phone: {
    field: 'personalPhone',
    message: 'Personal phone number must follow Pakistani format (+92 followed by 10 digits).',
  },
  invalid_employee_identity_document: {
    field: 'identityDocumentNumber',
    message: 'Identity document number format is invalid (CNIC must be 13 digits, Passport min 6 chars).',
  },
  invalid_employee_date_of_birth: {
    field: 'dateOfBirth',
    message: 'Date of birth must be a valid date in the past.',
  },
  invalid_employee_email: {
    field: 'officialEmail',
    message: 'Please provide a valid email format for Official Email or Personal Email.',
  },
  site_staff_cannot_select_office: {
    field: 'branchId',
    message: 'Site / Field Operations staff cannot have an office branch assigned.',
  },
  select_staff_category_before_office: {
    field: 'staffCategoryId',
    message: 'Please select Workplace Placement before choosing an office branch.',
  },
  unsupported_staff_category: {
    field: 'staffCategoryId',
    message: 'The selected workplace category is invalid or inactive.',
  },
  employee_login_phone_required: {
    field: 'loginPhoneNumber',
    message: 'Proposed mobile login phone is required when Mobile Access is checked.',
  },
  employee_login_phone_conflict: {
    field: 'loginPhoneNumber',
    message: 'This mobile login phone is already registered to another user account.',
  },
  employee_identity_or_email_conflict: {
    field: 'identityDocumentNumber',
    message: 'An employee with this CNIC/Passport or Official Email is already registered.',
  },
  employee_company_sim_conflict: {
    field: 'companyPhone',
    message: 'This company SIM phone number is already assigned to another employee.',
  },
  inactive_or_unknown_employee_master: {
    field: 'departmentId',
    message: 'One of the selected items (Department, Designation, or Employment Type) is inactive.',
  },
  invalid_employee_emergency_contact: {
    field: 'emergencyContact.phone',
    message: 'Emergency contact details are incomplete or phone format is invalid.',
  },
};

const FIELD_FRIENDLY_NAMES: Record<string, string> = {
  fullLegalName: 'Full Legal Name',
  identityDocumentType: 'Identity Document Type',
  identityDocumentNumber: 'Identity Document Number',
  dateOfBirth: 'Date of Birth',
  gender: 'Gender',
  personalPhone: 'Personal Phone',
  'currentAddress.line1': 'Current Address Line 1',
  'currentAddress.province': 'Current Address Province',
  'currentAddress.city': 'Current Address City',
  'permanentAddress.line1': 'Permanent Address Line 1',
  'permanentAddress.province': 'Permanent Address Province',
  'permanentAddress.city': 'Permanent Address City',
  'emergencyContact.name': 'Emergency Contact Name',
  'emergencyContact.relationship': 'Emergency Relationship',
  'emergencyContact.phone': 'Emergency Phone',
  departmentId: 'Department',
  designationId: 'Designation',
  staffCategoryId: 'Workplace Placement',
  employmentTypeId: 'Employment Type',
  branchId: 'Office Branch',
  reportingManagerEmployeeId: 'Reporting Manager',
  joinedOn: 'Joining Date',
  loginPhoneNumber: 'Proposed Mobile Login Phone',
};

function scrollToField(fieldKey: string) {
  setTimeout(() => {
    let target = document.querySelector(`[data-field="${fieldKey}"]`) as HTMLElement | null;

    if (!target && fieldKey.includes('.')) {
      const parts = fieldKey.split('.');
      target = document.querySelector(`[data-field="${fieldKey}"], [name="${parts[1]}"]`) as HTMLElement | null;
    }

    if (!target) {
      target = document.querySelector(`[name="${fieldKey}"], #${fieldKey}`) as HTMLElement | null;
    }

    if (!target) {
      target = document.querySelector('[aria-invalid="true"], [data-invalid="true"]') as HTMLElement | null;
    }

    if (target) {
      target.scrollIntoView({ behavior: 'smooth', block: 'center' });
      target.classList.add('field-error-pulse');
      setTimeout(() => target?.classList.remove('field-error-pulse'), 2500);

      const input =
        target.tagName === 'INPUT' || target.tagName === 'SELECT' || target.tagName === 'TEXTAREA'
          ? target
          : (target.querySelector('input, select, textarea') as HTMLElement | null);
      input?.focus?.();
    }
  }, 70);
}

export function ProfileForm({
  employeeId,
  initial,
  version,
  editable,
  onSaved,
}: {
  employeeId: string;
  initial: Profile;
  version: string;
  editable: boolean;
  onSaved: (nextTab?: string) => void;
}) {
  const { hash } = useLocation();
  useEffect(() => {
    if (hash) document.getElementById(hash.slice(1))?.scrollIntoView({ block: 'start' });
  }, [hash]);
  const [data, setData] = useState(initial);
  const [baseline, setBaseline] = useState(JSON.stringify(initial));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const handleFailure = (failure: unknown) => {
    if (
      failure &&
      typeof failure === 'object' &&
      'code' in failure &&
      typeof (failure as { code?: string }).code === 'string'
    ) {
      const code = (failure as { code: string }).code;
      if (SERVER_ERROR_TO_FIELD[code]) {
        const mapped = SERVER_ERROR_TO_FIELD[code]!;
        setErrors((prev) => ({
          ...prev,
          [mapped.field]: mapped.message,
        }));
        scrollToField(mapped.field);
        return;
      }
    }
    setTimeout(() => {
      document.getElementById('profile-top-alert')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 50);
  };
  const action = useHrAction();
  useDirtyForm(editable && JSON.stringify(data) !== baseline);
  const docs = useHrQuery(`employees/${employeeId}/documents`, documentsSchema, [
    'hr.employees.read',
    'hr.employees.documents.read',
  ]);
  const category = useHrQuery(
    `staff-categories/${data.staffCategoryId}`,
    masterSchema,
    ['hr.organization.read'],
    !!data.staffCategoryId,
  );
  const allCategoriesQuery = useHrQuery('staff-categories?pageSize=50', pageOf(masterSchema), [
    'hr.organization.read',
  ]);

  const categories = allCategoriesQuery.data?.items ?? [];
  const officeCategory =
    categories.find(
      (c) =>
        c.code?.toUpperCase() === 'OFFICE' ||
        c.code?.toUpperCase() === 'HQ' ||
        c.name?.toLowerCase().includes('office') ||
        c.name?.toLowerCase().includes('corporate'),
    ) || categories[0];
  const siteCategory =
    categories.find(
      (c) =>
        c.code?.toUpperCase() === 'SITE' ||
        c.code?.toUpperCase() === 'FIELD' ||
        c.name?.toLowerCase().includes('site') ||
        c.name?.toLowerCase().includes('field'),
    ) || (categories.length > 1 ? categories[1] : undefined);

  const [placementPref, setPlacementPref] = useState<'office' | 'site' | null>(null);

  useEffect(() => {
    if (
      placementPref === 'office' &&
      officeCategory &&
      data.staffCategoryId !== officeCategory.id
    ) {
      setData((p) => ({ ...p, staffCategoryId: officeCategory.id }));
    } else if (
      placementPref === 'site' &&
      siteCategory &&
      data.staffCategoryId !== siteCategory.id
    ) {
      setData((p) => ({ ...p, staffCategoryId: siteCategory.id, branchId: null }));
    }
  }, [placementPref, officeCategory, siteCategory, data.staffCategoryId]);

  const isOffice =
    placementPref === 'office' ||
    (category.data?.code && category.data.code.toUpperCase() === 'OFFICE') ||
    (!!officeCategory && data.staffCategoryId === officeCategory.id);
  const isSite =
    placementPref === 'site' ||
    (category.data?.code &&
      (category.data.code.toUpperCase() === 'SITE' ||
        category.data.code.toUpperCase() === 'FIELD')) ||
    (!!siteCategory && data.staffCategoryId === siteCategory.id);

  const set = <K extends keyof Profile>(key: K, value: Profile[K]) => {
    setData((p) => ({ ...p, [key]: value }));
    setErrors((prev) => {
      let hasMatch = false;
      const next = { ...prev };
      for (const k in next) {
        if (k === key || k.startsWith(key + '.') || k.startsWith(key + '[')) {
          delete next[k];
          hasMatch = true;
        }
      }
      return hasMatch ? next : prev;
    });
  };

  const handleSave = async (advanceToTab?: string) => {
    const isOfficeNow = isOffice || (category.data?.code ? category.data.code === 'OFFICE' : false);
    const validationErrors = validateProfile(data, isOfficeNow);
    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      const firstField = Object.keys(validationErrors)[0];
      if (firstField) scrollToField(firstField);
      return;
    }
    setErrors({});
    const isSiteNow = isSite || (category.data?.code ? category.data.code === 'SITE' : false);
    const result = await action.run(
      `employees/${employeeId}/profile`,
      {
        expectedVersion: version,
        data: { ...data, branchId: isSiteNow ? null : data.branchId },
      },
      'PUT',
      writeResult,
      handleFailure,
    );
    if (result) {
      setBaseline(JSON.stringify(data));
      onSaved(advanceToTab);
    }
  };

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void handleSave('financial');
      }}
    >
      {(Object.keys(errors).length > 0 || !!action.error) && (
        <div id="profile-top-alert" className="modern-error-banner" role="alert">
          <div className="modern-error-header">
            <AlertCircle size={20} />
            <span>
              {Object.keys(errors).length > 0
                ? `${Object.keys(errors).length} mandatory field${Object.keys(errors).length > 1 ? 's' : ''} require attention:`
                : 'Action could not be saved:'}
            </span>
          </div>
          <p className="modern-error-message">
            {action.error ||
              'Please complete all required fields marked with * and ensure formats are valid. Click any field below to jump directly to it:'}
          </p>
          {Object.keys(errors).length > 0 && (
            <div className="modern-error-list">
              {Object.entries(errors).map(([key]) => (
                <button
                  key={key}
                  type="button"
                  className="modern-error-pill"
                  onClick={() => scrollToField(key)}
                >
                  <span>&bull; {FIELD_FRIENDLY_NAMES[key] || key}</span>
                  <ArrowRight size={12} />
                </button>
              ))}
            </div>
          )}
        </div>
      )}
      <fieldset disabled={!editable || action.pending}>
        <FormSection
          title="Identity Information"
          subtitle="Please complete employee legal identity documents, full name, and birth details."
          id="identity"
        >
          <Field
            label="Full legal name"
            required
            dataField="fullLegalName"
            error={errors.fullLegalName}
            value={data.fullLegalName}
            onChange={(v) => set('fullLegalName', v)}
          />
          <Field
            label="Father or spouse name"
            dataField="fatherOrSpouseName"
            value={data.fatherOrSpouseName}
            onChange={(v) => set('fatherOrSpouseName', v)}
          />
          <EnumField
            label="Identity document type"
            required
            dataField="identityDocumentType"
            error={errors.identityDocumentType}
            value={data.identityDocumentType}
            options={['CNIC', 'Passport']}
            onChange={(v) => {
              const newType = v as 1 | 2 | null;
              if (newType === 1 && data.identityDocumentNumber) {
                const formatted = formatCnic(data.identityDocumentNumber);
                set('identityDocumentNumber', formatted);
              }
              set('identityDocumentType', newType);
            }}
          />
          <Field
            label="Identity document number"
            required
            dataField="identityDocumentNumber"
            error={errors.identityDocumentNumber}
            value={data.identityDocumentNumber}
            placeholder={data.identityDocumentType === 2 ? 'Passport Number' : '42101-1234567-1'}
            max={data.identityDocumentType === 2 ? 20 : 15}
            onChange={(v) => {
              if (data.identityDocumentType !== 2) {
                const formatted = v ? formatCnic(v) : null;
                set('identityDocumentNumber', formatted);
                const digits = (formatted || '').replace(/\D/g, '');
                if (digits.length > 0 && digits.length < 13) {
                  setErrors((prev) => ({
                    ...prev,
                    identityDocumentNumber: `CNIC must be 13 digits (${digits.length}/13 entered). Format: 42101-1234567-1`,
                  }));
                } else {
                  setErrors((prev) => {
                    const next = { ...prev };
                    delete next.identityDocumentNumber;
                    return next;
                  });
                }
              } else {
                set('identityDocumentNumber', v);
              }
            }}
          />
          <Field
            label="Identity expiry date"
            dataField="identityDocumentExpiresOn"
            value={data.identityDocumentExpiresOn}
            type="date"
            onChange={(v) => set('identityDocumentExpiresOn', v)}
          />
          <Field
            label="Date of birth"
            required
            dataField="dateOfBirth"
            error={errors.dateOfBirth}
            value={data.dateOfBirth}
            type="date"
            onChange={(v) => set('dateOfBirth', v)}
          />
          <SelectInput
            label="Gender"
            required
            data-field="gender"
            error={errors.gender}
            value={data.gender ?? ''}
            options={GENDER_OPTIONS}
            onChange={(e) => set('gender', e.target.value || null)}
          />
          <SelectInput
            label="Marital status"
            data-field="maritalStatus"
            error={errors.maritalStatus}
            value={data.maritalStatus ?? ''}
            options={MARITAL_STATUS_OPTIONS}
            onChange={(e) => set('maritalStatus', e.target.value || null)}
          />
        </FormSection>
        <FormSection
          title="Contact Details"
          subtitle="Phone numbers and official email addresses for internal communication."
          id="contacts"
        >
          <PhoneField
            label="Personal phone"
            required
            error={errors.personalPhone}
            value={data.personalPhone}
            onChange={(v) => {
              set('personalPhone', v);
              const digits = (v || '').replace(/\D/g, '').slice(2);
              if (digits.length > 0 && digits.length < 10) {
                setErrors((prev) => ({
                  ...prev,
                  personalPhone: `Personal phone must be 10 digits after +92 (${digits.length}/10 entered).`,
                }));
              } else {
                setErrors((prev) => {
                  const next = { ...prev };
                  delete next.personalPhone;
                  return next;
                });
              }
            }}
          />
          <PhoneField
            label="Company phone"
            error={errors.companyPhone}
            value={data.companyPhone}
            onChange={(v) => {
              set('companyPhone', v);
              const digits = (v || '').replace(/\D/g, '').slice(2);
              if (digits.length > 0 && digits.length < 10) {
                setErrors((prev) => ({
                  ...prev,
                  companyPhone: `Company phone must be 10 digits after +92 (${digits.length}/10 entered).`,
                }));
              } else {
                setErrors((prev) => {
                  const next = { ...prev };
                  delete next.companyPhone;
                  return next;
                });
              }
            }}
          />
          <Field
            label="Personal email"
            dataField="personalEmail"
            error={errors.personalEmail}
            type="email"
            max={320}
            value={data.personalEmail}
            onChange={(v) => set('personalEmail', v)}
          />
          <Field
            label="Official email"
            dataField="officialEmail"
            error={errors.officialEmail}
            type="email"
            max={320}
            value={data.officialEmail}
            onChange={(v) => set('officialEmail', v)}
          />
        </FormSection>
        <AddressFields
          label="Current address"
          required
          prefix="currentAddress"
          errors={errors}
          value={data.currentAddress}
          onChange={(v) => set('currentAddress', v)}
        />
        <AddressFields
          label="Permanent address"
          required
          prefix="permanentAddress"
          errors={errors}
          value={data.permanentAddress}
          onChange={(v) => set('permanentAddress', v)}
        />
        <EmergencyFields
          required
          errors={errors}
          value={data.emergencyContact}
          onChange={(v) => set('emergencyContact', v)}
        />
        <FormSection
          title="Workplace Placement (Office vs Site)"
          subtitle="Specify whether this employee will be based in a registered office branch or deployed to active field/construction sites."
          id="placement"
        >
          <div className="workplace-placement-grid" data-field="staffCategoryId" style={{ gridColumn: '1 / -1' }}>
            <div
              className={`placement-card ${isOffice ? 'is-selected' : ''}`}
              role="button"
              tabIndex={0}
              onClick={() => {
                setPlacementPref('office');
                if (officeCategory) {
                  setData((p) => ({ ...p, staffCategoryId: officeCategory.id }));
                  setErrors((prev) => {
                    const next = { ...prev };
                    delete next.staffCategoryId;
                    return next;
                  });
                }
              }}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  setPlacementPref('office');
                  if (officeCategory)
                    setData((p) => ({ ...p, staffCategoryId: officeCategory.id }));
                }
              }}
            >
              <div className="placement-card-icon">
                <Building2 size={24} />
              </div>
              <div className="placement-card-body">
                <h3>
                  Office Based Staff
                  {isOffice && <CheckCircle2 size={16} color="#2563eb" />}
                </h3>
                <p>
                  Stationed at a company office / branch. Requires an office branch selection for
                  daily attendance & desk assignments.
                </p>
              </div>
            </div>

            <div
              className={`placement-card ${isSite ? 'is-selected' : ''}`}
              role="button"
              tabIndex={0}
              onClick={() => {
                setPlacementPref('site');
                if (siteCategory) {
                  setData((p) => ({ ...p, staffCategoryId: siteCategory.id, branchId: null }));
                  setErrors((prev) => {
                    const next = { ...prev };
                    delete next.staffCategoryId;
                    delete next.branchId;
                    return next;
                  });
                }
              }}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  setPlacementPref('site');
                  if (siteCategory)
                    setData((p) => ({ ...p, staffCategoryId: siteCategory.id, branchId: null }));
                }
              }}
            >
              <div className="placement-card-icon">
                <MapPin size={24} />
              </div>
              <div className="placement-card-body">
                <h3>
                  Site / Field Operations
                  {isSite && <CheckCircle2 size={16} color="#2563eb" />}
                </h3>
                <p>
                  Field & project site deployment. No office branch needed. Active project site,
                  geofencing & shift assigned by Admin.
                </p>
              </div>
            </div>
          </div>

          {errors.staffCategoryId && (
            <div
              style={{
                gridColumn: '1 / -1',
                color: '#dc2626',
                fontSize: '13px',
                fontWeight: 600,
                marginTop: '-6px',
                marginBottom: '10px',
              }}
            >
              {errors.staffCategoryId}
            </div>
          )}

          {isOffice && (
            <div
              style={{
                gridColumn: '1 / -1',
                background: '#f8fafc',
                padding: '16px',
                borderRadius: '10px',
                border: '1px solid #e2e8f0',
                marginBottom: '8px',
              }}
            >
              <ReferenceField
                label="Assigned Office Branch"
                kind="branches"
                required
                dataField="branchId"
                error={errors.branchId}
                value={data.branchId}
                onChange={(v) => set('branchId', v)}
              />
              <p style={{ margin: '6px 0 0', fontSize: '12px', color: '#64748b' }}>
                Select the physical branch or headquarters where the employee will report.
              </p>
            </div>
          )}

          {isSite && (
            <div className="placement-notice-box is-site" style={{ gridColumn: '1 / -1' }}>
              <Info className="placement-notice-icon" size={20} />
              <div>
                <strong>Site Staff Deployment Mode Activated:</strong>
                <p style={{ margin: '4px 0 0' }}>
                  This employee will not be assigned to a physical office branch. After profile
                  submission, active site boundaries, GPS geofencing, and site supervisor rosters
                  will be assigned from the <strong>Admin Dashboard</strong>.
                </p>
              </div>
            </div>
          )}
        </FormSection>
        <FormSection
          title="Employment Details"
          subtitle="Department, designation, reporting hierarchy, and joining timeline."
          id="employment"
        >
          <ReferenceField
            label="Department"
            kind="departments"
            required
            dataField="departmentId"
            error={errors.departmentId}
            value={data.departmentId}
            onChange={(v) => set('departmentId', v)}
          />
          <ReferenceField
            label="Designation"
            kind="designations"
            required
            dataField="designationId"
            error={errors.designationId}
            value={data.designationId}
            onChange={(v) => set('designationId', v)}
          />
          <ReferenceField
            label="Employment type"
            kind="employment-types"
            required
            dataField="employmentTypeId"
            error={errors.employmentTypeId}
            value={data.employmentTypeId}
            onChange={(v) => set('employmentTypeId', v)}
          />
          <ReferenceField
            label="Reporting manager"
            kind="employees"
            required
            dataField="reportingManagerEmployeeId"
            error={errors.reportingManagerEmployeeId}
            excludeId={employeeId}
            value={data.reportingManagerEmployeeId}
            onChange={(v) => set('reportingManagerEmployeeId', v)}
          />
          <Field
            label="Joining date"
            required
            dataField="joinedOn"
            error={errors.joinedOn}
            type="date"
            value={data.joinedOn}
            onChange={(v) => set('joinedOn', v)}
          />
          <Field
            label="Probation end date"
            dataField="probationEndsOn"
            error={errors.probationEndsOn}
            type="date"
            value={data.probationEndsOn}
            onChange={(v) => set('probationEndsOn', v)}
          />
          {data.proposedShiftId && (
            <Field
              label="Existing proposed shift reference"
              readOnly
              dataField="proposedShiftId"
              value={data.proposedShiftId}
              onChange={() => undefined}
            />
          )}
        </FormSection>
        <FormSection
          title="Mobile access and company SIM"
          subtitle="Request mobile app login access and optional company-issued SIM."
          id="mobile"
        >
          <div className="col-span-full" style={{ gridColumn: '1 / -1', marginBottom: '8px' }}>
            <label className="hr-check">
              <input
                type="checkbox"
                checked={data.mobileAccessRequested}
                onChange={(e) => set('mobileAccessRequested', e.target.checked)}
              />
              Mobile access requested
            </label>
          </div>
          <PhoneField
            label="Proposed mobile login phone"
            required={data.mobileAccessRequested}
            dataField="loginPhoneNumber"
            error={errors.loginPhoneNumber}
            value={data.loginPhoneNumber}
            onChange={(v) => {
              set('loginPhoneNumber', v);
              const digits = (v || '').replace(/\D/g, '').slice(2);
              if (digits.length > 0 && digits.length < 10) {
                setErrors((prev) => ({
                  ...prev,
                  loginPhoneNumber: `Proposed phone must be 10 digits after +92 (${digits.length}/10 entered).`,
                }));
              } else {
                setErrors((prev) => {
                  const next = { ...prev };
                  delete next.loginPhoneNumber;
                  return next;
                });
              }
            }}
          />
          <Field
            label="SIM issued on"
            dataField="simIssuedOn"
            type="date"
            value={data.simIssuedOn}
            onChange={(v) => set('simIssuedOn', v)}
          />
          <Field
            label="SIM status"
            dataField="simStatus"
            max={40}
            value={data.simStatus}
            onChange={(v) => set('simStatus', v)}
          />
          <Field
            label="SIM credit tier"
            dataField="simCreditTier"
            max={50}
            value={data.simCreditTier}
            onChange={(v) => set('simCreditTier', v)}
          />
        </FormSection>
        <section className="hr-form-section" id="previous-employment">
          <h2>Previous employment</h2>
          {data.previousEmployment.map((row, i) => {
            const update = (patch: Partial<typeof row>) => {
              const updated = data.previousEmployment.map((r, n) =>
                n === i ? { ...r, ...patch } : r,
              );
              set('previousEmployment', updated);
            };
            return (
              <div className="hr-repeat" key={row.entryId}>
                <div className="hr-repeat-heading">
                  <h3>Employment {i + 1}</h3>
                  <Button
                    variant="ghost"
                    title="Remove employment"
                    aria-label={`Remove employment ${i + 1}`}
                    onClick={() =>
                      set(
                        'previousEmployment',
                        data.previousEmployment.filter((_, n) => n !== i),
                      )
                    }
                  >
                    <Trash2 size={16} />
                  </Button>
                </div>
                <div className="hr-form-grid">
                  <Field
                    label="Employer name"
                    required
                    error={errors[`previousEmployment[${i}].employerName`]}
                    value={row.employerName}
                    onChange={(v) => update({ employerName: v })}
                  />
                  <Field
                    label="Role title"
                    required
                    error={errors[`previousEmployment[${i}].roleTitle`]}
                    max={150}
                    value={row.roleTitle}
                    onChange={(v) => update({ roleTitle: v })}
                  />
                  <Field
                    label="Employment started on"
                    required
                    error={errors[`previousEmployment[${i}].startedOn`]}
                    type="date"
                    value={row.startedOn}
                    onChange={(v) => update({ startedOn: v })}
                  />
                  <Field
                    label="Employment ended on"
                    type="date"
                    value={row.endedOn}
                    onChange={(v) => update({ endedOn: v })}
                  />
                  <LongField
                    label="Leaving reason"
                    value={row.leavingReason}
                    onChange={(v) => update({ leavingReason: v })}
                  />
                </div>
              </div>
            );
          })}
          {editable && (
            <Button
              variant="secondary"
              disabled={data.previousEmployment.length >= 100}
              onClick={() =>
                set('previousEmployment', [
                  ...data.previousEmployment,
                  {
                    entryId: crypto.randomUUID(),
                    employerName: null,
                    roleTitle: null,
                    startedOn: null,
                    endedOn: null,
                    leavingReason: null,
                  },
                ])
              }
            >
              <Plus size={16} />
              Add employment
            </Button>
          )}
        </section>
        <section className="hr-form-section" id="certifications">
          <h2>Certifications</h2>
          {data.certifications.map((row, i) => {
            const update = (patch: Partial<typeof row>) => {
              const updated = data.certifications.map((r, n) => (n === i ? { ...r, ...patch } : r));
              set('certifications', updated);
            };
            return (
              <div className="hr-repeat" key={i}>
                <div className="hr-repeat-heading">
                  <h3>Certification {i + 1}</h3>
                  <Button
                    variant="ghost"
                    title="Remove certification"
                    aria-label={`Remove certification ${i + 1}`}
                    onClick={() =>
                      set(
                        'certifications',
                        data.certifications.filter((_, n) => n !== i),
                      )
                    }
                  >
                    <Trash2 size={16} />
                  </Button>
                </div>
                <div className="hr-form-grid">
                  <Field
                    label="Certification type"
                    required
                    error={errors[`certifications[${i}].type`]}
                    max={100}
                    value={row.type}
                    onChange={(v) => update({ type: v })}
                  />
                  <Field
                    label="Certificate number"
                    required
                    error={errors[`certifications[${i}].number`]}
                    max={100}
                    value={row.number}
                    onChange={(v) => update({ number: v })}
                  />
                  <Field
                    label="Issuer"
                    required
                    error={errors[`certifications[${i}].issuer`]}
                    value={row.issuer}
                    onChange={(v) => update({ issuer: v })}
                  />
                  <Field
                    label="Certificate issued on"
                    required
                    error={errors[`certifications[${i}].issuedOn`]}
                    type="date"
                    value={row.issuedOn}
                    onChange={(v) => update({ issuedOn: v })}
                  />
                  <Field
                    label="Certificate expires on"
                    type="date"
                    value={row.expiresOn}
                    onChange={(v) => update({ expiresOn: v })}
                  />
                  <DocumentChoice
                    label="Certificate evidence"
                    documents={docs.data ?? []}
                    value={row.documentId}
                    onChange={(v) => update({ documentId: v })}
                  />
                </div>
              </div>
            );
          })}
          {editable && (
            <Button
              variant="secondary"
              disabled={data.certifications.length >= 100}
              onClick={() =>
                set('certifications', [
                  ...data.certifications,
                  {
                    type: null,
                    number: null,
                    issuer: null,
                    issuedOn: null,
                    expiresOn: null,
                    documentId: null,
                  },
                ])
              }
            >
              <Plus size={16} />
              Add certification
            </Button>
          )}
        </section>
        <section className="hr-form-section" id="assets">
          <h2>Asset evidence</h2>
          {data.assets.map((row, i) => {
            const update = (patch: Partial<typeof row>) => {
              const updated = data.assets.map((r, n) => (n === i ? { ...r, ...patch } : r));
              set('assets', updated);
            };
            return (
              <div className="hr-repeat" key={i}>
                <div className="hr-repeat-heading">
                  <h3>Asset {i + 1}</h3>
                  <Button
                    variant="ghost"
                    title="Remove asset"
                    aria-label={`Remove asset ${i + 1}`}
                    onClick={() =>
                      set(
                        'assets',
                        data.assets.filter((_, n) => n !== i),
                      )
                    }
                  >
                    <Trash2 size={16} />
                  </Button>
                </div>
                <div className="hr-form-grid">
                  <Field
                    label="Asset reference"
                    required
                    error={errors[`assets[${i}].assetReference`]}
                    max={100}
                    value={row.assetReference}
                    onChange={(v) => update({ assetReference: v })}
                  />
                  <Field
                    label="Asset issued on"
                    required
                    error={errors[`assets[${i}].issuedOn`]}
                    type="date"
                    value={row.issuedOn}
                    onChange={(v) => update({ issuedOn: v })}
                  />
                  <LongField
                    label="Issue condition"
                    required
                    error={errors[`assets[${i}].issueCondition`]}
                    value={row.issueCondition}
                    onChange={(v) => update({ issueCondition: v })}
                  />
                  <Field
                    label="Asset returned on"
                    type="date"
                    value={row.returnedOn}
                    onChange={(v) => update({ returnedOn: v })}
                  />
                  <Field
                    label="Return recipient account ID"
                    max={36}
                    pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
                    value={row.returnedToAccountId}
                    onChange={(v) => update({ returnedToAccountId: v })}
                  />
                  <LongField
                    label="Return condition"
                    value={row.returnCondition}
                    onChange={(v) => update({ returnCondition: v })}
                  />
                  <DocumentChoice
                    label="Asset evidence document"
                    documents={docs.data ?? []}
                    value={row.evidenceDocumentId}
                    onChange={(v) => update({ evidenceDocumentId: v })}
                  />
                </div>
              </div>
            );
          })}
          {editable && (
            <Button
              variant="secondary"
              disabled={data.assets.length >= 100}
              onClick={() =>
                set('assets', [
                  ...data.assets,
                  {
                    assetReference: null,
                    issuedOn: null,
                    returnedOn: null,
                    issueCondition: null,
                    returnCondition: null,
                    returnedToAccountId: null,
                    evidenceDocumentId: null,
                  },
                ])
              }
            >
              <Plus size={16} />
              Add asset
            </Button>
          )}
        </section>
        <section className="hr-form-section" id="manifest">
          <h2>Submission documents</h2>
          {(docs.data ?? []).map((d) => (
            <label className="hr-check" key={d.id}>
              <input
                type="checkbox"
                checked={data.documentIds.includes(d.id)}
                onChange={(e) =>
                  set(
                    'documentIds',
                    e.target.checked
                      ? [...data.documentIds, d.id]
                      : data.documentIds.filter((x) => x !== d.id),
                  )
                }
              />
              {d.fileName}
              <span className="muted">{d.isAvailable ? 'Available' : 'Unavailable'}</span>
            </label>
          ))}
          {data.documentIds
            .filter((x) => !docs.data?.some((d) => d.id === x))
            .map((x) => (
              <p className="muted" key={x}>
                Protected document reference: {x}
              </p>
            ))}
          {docs.isError && (
            <p role="alert">Document list unavailable. Existing selections are preserved.</p>
          )}
        </section>
      </fieldset>
      <ActionMessage action={{ error: action.error, success: null }} />
      {editable && (
        <div className="stepper-nav-footer">
          <div>
            <Button
              type="button"
              variant="secondary"
              disabled={action.pending}
              onClick={() => void handleSave()}
            >
              <Save size={16} />
              {action.pending ? 'Saving...' : 'Save Draft'}
            </Button>
          </div>
          <div className="stepper-nav-right">
            {Object.keys(errors).length > 0 && (
              <span style={{ color: '#dc2626', fontSize: '13px', fontWeight: 600 }}>
                Please fill in all mandatory fields (*)
              </span>
            )}
            <Button
              type="button"
              variant="success"
              disabled={
                action.pending ||
                (!!data.staffCategoryId && !category.data && !allCategoriesQuery.data)
              }
              onClick={() => void handleSave('financial')}
            >
              <span>Save & Continue to Step 2: Financial</span>
              <ArrowRight size={16} />
            </Button>
          </div>
        </div>
      )}
    </form>
  );
}
