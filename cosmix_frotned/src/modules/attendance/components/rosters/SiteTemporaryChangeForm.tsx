import { useState } from 'react';
import { z } from 'zod';
import { Check, Eye } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { queryString } from '../../api/attendance-api';
import {
  temporaryPreview,
  temporaryWrite,
  type SiteRosterHistory,
} from '../../schemas/site-rosters';
import { shiftVersion } from '../../schemas/shifts';
import { businessToday } from '../../utils/messages';
import { nextDate } from '../../utils/shift-schedule';
import { freezeBody, previewMatches, temporaryBody } from '../../utils/roster-intent';
import { VersionReference } from '../References';
import { Command, MutationForm, QueryState } from '../SchedulingUi';
import { StaffManifest } from './SiteRosterHistory';

export function SiteTemporaryChangeForm({
  history,
  onSaved,
  onCancel,
}: {
  history: SiteRosterHistory;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const access = useAttendanceAccess();
  const [source] = useState(history);
  const [businessDate, setDate] = useState('');
  const [version, setVersion] = useState<z.infer<typeof shiftVersion> | null>(null);
  const [request, setRequest] = useState<{ businessDate: string; versionId: string } | null>(null);
  const [reviewed, setReviewed] = useState<z.infer<typeof temporaryPreview> | null>(null);
  const [evidence, setEvidence] = useState('');
  const [inputReason, setReason] = useState('');
  const root = `/hr/sites/${source.siteId}/temporary-shift-changes`;
  const query = useAttendanceQuery(
    root +
      '/preview' +
      queryString({
        businessDate: request?.businessDate,
        replacementShiftVersionId: request?.versionId,
      }),
    temporaryPreview,
    Boolean(request) && access.can('attendance.rosters.manage', source.siteId),
  );
  const matches = (preview: z.infer<typeof temporaryPreview>) =>
    previewMatches(preview, source.siteId, businessDate, version?.id ?? '', history.timeline);
  const valid = reviewed && matches(reviewed);
  function reset() {
    setRequest(null);
    setReviewed(null);
  }
  function build() {
    if (!valid || !reviewed) throw new Error('Obtain and review a fresh matching preview.');
    if (businessDate <= businessToday()) throw new Error('The temporary date must follow today.');
    return {
      path: root,
      method: 'POST' as const,
      body: temporaryBody(reviewed, evidence, inputReason),
      schema: temporaryWrite,
    };
  }
  return (
    <MutationForm
      title="Temporary site shift change"
      build={build}
      disabled={!valid}
      onSaved={onSaved}
      onCancel={onCancel}
    >
      <div className="attendance-grid">
        <TextInput
          label="Business date"
          type="date"
          min={nextDate(businessToday())}
          required
          value={businessDate}
          onChange={(event) => {
            setDate(event.target.value);
            reset();
          }}
        />
        <VersionReference
          value={version}
          onChange={(value) => {
            setVersion(value);
            reset();
          }}
        />
      </div>
      <Command
        icon={Eye}
        variant="secondary"
        disabled={!version || businessDate <= businessToday() || query.isFetching}
        onClick={() => {
          setReviewed(null);
          const next = { businessDate, versionId: version!.id };
          if (request?.businessDate === next.businessDate && request?.versionId === next.versionId)
            void query.refetch();
          else setRequest(next);
        }}
      >
        Preview affected staff
      </Command>
      {request && (
        <>
          <QueryState query={query} />
          {query.data && !query.isError && (
            <>
              <p>
                Business date: {query.data.businessDate}
                <br />
                Original version: {query.data.originalShiftVersionId}
                <br />
                Replacement version: {query.data.replacementShiftVersionId}
              </p>
              <StaffManifest ids={query.data.affectedEmployeeIds} />
              <Command
                icon={Check}
                variant="secondary"
                disabled={!matches(query.data) || query.isFetching}
                onClick={() => setReviewed(freezeBody(query.data!))}
              >
                Use reviewed preview
              </Command>
              {!matches(query.data) && (
                <p role="alert">
                  The preview no longer matches this site timeline or selection. Reload and review
                  the site history.
                </p>
              )}
            </>
          )}
        </>
      )}
      {valid && (
        <p role="status">
          Reviewed manifest retained: {reviewed.affectedEmployeeIds.length} employees.
        </p>
      )}
      <TextInput
        label="Written evidence reference"
        required
        maxLength={500}
        value={evidence}
        onChange={(event) => setEvidence(event.target.value)}
      />
      <TextInput
        label="Reason"
        required
        maxLength={1000}
        value={inputReason}
        onChange={(event) => setReason(event.target.value)}
      />
    </MutationForm>
  );
}
