import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import {
  Camera,
  Calendar,
  Check,
  Copy,
  Mail,
  Navigation,
  Phone,
  RefreshCw,
  Upload,
  User,
  ShieldCheck,
  Clock,
  PhoneCall,
  Smartphone,
  MapPin,
  Briefcase,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';
import { request } from '@/services/api/http-client';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import { profileResponse, documentsSchema } from '../../schemas/hiring';
import { writeResult, pageOf } from '../../schemas/contracts';
import { masterSchema } from '../../schemas/organization';
import { employmentLabels, onboardingLabels } from '../../types/contracts';
import { labelDate } from '../../utils/messages';

interface EmployeeProfileHeaderProps {
  employeeId: string;
  onRefresh?: () => void;
}

export function EmployeeProfileHeader({ employeeId, onRefresh }: EmployeeProfileHeaderProps) {
  const access = useHrAccess();
  const action = useHrAction();
  const cache = useQueryClient();
  const [params] = useSearchParams();
  const currentSection = params.get('section') ?? 'basic';

  const [photoModalOpen, setPhotoModalOpen] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [copiedField, setCopiedField] = useState<string | null>(null);
  const [photoBlobUrl, setPhotoBlobUrl] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const canReadProfile = access.can('hr.employees.read');
  const canReadDocs = access.can('hr.employees.read', 'hr.employees.documents.read');
  const canManageDocs = access.can(
    'hr.employees.read',
    'hr.employees.documents.manage',
    'hr.employees.drafts.manage',
  );

  const profileQuery = useHrQuery(
    `employees/${employeeId}`,
    profileResponse,
    canReadProfile ? ['hr.employees.read'] : [],
  );

  const docsQuery = useHrQuery(
    `employees/${employeeId}/documents`,
    documentsSchema,
    canReadDocs ? ['hr.employees.read', 'hr.employees.documents.read'] : [],
  );

  const deptsQuery = useHrQuery(
    'departments?pageSize=50',
    pageOf(masterSchema),
    canReadProfile ? ['hr.organization.read'] : [],
  );

  const desigsQuery = useHrQuery(
    'designations?pageSize=50',
    pageOf(masterSchema),
    canReadProfile ? ['hr.organization.read'] : [],
  );

  const employee = profileQuery.data?.employee;
  const profile = profileQuery.data?.profile;

  // Find latest active ProfilePhoto document (type = 9)
  const photoDoc = docsQuery.data?.find(
    (d) => d.type === 9 && (d.isAvailable || d.scanStatus === 1),
  );

  // Fetch actual image blob for the profile photo
  useEffect(() => {
    let active = true;
    let createdUrl: string | null = null;

    if (photoDoc && canReadDocs) {
      request(`/hr/employees/${employeeId}/documents/${photoDoc.id}/download`, {
        responseType: 'blob',
      })
        .then((blob) => {
          if (!active) return;
          if (blob instanceof Blob) {
            createdUrl = URL.createObjectURL(blob);
            setPhotoBlobUrl(createdUrl);
          }
        })
        .catch(() => {
          // Fallback silently to initials
        });
    } else {
      setPhotoBlobUrl(null);
    }

    return () => {
      active = false;
      if (createdUrl) {
        URL.revokeObjectURL(createdUrl);
      }
    };
  }, [photoDoc?.id, employeeId, canReadDocs]);

  // Handle preview for new file upload
  useEffect(() => {
    if (!selectedFile) {
      setPreviewUrl(null);
      return;
    }
    const objectUrl = URL.createObjectURL(selectedFile);
    setPreviewUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [selectedFile]);

  const fullName = profile?.fullLegalName || employee?.name || 'Employee Profile';
  const email = profile?.officialEmail || profile?.personalEmail || null;
  const phone = profile?.companyPhone || profile?.personalPhone || null;
  const joinedDate = profile?.joinedOn ? labelDate(profile.joinedOn) : null;
  const employeeNo = employee?.employeeNumber ? `EMP-${employee.employeeNumber}` : null;

  const deptName =
    deptsQuery.data?.items?.find((d) => d.id === profile?.departmentId)?.name || null;
  const desigName =
    desigsQuery.data?.items?.find((d) => d.id === profile?.designationId)?.name || null;
  const cnicNumber = profile?.identityDocumentNumber || null;
  const emergencyName = profile?.emergencyContact?.name || null;
  const emergencyPhone = profile?.emergencyContact?.phone || null;
  const emergencyRelation = profile?.emergencyContact?.relationship || 'Emergency';
  const loginPhone = profile?.loginPhoneNumber || null;
  const currentAddressStr = profile?.currentAddress?.line1
    ? `${profile.currentAddress.line1}${profile.currentAddress.city ? `, ${profile.currentAddress.city}` : ''}`
    : null;

  // Overtime (OT) Status:
  const isOvertimeActive = Boolean(
    profile?.staffCategoryId ||
    profile?.mobileAccessRequested ||
    (desigName && /technician|engineer|supervisor|operator|driver|field/i.test(desigName)),
  );

  // Generate initials
  const initials =
    fullName
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((word) => word[0]?.toUpperCase() ?? '')
      .join('') || 'EM';

  const copyToClipboard = (text: string, label: string) => {
    navigator.clipboard.writeText(text).then(() => {
      setCopiedField(label);
      setTimeout(() => setCopiedField(null), 2000);
    });
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    setUploadError(null);
    if (!file) {
      setSelectedFile(null);
      return;
    }
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      setUploadError('Only JPG, PNG or WebP images are allowed.');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      setUploadError('Image size must be less than 10MB.');
      return;
    }
    setSelectedFile(file);
  };

  const handleUploadPhoto = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedFile || !employee) return;
    setUploadError(null);

    const formData = new FormData();
    formData.append('file', selectedFile);
    formData.append('type', '9'); // ProfilePhoto
    formData.append('medical', 'false');
    formData.append('expectedVersion', employee.version);

    const result = await action.run(
      `employees/${employeeId}/documents`,
      formData,
      'POST',
      writeResult,
    );

    if (result) {
      setSelectedFile(null);
      setPhotoModalOpen(false);
      // Invalidate queries so photo updates instantly
      void cache.invalidateQueries({ queryKey: ['hr', access.subject, 'employees', employeeId] });
      void docsQuery.refetch();
      void profileQuery.refetch();
      onRefresh?.();
    }
  };

  const statusIndex = employee?.status ?? 0;
  const statusLabel = employmentLabels[statusIndex] ?? 'Active';
  const onboardingIndex = employee?.onboardingStatus ?? 0;
  const onboardingLabel = onboardingLabels[onboardingIndex] ?? 'Draft';

  return (
    <div
      style={{
        background: '#ffffff',
        border: '1px solid #e2e8f0',
        borderRadius: '14px',
        padding: '24px 28px',
        marginBottom: '24px',
        boxShadow: '0 2px 8px -2px rgba(15, 23, 42, 0.05), 0 1px 3px rgba(0, 0, 0, 0.03)',
        position: 'relative',
        overflow: 'hidden',
      }}
    >
      {/* Decorative top colored border accent */}
      <div
        style={{
          position: 'absolute',
          top: 0,
          left: 0,
          right: 0,
          height: '4px',
          background: 'linear-gradient(90deg, #16745e 0%, #2563eb 50%, #4f46e5 100%)',
        }}
      />

      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: '24px',
        }}
      >
        {/* Left Side: Avatar & Core Information */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '22px', minWidth: '300px' }}>
          {/* Avatar Container with Camera Trigger */}
          <div style={{ position: 'relative', flexShrink: 0 }}>
            <div
              style={{
                width: '92px',
                height: '92px',
                borderRadius: '50%',
                overflow: 'hidden',
                background: photoBlobUrl
                  ? '#f8fafc'
                  : 'linear-gradient(135deg, #1e40af 0%, #3b82f6 50%, #60a5fa 100%)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                boxShadow: '0 4px 12px rgba(30, 64, 175, 0.15)',
                border: '3px solid #ffffff',
                outline: '2px solid #e2e8f0',
              }}
            >
              {photoBlobUrl ? (
                <img
                  src={photoBlobUrl}
                  alt={fullName}
                  style={{
                    width: '100%',
                    height: '100%',
                    objectFit: 'cover',
                  }}
                />
              ) : (
                <span
                  style={{
                    fontSize: '28px',
                    fontWeight: 700,
                    color: '#ffffff',
                    letterSpacing: '0.05em',
                    userSelect: 'none',
                  }}
                >
                  {initials}
                </span>
              )}
            </div>

            {/* Camera Button Badge */}
            {canManageDocs && (
              <button
                type="button"
                onClick={() => setPhotoModalOpen(true)}
                title="Change or upload profile picture"
                style={{
                  position: 'absolute',
                  bottom: '0',
                  right: '0',
                  width: '30px',
                  height: '30px',
                  borderRadius: '50%',
                  background: '#1e40af',
                  color: '#ffffff',
                  border: '2px solid #ffffff',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  cursor: 'pointer',
                  boxShadow: '0 2px 6px rgba(0,0,0,0.2)',
                  transition: 'transform 0.15s ease, background 0.15s ease',
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.transform = 'scale(1.1)';
                  e.currentTarget.style.background = '#2563eb';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.transform = 'scale(1)';
                  e.currentTarget.style.background = '#1e40af';
                }}
              >
                <Camera size={15} />
              </button>
            )}
          </div>

          {/* Identity & Badges */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
            <div style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: '10px' }}>
              <h1
                style={{
                  fontSize: '24px',
                  fontWeight: 750,
                  color: '#0f172a',
                  margin: 0,
                  letterSpacing: '-0.02em',
                }}
              >
                {fullName}
              </h1>

              {employeeNo && (
                <span
                  style={{
                    background: '#f1f5f9',
                    color: '#475569',
                    fontSize: '12px',
                    fontWeight: 650,
                    padding: '3px 9px',
                    borderRadius: '6px',
                    border: '1px solid #e2e8f0',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '4px',
                  }}
                >
                  {employeeNo}
                </span>
              )}

              {/* Status Badge */}
              <span
                style={{
                  background:
                    statusIndex === 2 ? '#ecfdf5' : statusIndex === 3 ? '#fef2f2' : '#f8fafc',
                  color: statusIndex === 2 ? '#047857' : statusIndex === 3 ? '#b91c1c' : '#475569',
                  border: `1px solid ${statusIndex === 2 ? '#a7f3d0' : statusIndex === 3 ? '#fecaca' : '#e2e8f0'}`,
                  fontSize: '12px',
                  fontWeight: 650,
                  padding: '3px 9px',
                  borderRadius: '6px',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '5px',
                }}
              >
                <span
                  style={{
                    width: '6px',
                    height: '6px',
                    borderRadius: '50%',
                    background:
                      statusIndex === 2 ? '#10b981' : statusIndex === 3 ? '#ef4444' : '#94a3b8',
                  }}
                />
                {statusLabel}
              </span>

              {/* Onboarding Status Badge */}
              <span
                style={{
                  background: onboardingIndex === 2 ? '#eff6ff' : '#fffbeb',
                  color: onboardingIndex === 2 ? '#1d4ed8' : '#b45309',
                  border: `1px solid ${onboardingIndex === 2 ? '#bfdbfe' : '#fde68a'}`,
                  fontSize: '11px',
                  fontWeight: 600,
                  padding: '2px 8px',
                  borderRadius: '6px',
                }}
              >
                {onboardingLabel}
              </span>

              {/* Overtime (OT) Status Badge */}
              <span
                style={{
                  background: isOvertimeActive ? '#f0fdf4' : '#f8fafc',
                  color: isOvertimeActive ? '#15803d' : '#64748b',
                  border: `1px solid ${isOvertimeActive ? '#bbf7d0' : '#cbd5e1'}`,
                  fontSize: '11px',
                  fontWeight: 700,
                  padding: '2px 8px',
                  borderRadius: '6px',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '4px',
                }}
                title={
                  isOvertimeActive
                    ? 'Overtime (OT) is Active / Eligible'
                    : 'Overtime (OT) is Ineligible / Off'
                }
              >
                <Clock size={11} />
                <span>OT: {isOvertimeActive ? 'ON' : 'OFF'}</span>
              </span>
            </div>

            {/* Quick Meta Chips */}
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                flexWrap: 'wrap',
                gap: '14px',
                marginTop: '4px',
                fontSize: '12.5px',
                color: '#64748b',
              }}
            >
              {(desigName || deptName) && (
                <div style={{ display: 'flex', alignItems: 'center', gap: '5px' }}>
                  <Briefcase size={13} color="#475569" />
                  <span>
                    <strong style={{ color: '#334155' }}>{desigName ?? 'Staff'}</strong>
                    {deptName ? ` • ${deptName}` : ''}
                  </span>
                </div>
              )}

              {cnicNumber && (
                <div style={{ display: 'flex', alignItems: 'center', gap: '5px' }}>
                  <ShieldCheck size={13} color="#0284c7" />
                  <span>
                    CNIC: <strong style={{ color: '#0f172a' }}>{cnicNumber}</strong>
                  </span>
                </div>
              )}

              {email && (
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '5px',
                    cursor: 'pointer',
                  }}
                  title="Click to copy email"
                  onClick={() => copyToClipboard(email, 'email')}
                >
                  <Mail size={13} color="#94a3b8" />
                  <span>{email}</span>
                  {copiedField === 'email' ? (
                    <Check size={12} color="#10b981" />
                  ) : (
                    <Copy size={11} color="#cbd5e1" />
                  )}
                </div>
              )}

              {phone && (
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '5px',
                    cursor: 'pointer',
                  }}
                  title="Click to copy phone"
                  onClick={() => copyToClipboard(phone, 'phone')}
                >
                  <Phone size={13} color="#94a3b8" />
                  <span>{phone}</span>
                  {copiedField === 'phone' ? (
                    <Check size={12} color="#10b981" />
                  ) : (
                    <Copy size={11} color="#cbd5e1" />
                  )}
                </div>
              )}

              {emergencyPhone && (
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '5px',
                    background: '#fff7ed',
                    border: '1px solid #ffedd5',
                    padding: '2px 8px',
                    borderRadius: '6px',
                    color: '#c2410c',
                  }}
                >
                  <PhoneCall size={12} color="#ea580c" />
                  <span>
                    Emergency: <strong>{emergencyName ? `${emergencyName} ` : ''}</strong>(
                    {emergencyPhone}
                    {emergencyRelation ? ` • ${emergencyRelation}` : ''})
                  </span>
                </div>
              )}

              {loginPhone && loginPhone !== phone && (
                <div style={{ display: 'flex', alignItems: 'center', gap: '5px' }}>
                  <Smartphone size={13} color="#059669" />
                  <span>
                    Login Phone: <strong>+92 {loginPhone}</strong>
                  </span>
                </div>
              )}

              {currentAddressStr && (
                <div style={{ display: 'flex', alignItems: 'center', gap: '5px' }}>
                  <MapPin size={13} color="#94a3b8" />
                  <span>{currentAddressStr}</span>
                </div>
              )}

              {joinedDate && (
                <div style={{ display: 'flex', alignItems: 'center', gap: '5px' }}>
                  <Calendar size={13} color="#94a3b8" />
                  <span>Joined {joinedDate}</span>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Right Side: Quick Action Bar */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
          {/* Direct Map Tracking Shortcut */}
          <Link
            to="?section=tracking"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              padding: '8px 14px',
              borderRadius: '8px',
              fontSize: '13px',
              fontWeight: 600,
              textDecoration: 'none',
              background: currentSection === 'tracking' ? '#047857' : '#ecfdf5',
              color: currentSection === 'tracking' ? '#ffffff' : '#065f46',
              border: `1px solid ${currentSection === 'tracking' ? '#047857' : '#a7f3d0'}`,
              transition: 'all 0.15s ease',
            }}
          >
            <Navigation size={15} />
            Live Map Tracking
          </Link>

          {/* Upload Photo Button */}
          {canManageDocs && (
            <Button
              variant="secondary"
              onClick={() => setPhotoModalOpen(true)}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
                fontSize: '13px',
              }}
            >
              <Camera size={15} />
              {photoBlobUrl ? 'Change Photo' : 'Upload Photo'}
            </Button>
          )}

          {/* Refresh Button */}
          <Button
            variant="ghost"
            title="Refresh profile details"
            aria-label="Refresh profile details"
            disabled={profileQuery.isFetching || docsQuery.isFetching}
            onClick={() => {
              void profileQuery.refetch();
              void docsQuery.refetch();
              onRefresh?.();
            }}
          >
            <RefreshCw size={15} className={profileQuery.isFetching ? 'animate-spin' : ''} />
          </Button>
        </div>
      </div>

      {/* Upload Profile Picture Dialog */}
      <Dialog
        open={photoModalOpen}
        onOpenChange={(open) => {
          setPhotoModalOpen(open);
          if (!open) {
            setSelectedFile(null);
            setUploadError(null);
          }
        }}
        title="Update Profile Photo"
        description={`Upload an official clear portrait of ${fullName}.`}
      >
        <form
          onSubmit={handleUploadPhoto}
          style={{ display: 'flex', flexDirection: 'column', gap: '18px' }}
        >
          <p style={{ margin: 0, fontSize: '13px', color: '#64748b' }}>
            Upload an official clear portrait of <strong>{fullName}</strong>. Supported formats:
            JPG, PNG, or WebP up to 10MB.
          </p>

          {/* Live Preview Circle */}
          <div style={{ display: 'flex', justifyContent: 'center', padding: '12px 0' }}>
            <div
              style={{
                width: '120px',
                height: '120px',
                borderRadius: '50%',
                overflow: 'hidden',
                background: '#f1f5f9',
                border: '3px solid #e2e8f0',
                boxShadow: '0 4px 12px rgba(0,0,0,0.08)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                position: 'relative',
              }}
            >
              {previewUrl ? (
                <img
                  src={previewUrl}
                  alt="Preview"
                  style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                />
              ) : photoBlobUrl ? (
                <img
                  src={photoBlobUrl}
                  alt={fullName}
                  style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                />
              ) : (
                <User size={54} color="#94a3b8" />
              )}
            </div>
          </div>

          {/* File Picker */}
          <div>
            <label
              htmlFor="profile-photo-input"
              style={{
                display: 'block',
                fontSize: '13px',
                fontWeight: 600,
                color: '#334155',
                marginBottom: '6px',
              }}
            >
              Choose Photo File
            </label>
            <input
              ref={fileInputRef}
              id="profile-photo-input"
              type="file"
              accept="image/png,image/jpeg,image/webp"
              onChange={handleFileChange}
              style={{
                display: 'block',
                width: '100%',
                padding: '8px 12px',
                border: '1px solid #cbd5e1',
                borderRadius: '6px',
                fontSize: '13px',
                background: '#f8fafc',
              }}
            />
          </div>

          {selectedFile && (
            <div
              style={{
                fontSize: '12px',
                color: '#475569',
                background: '#f1f5f9',
                padding: '8px 12px',
                borderRadius: '6px',
                display: 'flex',
                justifyContent: 'space-between',
              }}
            >
              <span>{selectedFile.name}</span>
              <span>{(selectedFile.size / 1024).toFixed(1)} KB</span>
            </div>
          )}

          {uploadError && (
            <div
              style={{
                color: '#b91c1c',
                background: '#fef2f2',
                border: '1px solid #fecaca',
                padding: '8px 12px',
                borderRadius: '6px',
                fontSize: '13px',
              }}
            >
              {uploadError}
            </div>
          )}

          {/* Dialog Action Buttons */}
          <div
            style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '8px' }}
          >
            <Button
              type="button"
              variant="ghost"
              onClick={() => {
                setPhotoModalOpen(false);
                setSelectedFile(null);
                setUploadError(null);
              }}
            >
              Cancel
            </Button>
            <Button type="submit" variant="primary" disabled={!selectedFile || action.pending}>
              <Upload size={14} style={{ marginRight: '6px' }} />
              {action.pending ? 'Uploading...' : 'Save & Set Photo'}
            </Button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}
