import { useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Download, FileCheck, RefreshCw, Upload } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { FileUploadDropzone } from '@/components/forms/FileUploadDropzone';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import { documentsSchema } from '../../schemas/hiring';
import { writeResult } from '../../schemas/contracts';
import { documentTypes } from '../../types/hiring';
import { downloadFile } from '../../api/hr-api';
import { errorMessage, labelDate } from '../../utils/messages';
import { ActionMessage, Denied, QueryState } from '../Feedback';
import { EnumField, Field, LongField } from '../hiring/FormFields';
import { DocumentCompleteness, DocumentHistory } from './DocumentEvidence';

export function EmployeeDocuments({
  employeeId,
  version,
  medical = false,
  editable,
  approved = false,
  onSaved,
}: {
  employeeId: string;
  version: string;
  medical?: boolean;
  editable: boolean;
  approved?: boolean;
  onSaved: () => void;
}) {
  const access = useHrAccess();
  const cache = useQueryClient();
  const read = [
    'hr.employees.read',
    'hr.employees.documents.read',
    ...(medical ? ['hr.employees.medical.read'] : []),
  ];
  const manage =
    editable &&
    access.can(
      'hr.employees.drafts.manage',
      'hr.employees.documents.manage',
      ...(medical ? ['hr.employees.medical.manage'] : []),
      ...(approved ? ['hr.employees.changes.request'] : []),
    );
  const verify =
    editable &&
    access.can(
      'hr.employees.read',
      'hr.employees.documents.verify',
      ...(medical ? ['hr.employees.medical.read'] : []),
      ...(approved ? ['hr.employees.changes.request'] : []),
    );
  const withdrawals = (['expire', 'revoke', 'archive'] as const).filter((operation) =>
    access.can(
      'hr.employees.read',
      'hr.employees.documents.manage',
      `hr.employees.documents.${operation}`,
      ...(medical ? ['hr.employees.medical.manage'] : []),
    ),
  );
  const suffix = medical ? '?medical=true' : '';
  const path = `employees/${employeeId}/documents`;
  const query = useHrQuery(path + suffix, documentsSchema, read);
  const action = useHrAction();
  const lifetime = useHrLifetime();
  const [file, setFile] = useState<File | null>(null);
  const [type, setType] = useState<number | null>(null);
  const [issuedOn, setIssuedOn] = useState<string | null>(null);
  const [expiresOn, setExpiresOn] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [downloadPending, setDownloadPending] = useState(false);
  const [reason, setReason] = useState<string | null>(null);
  const input = useRef<HTMLInputElement>(null);
  useDirtyForm(!!file || !!reason?.trim());
  if (!access.can(...read)) return <Denied />;
  async function saved(result: unknown) {
    const signal = lifetime();
    if (result) {
      setReason(null);
      await cache.refetchQueries({
        type: 'active',
        predicate: (record) => {
          const resource = record.queryKey[2];
          return (
            record.queryKey[0] === 'hr' &&
            record.queryKey[1] === access.subject &&
            typeof resource === 'string' &&
            (resource === path + suffix ||
              resource.startsWith(path + '/completeness?') ||
              (resource.startsWith(path + '/') && resource.includes('/history?')))
          );
        },
      });
      if (!signal.aborted) onSaved();
    }
  }
  async function download(documentId: string, name: string, review: boolean) {
    const signal = lifetime();
    if (downloadPending || signal.aborted) return;
    setDownloadPending(true);
    setError(null);
    try {
      await downloadFile(
        `employees/${employeeId}/documents/${documentId}/${review ? 'review' : 'download'}${suffix}`,
        name,
        signal,
      );
    } catch (e) {
      if (!signal.aborted) setError(errorMessage(e));
    } finally {
      if (!signal.aborted) setDownloadPending(false);
    }
  }
  return (
    <div className="emp-documents-container" style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      {/* Top Header */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '12px',
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: '#0f172a' }}>
            {medical ? 'Private Medical Documents' : 'Employee Documents & Evidence'}
          </h2>
          <p className="muted" style={{ margin: '4px 0 0 0', fontSize: '12.5px', color: '#64748b' }}>
            Upload, verify, and maintain official employee verification files and records.
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          title="Reload documents"
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
          <RefreshCw size={13} className={query.isFetching ? 'animate-spin' : ''} />
          Reload
        </Button>
      </div>

      {error && (
        <p role="alert" className="hr-message hr-message--error" style={{ margin: 0 }}>
          {error}
        </p>
      )}
      <ActionMessage action={{ error: action.error, success: null }} />

      {/* Card 1: Upload Document */}
      {manage && (
        <div
          className="emp-card"
          style={{
            padding: '20px 24px',
            background: '#ffffff',
            border: '1px solid #e2e8f0',
            borderRadius: '10px',
          }}
        >
          <div className="emp-card-header simple" style={{ marginBottom: '16px' }}>
            <div
              className="emp-card-title"
              style={{ fontSize: '15px', fontWeight: 700, color: '#0f172a' }}
            >
              <Upload size={17} className="emp-card-title-icon" style={{ color: '#2563eb' }} />
              <span>Upload Document Evidence</span>
            </div>
          </div>

          <form
            onSubmit={(e) => {
              e.preventDefault();
              setError(null);
              void (async () => {
                if (!file || !type) return;
                if (
                  !file.size ||
                  file.size > 10 * 1024 * 1024 ||
                  !['application/pdf', 'image/png', 'image/jpeg'].includes(file.type) ||
                  !/\.(pdf|png|jpe?g)$/i.test(file.name) ||
                  (type === 9 && !file.type.startsWith('image/'))
                ) {
                  setError(
                    'Choose a PDF, PNG or JPEG up to 10 MiB. Profile photos must be PNG or JPEG.',
                  );
                  return;
                }
                const body = new FormData();
                body.append('file', file);
                body.append('type', String(type));
                body.append('medical', String(medical));
                body.append('expectedVersion', version);
                if (issuedOn) body.append('issuedOn', issuedOn);
                if (expiresOn) body.append('expiresOn', expiresOn);
                const result = await action.run(path, body, 'POST', writeResult);
                if (result) {
                  setFile(null);
                  if (input.current) input.current.value = '';
                  await saved(result);
                }
              })();
            }}
          >
            <fieldset disabled={action.pending} style={{ border: 0, padding: 0, margin: 0 }}>
              <div className="hr-form-grid modern-form-grid-3" style={{ gap: '14px' }}>
                <EnumField
                  label="Document type"
                  required
                  options={documentTypes}
                  value={type}
                  onChange={setType}
                  dataField="documentType"
                />
                <Field
                  label="Document issued on"
                  type="date"
                  value={issuedOn}
                  onChange={setIssuedOn}
                  dataField="documentIssuedOn"
                />
                <Field
                  label="Document expires on"
                  type="date"
                  value={expiresOn}
                  onChange={setExpiresOn}
                  dataField="documentExpiresOn"
                />
                <div className="col-span-full" style={{ gridColumn: '1 / -1', marginTop: '4px' }}>
                  <FileUploadDropzone
                    label="Upload Document File"
                    required
                    value={file}
                    onChange={setFile}
                    error={error ?? undefined}
                    dataField="documentFile"
                    hint="Supports PDF, PNG, JPG (Max 10 MB). For photos, use PNG or JPG."
                  />
                </div>
              </div>

              <div style={{ marginTop: '16px', display: 'flex', alignItems: 'center', gap: '12px' }}>
                <Button
                  type="submit"
                  variant="primary"
                  disabled={!file || !type || action.pending}
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '6px',
                    height: '36px',
                    padding: '0 18px',
                    fontSize: '13px',
                    fontWeight: 600,
                  }}
                >
                  <Upload size={14} />
                  {action.pending ? 'Uploading document...' : 'Upload Document'}
                </Button>
                {file && (
                  <span style={{ fontSize: '12.5px', color: '#166534', fontWeight: 500 }}>
                    Selected: {file.name}
                  </span>
                )}
              </div>
            </fieldset>
          </form>
        </div>
      )}

      {/* Card 2: Document Repository */}
      <div
        className="emp-card"
        style={{
          padding: '20px 24px',
          background: '#ffffff',
          border: '1px solid #e2e8f0',
          borderRadius: '10px',
        }}
      >
        <div
          className="emp-card-header simple"
          style={{
            marginBottom: '16px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div
            className="emp-card-title"
            style={{ fontSize: '15px', fontWeight: 700, color: '#0f172a' }}
          >
            <FileCheck size={17} className="emp-card-title-icon" style={{ color: '#0f172a' }} />
            <span>Document Repository</span>
            {query.data && (
              <span
                style={{
                  fontSize: '12px',
                  fontWeight: 600,
                  background: '#f1f5f9',
                  color: '#475569',
                  padding: '2px 8px',
                  borderRadius: '12px',
                  marginLeft: '8px',
                }}
              >
                {query.data.length} {query.data.length === 1 ? 'file' : 'files'}
              </span>
            )}
          </div>
        </div>

        {(verify || manage || withdrawals.length > 0) && (
          <div
            style={{
              marginBottom: '16px',
              padding: '12px 16px',
              background: '#f8fafc',
              borderRadius: '8px',
              border: '1px solid #e2e8f0',
            }}
          >
            <label
              style={{
                display: 'block',
                fontSize: '12px',
                fontWeight: 600,
                color: '#334155',
                marginBottom: '4px',
              }}
            >
              Action Reason (Required for rescan, rejection, or withdrawal)
            </label>
            <input
              type="text"
              placeholder="e.g., Unclear scan, expired passport, updated policy"
              value={reason ?? ''}
              onChange={(e) => setReason(e.target.value || null)}
              style={{
                width: '100%',
                height: '36px',
                padding: '6px 12px',
                borderRadius: '6px',
                border: '1px solid #cbd5e1',
                fontSize: '13px',
                background: '#ffffff',
              }}
            />
          </div>
        )}

        <QueryState query={query}>
          {!query.data?.length ? (
            <div style={{ textAlign: 'center', padding: '36px 16px', color: '#64748b' }}>
              <Download size={32} style={{ margin: '0 auto 10px auto', opacity: 0.35, display: 'block' }} />
              <p style={{ margin: 0, fontSize: '14px', fontWeight: 600, color: '#334155' }}>
                No documents uploaded yet
              </p>
              <p style={{ margin: '4px 0 0 0', fontSize: '12.5px', color: '#94a3b8' }}>
                Uploaded employee evidence files and certificates will appear here.
              </p>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {query.data.map((d) => (
                <article
                  key={d.id}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    flexWrap: 'wrap',
                    gap: '14px',
                    padding: '16px 18px',
                    borderRadius: '8px',
                    border: '1px solid #e2e8f0',
                    background: '#f8fafc',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'flex-start', gap: '12px', maxWidth: '65%' }}>
                    <div
                      style={{
                        width: '38px',
                        height: '38px',
                        borderRadius: '8px',
                        background: '#e0f2fe',
                        color: '#0284c7',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        flexShrink: 0,
                      }}
                    >
                      <FileCheck size={18} />
                    </div>
                    <div>
                      <div style={{ fontWeight: 650, fontSize: '14px', color: '#0f172a' }}>
                        {d.fileName}
                      </div>
                      <div
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: '6px',
                          flexWrap: 'wrap',
                          marginTop: '4px',
                          fontSize: '12px',
                          color: '#64748b',
                        }}
                      >
                        <span style={{ fontWeight: 600, color: '#1e293b' }}>
                          {documentTypes[d.type - 1] ?? 'Document'}
                        </span>
                        <span>&middot;</span>
                        <span>{(d.sizeBytes / 1024).toFixed(1)} KiB</span>
                        <span>&middot;</span>
                        <span
                          style={{
                            padding: '1px 6px',
                            borderRadius: '4px',
                            fontSize: '11px',
                            fontWeight: 600,
                            background: d.scanStatus === 1 ? '#dcfce7' : '#fee2e2',
                            color: d.scanStatus === 1 ? '#166534' : '#991b1b',
                          }}
                        >
                          Scan: {['Pending', 'Clean', 'Rejected', 'Failed'][d.scanStatus]}
                        </span>
                        <span>&middot;</span>
                        <span
                          style={{
                            padding: '1px 6px',
                            borderRadius: '4px',
                            fontSize: '11px',
                            fontWeight: 600,
                            background: d.verificationStatus === 1 ? '#dcfce7' : '#fef3c7',
                            color: d.verificationStatus === 1 ? '#166534' : '#854d0e',
                          }}
                        >
                          {['Pending Verify', 'Verified', 'Rejected'][d.verificationStatus]}
                        </span>
                      </div>
                      <div style={{ fontSize: '11.5px', color: '#94a3b8', marginTop: '4px' }}>
                        Issued: {labelDate(d.issuedOn)} &middot; Expires: {labelDate(d.expiresOn)}
                      </div>
                      <div style={{ marginTop: '8px' }}>
                        <DocumentHistory
                          key={d.id}
                          employeeId={employeeId}
                          documentId={d.id}
                          medical={medical}
                        />
                      </div>
                    </div>
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={!d.isAvailable || downloadPending}
                      onClick={() => void download(d.id, d.fileName, false)}
                      style={{ height: '32px', fontSize: '12px', gap: '5px' }}
                    >
                      <Download size={13} />
                      Download
                    </Button>
                    {manage && (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={action.pending || !!file || !reason?.trim() || d.lifecycleStatus !== 0}
                        onClick={() =>
                          void action
                            .run(
                              `${path}/${d.id}/rescan${suffix}`,
                              {
                                expectedVersion: version,
                                expectedDocumentVersion: d.version,
                                reason: reason?.trim(),
                              },
                              'POST',
                              writeResult,
                            )
                            .then(saved)
                        }
                        style={{ height: '32px', fontSize: '12px', gap: '5px' }}
                      >
                        <RefreshCw size={13} />
                        Rescan
                      </Button>
                    )}
                    {verify && (
                      <>
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={
                            d.scanStatus !== 1 ||
                            d.isScanInProgress ||
                            d.lifecycleStatus !== 0 ||
                            downloadPending
                          }
                          onClick={() => void download(d.id, d.fileName, true)}
                          style={{ height: '32px', fontSize: '12px', gap: '5px' }}
                        >
                          <FileCheck size={13} />
                          Review file
                        </Button>
                        <Button
                          variant="primary"
                          size="sm"
                          disabled={
                            action.pending ||
                            d.scanStatus !== 1 ||
                            d.isScanInProgress ||
                            d.lifecycleStatus !== 0 ||
                            !!file
                          }
                          onClick={() => {
                            if (window.confirm('Confirm verification of this document?'))
                              void action
                                .run(
                                  `${path}/${d.id}/verification${suffix}`,
                                  {
                                    decision: 1,
                                    expectedVersion: version,
                                    expectedDocumentVersion: d.version,
                                    reason: reason?.trim() || null,
                                  },
                                  'POST',
                                  writeResult,
                                )
                                .then(saved);
                          }}
                          style={{ height: '32px', fontSize: '12px', gap: '5px' }}
                        >
                          Verify
                        </Button>
                        <Button
                          variant="destructive"
                          size="sm"
                          disabled={
                            action.pending ||
                            !reason?.trim() ||
                            d.isScanInProgress ||
                            d.lifecycleStatus !== 0 ||
                            !!file
                          }
                          onClick={() => {
                            if (window.confirm('Reject this document?'))
                              void action
                                .run(
                                  `${path}/${d.id}/verification${suffix}`,
                                  {
                                    decision: 2,
                                    expectedVersion: version,
                                    expectedDocumentVersion: d.version,
                                    reason: reason?.trim(),
                                  },
                                  'POST',
                                  writeResult,
                                )
                                .then(saved);
                          }}
                          style={{ height: '32px', fontSize: '12px', gap: '5px' }}
                        >
                          Reject
                        </Button>
                      </>
                    )}
                    {withdrawals.map((operation) => (
                      <Button
                        key={operation}
                        variant="ghost"
                        size="sm"
                        disabled={
                          action.pending ||
                          !!file ||
                          !reason?.trim() ||
                          d.lifecycleStatus === 3 ||
                          (operation !== 'archive' && d.lifecycleStatus !== 0)
                        }
                        onClick={() => {
                          if (
                            window.confirm(
                              `${operation.charAt(0).toUpperCase() + operation.slice(1)} this document? The private file and its history will be retained.`,
                            )
                          )
                            void action
                              .run(
                                `${path}/${d.id}/${operation}${suffix}`,
                                {
                                  expectedVersion: version,
                                  expectedDocumentVersion: d.version,
                                  reason: reason?.trim(),
                                },
                                'POST',
                                writeResult,
                              )
                              .then(saved);
                        }}
                        style={{ height: '32px', fontSize: '12px' }}
                      >
                        {operation.charAt(0).toUpperCase() + operation.slice(1)}
                      </Button>
                    ))}
                  </div>
                </article>
              ))}
            </div>
          )}
        </QueryState>
      </div>

      {/* Card 3: Completeness & Compliance Audit (Placed cleanly at the bottom) */}
      {!medical && <DocumentCompleteness key={employeeId} employeeId={employeeId} />}
    </div>
  );
  );
}
