import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Camera, MoreHorizontal, Navigation, Pencil, Upload, User } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';
import { request } from '@/services/api/http-client';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import { documentsSchema } from '../../schemas/hiring';
import { writeResult, type EmployeeSummary } from '../../schemas/contracts';
import type { EmployeeProfileValues } from '../../hooks/use-employee-profile';
import { ActionMessage } from '../Feedback';
import { employmentLabels, onboardingLabels } from '../../types/contracts';

interface EmployeeProfileHeaderProps {
  employeeId: string;
  employee?: EmployeeSummary;
  profile: EmployeeProfileValues;
  approved: boolean;
  designationName?: string;
  directoryName?: string | null;
  refreshing: boolean;
  onRefresh: () => void;
}

export function EmployeeProfileHeader({
  employeeId,
  employee,
  profile,
  approved,
  designationName,
  directoryName,
  refreshing,
  onRefresh,
}: EmployeeProfileHeaderProps) {
  const access = useHrAccess();
  const action = useHrAction();

  const [photoModalOpen, setPhotoModalOpen] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [photo, setPhoto] = useState<{ key: string; url: string } | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const canReadDocs = access.can('hr.employees.read', 'hr.employees.documents.read');
  const canManageDocs =
    !approved &&
    employee?.status === 0 &&
    [0, 3].includes(employee.onboardingStatus) &&
    access.can('hr.employees.read', 'hr.employees.documents.manage', 'hr.employees.drafts.manage');

  const docsQuery = useHrQuery(`employees/${employeeId}/documents`, documentsSchema, [
    'hr.employees.read',
    'hr.employees.documents.read',
  ]);

  // Display an available, scan-clean profile photo only with document-read permission.
  const photoDoc =
    canReadDocs && docsQuery.isSuccess
      ? docsQuery.data.find((d) => d.type === 9 && d.isAvailable && d.scanStatus === 1)
      : undefined;
  const photoKey = photoDoc
    ? `${access.subject}:${employeeId}:${photoDoc.id}:${photoDoc.version}`
    : null;
  const photoBlobUrl = photoKey && photo?.key === photoKey ? photo.url : null;

  // Fetch actual image blob for the profile photo
  useEffect(() => {
    let active = true;
    let createdUrl: string | null = null;

    if (photoDoc && canReadDocs && photoKey) {
      request(`/hr/employees/${employeeId}/documents/${photoDoc.id}/download`, {
        responseType: 'blob',
      })
        .then((blob) => {
          if (!active) return;
          createdUrl = URL.createObjectURL(blob as Blob);
          setPhoto({ key: photoKey, url: createdUrl });
        })
        .catch(() => {
          // silently fail to initials
        });
    } else {
      setPhoto(null);
    }

    return () => {
      active = false;
      if (createdUrl) URL.revokeObjectURL(createdUrl);
    };
  }, [photoDoc?.id, employeeId, canReadDocs, photoKey]);

  // Handle local file preview
  useEffect(() => {
    if (!selectedFile) {
      setPreviewUrl(null);
      return;
    }
    const url = URL.createObjectURL(selectedFile);
    setPreviewUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [selectedFile]);

  const fullName =
    profile.fullLegalName || employee?.name || directoryName || 'Employee name unavailable';
  const employeeNo = employee?.employeeNumber || null;

  const initials = (profile.fullLegalName || employee?.name || directoryName || '')
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() ?? '')
    .join('');

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
    if (!selectedFile || !employee || !canManageDocs) return;
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
      if (canReadDocs) void docsQuery.refetch();
      onRefresh();
    }
  };

  const statusIndex = employee?.status;
  const statusLabel = statusIndex === undefined ? undefined : employmentLabels[statusIndex];
  const onboardingIndex = employee?.onboardingStatus;
  const onboardingLabel = approved
    ? 'Approved current record'
    : onboardingIndex === undefined
      ? undefined
      : onboardingLabels[onboardingIndex];

  return (
    <div className="emp-header-card">
      {/* Left Avatar + Identity */}
      <div className="emp-header-left">
        <div className="emp-avatar-wrapper">
          <div className="emp-avatar-circle">
            {photoBlobUrl ? (
              <img src={photoBlobUrl} alt={fullName} className="emp-avatar-img" />
            ) : (
              <span className="emp-avatar-initials">
                {initials || <User size={24} aria-label="Employee" />}
              </span>
            )}
          </div>

          {canManageDocs && (
            <button
              type="button"
              onClick={() => setPhotoModalOpen(true)}
              className="emp-avatar-cam-btn"
              title="Change profile picture"
            >
              <Camera size={10} />
            </button>
          )}
        </div>

        <div className="emp-header-info">
          <h1 className="emp-header-name">{fullName}</h1>
          <div className="emp-badges-row">
            {employeeNo && <span className="emp-badge-code">{employeeNo}</span>}
            {statusLabel && (
              <span className={`emp-badge-status ${statusIndex === 2 ? 'active' : 'inactive'}`}>
                <span className="emp-badge-dot" />
                {statusLabel}
              </span>
            )}
            {onboardingLabel && <span className="emp-badge-onboarding">{onboardingLabel}</span>}
            {designationName && <span className="emp-badge-desig">{designationName}</span>}
          </div>
          <p className="muted">
            {approved ? 'Current authorized employee details.' : 'Hiring draft details.'}
            {approved &&
              !profile.fullLegalName &&
              directoryName &&
              ' Name shown from the employee directory; protected identity details remain separate.'}
          </p>
        </div>
      </div>

      {/* Right Action Buttons */}
      <div className="emp-header-actions">
        {approved && access.can('hr.employees.read', 'hr.employees.basic.manage') && (
          <Link to="?section=basic&edit=1" className="emp-btn-primary">
            <Pencil size={12} />
            Edit details
          </Link>
        )}
        {approved && (
          <Link to="?section=tracking" className="emp-btn-secondary">
            <Navigation size={12} />
            Live map
          </Link>
        )}
        <button
          type="button"
          className="emp-btn-icon"
          title="Refresh profile details"
          disabled={refreshing || docsQuery.isFetching}
          onClick={() => {
            if (canReadDocs) void docsQuery.refetch();
            onRefresh?.();
          }}
        >
          <MoreHorizontal size={15} />
        </button>
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

          <ActionMessage action={action} />

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
            <Button
              type="submit"
              variant="primary"
              disabled={!selectedFile || action.pending || !canManageDocs}
            >
              <Upload size={14} style={{ marginRight: '6px' }} />
              {action.pending ? 'Uploading...' : 'Save & Set Photo'}
            </Button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}
