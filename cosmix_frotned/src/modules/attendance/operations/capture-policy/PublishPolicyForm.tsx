import { useId, useState } from 'react';
import { Copy } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { Command, MutationForm } from '../../components/SchedulingUi';
import { publicationSchema } from './contracts';
import {
  blankDraft,
  buildPublication,
  capturePolicyEndpoint,
  draftFromSettings,
  utcFromInput,
  type PublicationSource,
} from './inputs';
import { SettingsFields } from './SettingsFields';

export function PublishPolicyForm({
  source,
  actorId,
  allowed,
  onSaved,
  onCancel,
}: {
  source: PublicationSource;
  actorId: string;
  allowed: boolean;
  onSaved: (result: unknown) => void;
  onCancel: () => void;
}) {
  const [reviewed] = useState(source);
  const [draft, setDraft] = useState(blankDraft);
  const [effective, setEffective] = useState('');
  const [approvalReference, setApprovalReference] = useState('');
  const [reason, setReason] = useState('');
  const reasonId = useId();
  return (
    <MutationForm
      title="Publish capture policy"
      submitLabel="Publish approved policy"
      disabled={!allowed}
      onSaved={onSaved}
      onCancel={onCancel}
      build={() => {
        if (!allowed) throw new Error('Current publication authority is unavailable.');
        const body = buildPublication(draft, reviewed, {
          effectiveFromUtc: utcFromInput(effective),
          approvalReference,
          reason,
        });
        return {
          path: capturePolicyEndpoint,
          method: 'POST',
          body,
          schema: publicationSchema(body, reviewed.latestVersion?.id ?? null, actorId),
        };
      }}
    >
      <dl className="attendance-record">
        <dt>Reviewed timeline revision</dt>
        <dd>{reviewed.preview.latestRevision}</dd>
        <dt>Latest published effective time (UTC)</dt>
        <dd>{reviewed.latestVersion?.effectiveFromUtc ?? 'No published version'}</dd>
      </dl>
      <div className="attendance-grid">
        <TextInput
          label="Effective from (UTC)"
          type="datetime-local"
          step={1}
          required
          value={effective}
          onChange={(event) => setEffective(event.target.value)}
        />
        <TextInput
          label="Approval reference"
          required
          maxLength={200}
          value={approvalReference}
          onChange={(event) => setApprovalReference(event.target.value)}
        />
      </div>
      {reviewed.preview.effective && (
        <Command
          icon={Copy}
          variant="secondary"
          onClick={() => setDraft(draftFromSettings(reviewed.preview.effective!.settings))}
        >
          Use effective settings
        </Command>
      )}
      <SettingsFields draft={draft} onChange={setDraft} />
      <div className="field">
        <label htmlFor={reasonId}>Publication reason</label>
        <textarea
          id={reasonId}
          required
          maxLength={1000}
          rows={3}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
        />
      </div>
    </MutationForm>
  );
}
