import { useState } from 'react';
import { z } from 'zod';
import { Check, Eye } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { queryString } from '../../api/attendance-api';
import {
  amendmentPreview,
  amendmentWrite,
  type SiteRosterHistory,
} from '../../schemas/site-rosters';
import { shiftVersion } from '../../schemas/shifts';
import { businessToday } from '../../utils/messages';
import { amendmentBody, freezeBody } from '../../utils/roster-intent';
import { VersionReference } from '../References';
import { Command, MutationForm, QueryState } from '../SchedulingUi';
import { StaffManifest } from './SiteRosterHistory';

export function TemporaryAmendmentForm({
  history,
  mode,
  changeId,
  onSaved,
  onCancel,
}: {
  history: SiteRosterHistory;
  mode: 'cancel' | 'supersede';
  changeId: string;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const access = useAttendanceAccess();
  const [source] = useState(history);
  const original = source.temporaryChanges.find((row) => row.id === changeId);
  const [version, setVersion] = useState<z.infer<typeof shiftVersion> | null>(null);
  const [request, setRequest] = useState<string | null>(null);
  const [reviewed, setReviewed] = useState<z.infer<typeof amendmentPreview> | null>(null);
  const [evidence, setEvidence] = useState('');
  const [inputReason, setReason] = useState('');
  const root = `/hr/sites/${source.siteId}/temporary-shift-changes/${changeId}/${mode}`;
  const query = useAttendanceQuery(
    root +
      '/preview' +
      queryString({
        replacementShiftVersionId: mode === 'supersede' ? (request ?? undefined) : undefined,
      }),
    amendmentPreview,
    request !== null && access.can('attendance.rosters.manage', source.siteId),
  );
  function matches(preview: z.infer<typeof amendmentPreview>) {
    return (
      preview.siteId === source.siteId &&
      preview.changeId === changeId &&
      preview.businessDate === original?.businessDate &&
      preview.timeline === history.timeline &&
      (mode === 'cancel'
        ? preview.replacementShiftVersionId === null
        : preview.replacementShiftVersionId === version?.id)
    );
  }
  const valid = reviewed && matches(reviewed);
  function build() {
    if (
      !valid ||
      !reviewed ||
      !original ||
      original.status !== 'active' ||
      original.businessDate <= businessToday()
    )
      throw new Error('Obtain and review a fresh preview of an active future change.');
    const body = amendmentBody(reviewed, evidence, inputReason);
    return {
      path: root,
      method: 'POST' as const,
      schema: amendmentWrite,
      body:
        mode === 'supersede'
          ? { ...body, replacementShiftVersionId: reviewed.replacementShiftVersionId }
          : body,
    };
  }
  return (
    <MutationForm
      title={mode === 'cancel' ? 'Cancel temporary change' : 'Supersede temporary change'}
      build={build}
      disabled={!valid}
      onSaved={onSaved}
      onCancel={onCancel}
    >
      <p>
        Original change: {changeId}
        <br />
        Business date: {original?.businessDate}
      </p>
      {original && (
        <StaffManifest ids={original.affectedEmployeeIds} title="Original immutable manifest" />
      )}
      {mode === 'supersede' && (
        <VersionReference
          value={version}
          onChange={(value) => {
            setVersion(value);
            setRequest(null);
            setReviewed(null);
          }}
        />
      )}
      <Command
        icon={Eye}
        variant="secondary"
        disabled={query.isFetching || (mode === 'supersede' && !version)}
        onClick={() => {
          setReviewed(null);
          const next = mode === 'cancel' ? 'cancel' : version!.id;
          if (request === next) void query.refetch();
          else setRequest(next);
        }}
      >
        {mode === 'cancel' ? 'Preview base restoration' : 'Preview successor schedule'}
      </Command>
      {request !== null && (
        <>
          <QueryState query={query} />
          {query.data && !query.isError && (
            <>
              <p>
                Base version: {query.data.baseShiftVersionId}
                <br />
                Successor version: {query.data.replacementShiftVersionId ?? 'Base restored'}
              </p>
              <StaffManifest
                ids={query.data.previousAffectedEmployeeIds}
                title="Previous retained manifest"
              />
              <StaffManifest
                ids={query.data.affectedEmployeeIds}
                title="New server-resolved manifest"
              />
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
                  The preview changed. Reload the site history before reviewing a new amendment.
                </p>
              )}
            </>
          )}
        </>
      )}
      {valid && (
        <p role="status">
          Reviewed amendment retained: {reviewed.affectedEmployeeIds.length} employees.
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
