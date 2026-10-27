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
    <section>
      <h2>{medical ? 'Private medical documents' : 'Employee documents'}</h2>
      {!medical && <DocumentCompleteness key={employeeId} employeeId={employeeId} />}
      {manage && (
        <form
          className="hr-form-section"
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
          <fieldset disabled={action.pending}>
            <div className="hr-form-grid modern-form-grid-3">
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
              <div className="col-span-full" style={{ gridColumn: '1 / -1' }}>
                <FileUploadDropzone
                  label="Upload your Document File"
                  required
                  value={file}
                  onChange={setFile}
                  error={error ?? undefined}
                  dataField="documentFile"
                  hint="Supports PDF, PNG, JPG (Max 10 MB). For photos, use PNG or JPG."
                />
              </div>
            </div>
            <div className="hr-actions" style={{ marginTop: '16px' }}>
              <Button type="submit" variant="primary" disabled={!file || !type || action.pending}>
                <Upload size={16} />
                {action.pending ? 'Uploading document...' : 'Upload document'}
              </Button>
            </div>
          </fieldset>
        </form>
      )}
      {(verify || manage || withdrawals.length > 0) && (
        <LongField
          label="Document action reason (required for rescan, rejection and withdrawal)"
          max={1000}
          value={reason}
          onChange={setReason}
        />
      )}
      {error && (
        <p role="alert" className="hr-message hr-message--error">
          {error}
        </p>
      )}
      <ActionMessage action={{ error: action.error, success: null }} />
      <QueryState query={query}>
        {!query.data?.length && <p className="hr-notice">No documents uploaded.</p>}
        {query.data?.map((d) => (
          <article key={d.id} className="hr-document">
            <div>
              <h3>{d.fileName}</h3>
              <p className="muted">
                {documentTypes[d.type - 1]} &middot; {(d.sizeBytes / 1024).toFixed(1)} KiB
              </p>
              <p>
                Scan:{' '}
                {d.isScanInProgress
                  ? 'In progress; unavailable until completed'
                  : ['Pending', 'Clean', 'Rejected', 'Failed'][d.scanStatus]}{' '}
                &middot; Verification: {['Pending', 'Verified', 'Rejected'][d.verificationStatus]}
              </p>
              <p className="muted">
                Status: {['Current', 'Expired', 'Revoked', 'Archived'][d.lifecycleStatus]}
                {d.scanStatus === 2 && ' · Quarantined after scanner rejection'}
                {d.scanStatus === 3 && ' · Scanner unavailable; rescan required'}
              </p>
              <p className="muted">
                Issued {labelDate(d.issuedOn)} &middot; Expires {labelDate(d.expiresOn)}
              </p>
              <span className={`hr-status hr-status--${d.isAvailable ? 'positive' : 'negative'}`}>
                {d.isAvailable ? 'Available' : 'Unavailable'}
              </span>
              <DocumentHistory
                key={d.id}
                employeeId={employeeId}
                documentId={d.id}
                medical={medical}
              />
            </div>
            <div className="hr-actions">
              <Button
                variant="secondary"
                disabled={!d.isAvailable || downloadPending}
                onClick={() => void download(d.id, d.fileName, false)}
              >
                <Download size={16} />
                Download
              </Button>
              {manage && (
                <Button
                  variant="secondary"
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
                >
                  <RefreshCw size={16} />
                  Rescan
                </Button>
              )}
              {verify && (
                <>
                  <Button
                    variant="secondary"
                    disabled={
                      d.scanStatus !== 1 ||
                      d.isScanInProgress ||
                      d.lifecycleStatus !== 0 ||
                      downloadPending
                    }
                    onClick={() => void download(d.id, d.fileName, true)}
                  >
                    <FileCheck size={16} />
                    Review file
                  </Button>
                  <Button
                    variant="secondary"
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
                  >
                    Verify
                  </Button>
                  <Button
                    variant="secondary"
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
                  >
                    Reject document
                  </Button>
                </>
              )}
              {withdrawals.map((operation) => (
                <Button
                  key={operation}
                  variant="secondary"
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
                >
                  {operation.charAt(0).toUpperCase() + operation.slice(1)}
                </Button>
              ))}
            </div>
          </article>
        ))}
      </QueryState>
    </section>
  );
}
