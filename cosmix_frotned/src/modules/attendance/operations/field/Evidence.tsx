import { useState } from 'react';
import { Download } from 'lucide-react';
import { request } from '@/services/api/http-client';
import { TextInput } from '@/components/forms/TextInput';
import { Command } from '../../components/SchedulingUi';
import { useAttendanceLifetime } from '../../hooks/use-attendance-lifetime';
import { attendanceError } from '../../utils/messages';
import type { Attachment } from './contracts';
import { readAttachment } from './attachment';

export function AttachmentField({
  onChange,
  onBusy,
  onError,
}: {
  onChange: (value: Attachment | null) => void;
  onBusy: (busy: boolean) => void;
  onError: (error: string) => void;
}) {
  const lifetime = useAttendanceLifetime();
  const [busy, setBusy] = useState(false);
  async function choose(file?: File) {
    const signal = lifetime();
    onChange(null);
    onError('');
    if (!file) return;
    setBusy(true);
    onBusy(true);
    try {
      const attachment = await readAttachment(file, signal);
      signal.throwIfAborted();
      onChange(attachment);
    } catch (error) {
      if (!signal.aborted)
        onError(error instanceof Error ? error.message : 'Attachment unavailable.');
    } finally {
      if (!signal.aborted) {
        setBusy(false);
        onBusy(false);
      }
    }
  }
  return (
    <TextInput
      label="Response attachment (PNG / JPEG / PDF, maximum 10 MiB)"
      type="file"
      accept="image/png,image/jpeg,application/pdf"
      disabled={busy}
      onChange={(e) => {
        void choose(e.target.files?.[0]);
      }}
    />
  );
}

export function EvidenceDownload({ path, name }: { path: string; name: string }) {
  const lifetime = useAttendanceLifetime();
  const [pending, setPending] = useState(false),
    [error, setError] = useState('');
  async function download() {
    const signal = lifetime();
    if (pending || signal.aborted) return;
    setPending(true);
    setError('');
    let url: string | undefined;
    try {
      const result = await request(path, { responseType: 'blob', signal });
      signal.throwIfAborted();
      if (
        !(result instanceof Blob) ||
        result.size <= 0 ||
        result.size > 10 * 1024 * 1024 ||
        !['image/png', 'image/jpeg', 'application/pdf'].includes(result.type)
      )
        throw new Error('Invalid private evidence response.');
      url = URL.createObjectURL(result);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download =
        name +
        (result.type === 'application/pdf'
          ? '.pdf'
          : result.type === 'image/png'
            ? '.png'
            : '.jpg');
      anchor.click();
    } catch (failure) {
      if (!signal.aborted) setError(attendanceError(failure));
    } finally {
      if (url) URL.revokeObjectURL(url);
      if (!signal.aborted) setPending(false);
    }
  }
  return (
    <>
      <Command
        icon={Download}
        variant="secondary"
        disabled={pending}
        onClick={() => {
          void download();
        }}
      >
        {pending ? 'Downloading...' : 'Download evidence'}
      </Command>
      {error && (
        <p role="alert" className="attendance-error">
          {error}
        </p>
      )}
    </>
  );
}
