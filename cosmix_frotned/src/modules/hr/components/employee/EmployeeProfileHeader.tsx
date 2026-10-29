import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { Camera, MoreHorizontal, Navigation, Pencil, Upload, User } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';
import { request } from '@/services/api/http-client';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import { profileResponse, documentsSchema } from '../../schemas/hiring';
import { writeResult, pageOf } from '../../schemas/contracts';
import { masterSchema } from '../../schemas/organization';
import { employmentLabels, onboardingLabels } from '../../types/contracts';

interface EmployeeProfileHeaderProps {
  employeeId: string;
  onRefresh?: () => void;
}

export function EmployeeProfileHeader({ employeeId, onRefresh }: EmployeeProfileHeaderProps) {
  const access = useHrAccess();
  const action = useHrAction();
  const cache = useQueryClient();

  const [photoModalOpen, setPhotoModalOpen] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [photoBlobUrl, setPhotoBlobUrl] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const canReadDocs = access.can('hr.employees.read', 'hr.employees.documents.read');
  const canManageDocs = access.can(
    'hr.employees.read',
    'hr.employees.documents.manage',
    'hr.employees.drafts.manage',
  );

  const profileQuery = useHrQuery(`employees/${employeeId}`, profileResponse, [
    'hr.employees.read',
  ]);

  const docsQuery = useHrQuery(`employees/${employeeId}/documents`, documentsSchema, [
    'hr.employees.read',
    'hr.employees.documents.read',
  ]);

  const desigsQuery = useHrQuery('designations?pageSize=50', pageOf(masterSchema), [
    'hr.organization.read',
  ]);

  const employee = profileQuery.data?.employee;
  const profile = profileQuery.data?.profile;

  // Find latest active ProfilePhoto document (type = 9) that is verified and available
  const photoDoc = docsQuery.data?.find((d) => d.type === 9 && d.isAvailable && d.scanStatus === 1);

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
          createdUrl = URL.createObjectURL(blob as Blob);
          setPhotoBlobUrl(createdUrl);
        })
        .catch(() => {
          // silently fail to initials
        });
    } else {
      setPhotoBlobUrl(null);
    }

    return () => {
      active = false;
      if (createdUrl) URL.revokeObjectURL(createdUrl);
    };
  }, [photoDoc?.id, employeeId, canReadDocs]);

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

  const fullName = profile?.fullLegalName || employee?.name || 'Employee';
  const employeeNo = employee?.employeeNumber || null;

  const desigName =
    desigsQuery.data?.items?.find((d) => d.id === profile?.designationId)?.name || null;

  // Generate initials
  const initials =
    fullName
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((word: string) => word[0]?.toUpperCase() ?? '')
      .join('') || 'EM';

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
    <div className="emp-header-card">
      {/* Left Avatar + Identity */}
      <div className="emp-header-left">
        <div className="emp-avatar-wrapper">
          <div className="emp-avatar-circle">
            {photoBlobUrl ? (
              <img src={photoBlobUrl} alt={fullName} className="emp-avatar-img" />
            ) : (
              <span className="emp-avatar-initials">{initials}</span>
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
            <span className={`emp-badge-status ${statusIndex === 2 ? 'active' : 'inactive'}`}>
              <span className="emp-badge-dot" />
              {statusLabel}
            </span>
            <span className="emp-badge-onboarding">{onboardingLabel}</span>
            {desigName && <span className="emp-badge-desig">{desigName}</span>}
          </div>
          <p className="muted">
            Name and designation are from the hiring profile snapshot and may differ from current
            approved records. View Identity and Employment for current values.
          </p>
        </div>
      </div>

      {/* Right Action Buttons */}
      <div className="emp-header-actions">
        <Link to="?section=basic&edit=1" className="emp-btn-primary">
          <Pencil size={12} />
          Edit details
        </Link>
        <Link to="?section=tracking" className="emp-btn-secondary">
          <Navigation size={12} />
          Live map
        </Link>
        <button
          type="button"
          className="emp-btn-icon"
          title="Refresh profile details"
          disabled={profileQuery.isFetching || docsQuery.isFetching}
          onClick={() => {
            void profileQuery.refetch();
            void docsQuery.refetch();
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
