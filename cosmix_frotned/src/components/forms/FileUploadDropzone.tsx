import { useId, useRef, useState, type DragEvent } from 'react';
import { CloudUpload, FileText, CheckCircle2, X, AlertCircle } from 'lucide-react';

export function FileUploadDropzone({
  label = 'Upload your File',
  required = false,
  error,
  accept = 'application/pdf,image/png,image/jpeg',
  maxSizeBytes = 10 * 1024 * 1024,
  value,
  onChange,
  disabled = false,
  dataField,
  hint = 'Supported formats: PDF, PNG, JPG (Max 10 MB)',
}: {
  label?: string;
  required?: boolean;
  error?: string;
  accept?: string;
  maxSizeBytes?: number;
  value: File | null;
  onChange: (file: File | null) => void;
  disabled?: boolean;
  dataField?: string;
  hint?: string;
}) {
  const id = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  const handleFile = (file: File | null) => {
    setValidationError(null);
    if (!file) {
      onChange(null);
      return;
    }

    if (file.size > maxSizeBytes) {
      const maxMb = Math.round(maxSizeBytes / (1024 * 1024));
      setValidationError(`File is too large. Maximum allowed size is ${maxMb} MB.`);
      return;
    }

    // Check mime/extension
    const ext = file.name.split('.').pop()?.toLowerCase();
    const isAllowedExt = ['pdf', 'png', 'jpg', 'jpeg'].includes(ext ?? '');
    if (!isAllowedExt && !accept.includes(file.type)) {
      setValidationError('Invalid file format. Please upload a PDF, PNG, or JPG file.');
      return;
    }

    onChange(file);
  };

  const handleDragOver = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    if (!disabled) setIsDragging(true);
  };

  const handleDragLeave = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  };

  const handleDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
    if (disabled) return;

    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      handleFile(e.dataTransfer.files[0] ?? null);
    }
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  };

  const activeError = error || validationError;

  return (
    <div className="field upload-dropzone-field" data-field={dataField}>
      {label && (
        <label htmlFor={id} className="upload-dropzone-label">
          {label}
          {required && (
            <span className="hr-required-mark" aria-hidden="true">
              {' '}
              *
            </span>
          )}
        </label>
      )}

      <input
        ref={inputRef}
        id={id}
        type="file"
        accept={accept}
        disabled={disabled}
        required={required && !value}
        style={{ display: 'none' }}
        onChange={(e) => {
          handleFile(e.target.files?.[0] ?? null);
          // Reset input value so re-selecting same file triggers change
          if (inputRef.current) inputRef.current.value = '';
        }}
      />

      {!value ? (
        <div
          role="button"
          tabIndex={disabled ? -1 : 0}
          aria-label={label}
          className={`upload-dropzone-box ${isDragging ? 'is-dragging' : ''} ${activeError ? 'is-invalid' : ''} ${disabled ? 'is-disabled' : ''}`}
          onDragOver={handleDragOver}
          onDragLeave={handleDragLeave}
          onDrop={handleDrop}
          onClick={() => !disabled && inputRef.current?.click()}
          onKeyDown={(e) => {
            if ((e.key === 'Enter' || e.key === ' ') && !disabled) {
              e.preventDefault();
              inputRef.current?.click();
            }
          }}
        >
          <div className="upload-dropzone-icon-circle">
            <CloudUpload size={24} />
          </div>
          <p className="upload-dropzone-main-text">
            Upload your files <span className="upload-dropzone-link">here</span>
          </p>
          <p className="upload-dropzone-sub-text">Or you can drag and drop your file</p>
          {hint && <p className="upload-dropzone-hint">{hint}</p>}
        </div>
      ) : (
        <div className={`upload-selected-card ${activeError ? 'is-invalid' : ''}`}>
          <div className="upload-selected-icon">
            <FileText size={22} />
          </div>
          <div className="upload-selected-info">
            <span className="upload-selected-name" title={value.name}>
              {value.name}
            </span>
            <span className="upload-selected-size">
              {formatFileSize(value.size)} &middot; <CheckCircle2 size={13} color="#16a34a" /> Ready to upload
            </span>
          </div>
          {!disabled && (
            <button
              type="button"
              className="upload-selected-remove-btn"
              title="Remove file"
              aria-label="Remove selected file"
              onClick={() => handleFile(null)}
            >
              <X size={16} />
            </button>
          )}
        </div>
      )}

      {activeError && (
        <p className="field-error" role="alert" style={{ display: 'flex', alignItems: 'center', gap: '4px', marginTop: '6px' }}>
          <AlertCircle size={14} />
          <span>{activeError}</span>
        </p>
      )}
    </div>
  );
}
