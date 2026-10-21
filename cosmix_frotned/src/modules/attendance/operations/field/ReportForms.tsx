import { useState } from 'react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { MutationForm } from '../../components/SchedulingUi';
import {
  decisionInput,
  reportDetail,
  responseInput,
  returnedReview,
  type Attachment,
  type ReportDetail,
} from './contracts';
import { AttachmentField } from './Evidence';

export function ReportDecisionForm({
  detail,
  siteId,
  close,
}: {
  detail: ReportDetail;
  siteId: string;
  close: () => void;
}) {
  const [outcome, setOutcome] = useState(''),
    [reason, setReason] = useState('');
  return (
    <MutationForm
      title="HR report decision"
      onSaved={close}
      onCancel={close}
      build={() => ({
        path: `/site-reports/sites/${siteId}/${detail.report.id}/decisions`,
        method: 'POST',
        schema: reportDetail,
        body: decisionInput.parse({
          expectedReviewSequence: detail.report.reviewSequence,
          expectedResponseSequence: detail.report.responseSequence,
          outcome: Number(outcome),
          reason,
        }),
      })}
    >
      <p>
        Review sequence {detail.report.reviewSequence}; response sequence{' '}
        {detail.report.responseSequence}
      </p>
      <SelectInput
        label="Outcome"
        required
        value={outcome}
        onChange={(e) => setOutcome(e.target.value)}
        options={[
          { value: '', label: 'Select decision' },
          { value: '1', label: 'Accept' },
          { value: '2', label: 'Return to employee' },
          { value: '3', label: 'Reject' },
        ]}
      />
      <TextInput
        label="Reason"
        required
        maxLength={1000}
        value={reason}
        onChange={(e) => setReason(e.target.value)}
      />
    </MutationForm>
  );
}

export function ReportResponseForm({ detail, close }: { detail: ReportDetail; close: () => void }) {
  const [text, setText] = useState(''),
    [attachment, setAttachment] = useState<Attachment | null>(null);
  const [busy, setBusy] = useState(false),
    [error, setError] = useState('');
  return (
    <MutationForm
      title="Answer returned report"
      disabled={busy || Boolean(error)}
      onSaved={close}
      onCancel={close}
      submitLabel="Resubmit"
      build={() => {
        const returned = returnedReview(detail);
        if (!returned?.id || busy || error)
          throw new Error('Reload the returned report or correct its attachment.');
        return {
          path: `/site-reports/mine/${detail.report.id}/responses`,
          method: 'POST',
          schema: reportDetail,
          body: responseInput.parse({
            expectedReviewSequence: detail.report.reviewSequence,
            expectedResponseSequence: detail.report.responseSequence,
            returnedReviewId: returned.id,
            writtenResponse: text,
            attachment,
          }),
        };
      }}
    >
      <p>{returnedReview(detail)?.reason}</p>
      <label className="field">
        Written response
        <textarea
          rows={5}
          required
          maxLength={4000}
          style={{ width: '100%' }}
          value={text}
          onChange={(e) => setText(e.target.value)}
        />
      </label>
      <AttachmentField onChange={setAttachment} onBusy={setBusy} onError={setError} />
      {attachment && <p>Selected: {attachment.fileName}</p>}
      {error && (
        <p role="alert" className="attendance-error">
          {error}
        </p>
      )}
    </MutationForm>
  );
}
