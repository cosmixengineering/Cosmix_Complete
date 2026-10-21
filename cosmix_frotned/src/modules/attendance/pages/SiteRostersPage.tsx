import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { RefreshCw } from 'lucide-react';
import { useAttendanceAccess } from '../hooks/use-attendance-access';
import { useAttendanceQuery } from '../hooks/use-attendance-query';
import { id } from '../schemas/common';
import { siteRosterHistory } from '../schemas/site-rosters';
import {
  Command,
  Denied,
  PrivateWorkspace,
  QueryState,
  SavedNotice,
} from '../components/SchedulingUi';
import { SiteNavigation } from '../components/References';
import { SiteRosterHistory, type RosterOperation } from '../components/rosters/SiteRosterHistory';
import { SiteRosterForm } from '../components/rosters/SiteRosterForm';
import { SiteTemporaryChangeForm } from '../components/rosters/SiteTemporaryChangeForm';
import { TemporaryAmendmentForm } from '../components/rosters/TemporaryAmendmentForm';

function RostersWorkspace({ siteId }: { siteId: string }) {
  const access = useAttendanceAccess();
  const canRead = access.can('attendance.rosters.read', siteId);
  const canManage = access.can('attendance.rosters.manage', siteId);
  const query = useAttendanceQuery(
    `/hr/sites/${siteId}/shift-assignments`,
    siteRosterHistory,
    canRead,
  );
  const [operation, setOperation] = useState<RosterOperation | null>(null);
  const [notice, setNotice] = useState('');
  function close() {
    setOperation(null);
    void query.refetch();
  }
  function saved() {
    setNotice('Site schedule saved. Reloading current history.');
    close();
  }
  if (!canRead)
    return <Denied>This site requires a matching Site or All roster read grant.</Denied>;
  const history = query.data;
  return (
    <>
      <div className="attendance-heading">
        <h1>Site Rosters</h1>
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={Boolean(operation) || query.isFetching}
          onClick={() => void query.refetch()}
        >
          Reload
        </Command>
      </div>
      <p>Site: {siteId}</p>
      <SavedNotice message={notice} />
      <QueryState query={query} />
      {history && !query.isError && (
        <>
          {operation &&
            canManage &&
            (operation.mode === 'assign' ||
            operation.mode === 'replace' ||
            operation.mode === 'end' ? (
              <SiteRosterForm
                history={history}
                mode={operation.mode}
                assignmentId={operation.assignmentId}
                onSaved={saved}
                onCancel={close}
              />
            ) : operation.mode === 'temporary' ? (
              <SiteTemporaryChangeForm history={history} onSaved={saved} onCancel={close} />
            ) : 'changeId' in operation && operation.changeId ? (
              <TemporaryAmendmentForm
                history={history}
                mode={operation.mode}
                changeId={operation.changeId}
                onSaved={saved}
                onCancel={close}
              />
            ) : null)}
          <SiteRosterHistory
            history={history}
            canManage={canManage}
            busy={Boolean(operation) || query.isFetching}
            onAction={setOperation}
          />
        </>
      )}
    </>
  );
}
export function SiteRostersPage() {
  const { siteId } = useParams<{ siteId: string }>();
  return (
    <PrivateWorkspace resource={siteId}>
      <SiteNavigation siteId={siteId} mode="rosters" />
      {siteId ? (
        id.safeParse(siteId).success ? (
          <RostersWorkspace key={siteId} siteId={siteId} />
        ) : (
          <Denied>The route does not contain a valid site ID.</Denied>
        )
      ) : (
        <Denied>Select an authorized site to view its scheduling history.</Denied>
      )}
    </PrivateWorkspace>
  );
}
