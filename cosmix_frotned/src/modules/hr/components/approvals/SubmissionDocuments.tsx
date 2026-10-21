import { useState } from 'react';
import { Download, FileCheck } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import { useHrAccess } from '../../hooks/use-hr-access';
import { documentsSchema } from '../../schemas/hiring';
import { documentTypes, type EmployeeDocument } from '../../types/hiring';
import { downloadFile } from '../../api/hr-api';
import { errorMessage } from '../../utils/messages';
import { QueryState } from '../Feedback';

export function SubmissionDocuments({
  employeeId,
  documents,
  documentIds,
  medical = false,
}: {
  employeeId: string;
  documents?: EmployeeDocument[];
  documentIds?: string[];
  medical?: boolean;
}) {
  const suffix = medical ? '?medical=true' : '';
  const access = useHrAccess();
  const path = `employees/${employeeId}/documents`;
  const query = useHrQuery(path + suffix, documentsSchema, [
    'hr.employees.read',
    'hr.employees.documents.read',
    ...(medical ? ['hr.employees.medical.read'] : []),
  ]);
  const lifetime = useHrLifetime();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const rows = documents ?? (query.data ?? []).filter((d) => documentIds?.includes(d.id));
  async function download(document: EmployeeDocument, review: boolean) {
    const signal = lifetime();
    if (pending || signal.aborted) return;
    setPending(true);
    setError('');
    try {
      await downloadFile(
        `${path}/${document.id}/${review ? 'review' : 'download'}${suffix}`,
        document.fileName,
        signal,
      );
    } catch (e) {
      if (!signal.aborted) setError(errorMessage(e));
    } finally {
      if (!signal.aborted) setPending(false);
    }
  }
  return (
    <section className="hr-form-section">
      <h3>{medical ? 'Current medical file details' : 'Submitted documents'}</h3>
      {error && (
        <p role="alert" className="hr-message hr-message--error">
          {error}
        </p>
      )}
      <QueryState query={query}>
        {!rows.length && (
          <p className="muted">
            {medical && documentIds?.length
              ? 'Supporting medical document is unavailable.'
              : 'No documents in this submission.'}
          </p>
        )}
        {rows.map((d) => {
          const current = query.data?.find((r) => r.id === d.id);
          const matches = !!current && (!documents || current.version === d.version);
          return (
            <article className="hr-document" key={d.id}>
              <div>
                <h3>{d.fileName}</h3>
                <p>
                  {documentTypes[d.type - 1]} &middot; {(d.sizeBytes / 1024).toFixed(1)} KiB
                </p>
                <p>
                  Scan: {['Pending', 'Clean', 'Rejected', 'Failed'][d.scanStatus]} &middot;
                  Verification: {['Pending', 'Verified', 'Rejected'][d.verificationStatus]}
                </p>
                {!matches && (
                  <p role="alert">
                    Document evidence has changed since submission. Review is required.
                  </p>
                )}
              </div>
              <div className="hr-actions">
                <Button
                  variant="secondary"
                  disabled={pending || !matches || !current?.isAvailable}
                  onClick={() => void download(d, false)}
                >
                  <Download size={16} />
                  Download
                </Button>
                <Button
                  variant="secondary"
                  disabled={
                    pending ||
                    !matches ||
                    current?.scanStatus !== 1 ||
                    !access.can('hr.employees.documents.verify')
                  }
                  onClick={() => void download(d, true)}
                >
                  <FileCheck size={16} />
                  Review file
                </Button>
              </div>
            </article>
          );
        })}
      </QueryState>
    </section>
  );
}
