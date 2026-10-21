import { useState } from 'react';
import { Check, Mail, X } from 'lucide-react';
import { Pagination } from '@/components/data-display/Pagination';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { queryString } from '../../api/attendance-api';
import { Command, MutationForm, QueryState } from '../../components/SchedulingUi';
import {
  notification,
  notificationRead,
  notificationReadInput,
  notificationsPage,
} from './contracts';
import { RecordFields, Reload } from './FieldUi';
import { FieldWorkspace } from './FieldWorkspace';

function NotificationDetail({
  notificationId,
  close,
}: {
  notificationId: string;
  close: () => void;
}) {
  const access = useAttendanceAccess();
  const path = `/me/notifications/${notificationId}`;
  const query = useAttendanceQuery(path, notification, access.ready);
  const [marking, setMarking] = useState(false);
  function reload() {
    setMarking(false);
    void query.refetch();
  }
  return (
    <section className="attendance-section">
      <div className="attendance-heading">
        <h2>Notification</h2>
        <div className="attendance-commands">
          <Reload disabled={marking || query.isFetching} onClick={reload} />
          <Command icon={X} variant="secondary" disabled={marking} onClick={close}>
            Close
          </Command>
        </div>
      </div>
      <QueryState query={query} />
      {query.data && !query.isError && query.data.id === notificationId && (
        <>
          <h3>{query.data.title}</h3>
          <p>{query.data.body}</p>
          <RecordFields values={{ Created: query.data.createdAt, Read: query.data.readAt }} />
          {!query.data.readAt && (
            <Command
              icon={Check}
              disabled={marking || query.isFetching}
              onClick={() => setMarking(true)}
            >
              Mark read
            </Command>
          )}
          {marking && (
            <MutationForm
              title="Mark own notification read"
              onSaved={reload}
              onCancel={reload}
              submitLabel="Mark read"
              build={() => ({
                path: path + '/read',
                method: 'POST',
                schema: notificationRead,
                body: notificationReadInput.parse({ expectedVersion: query.data?.version }),
              })}
            >
              <p>{query.data.title}</p>
            </MutationForm>
          )}
        </>
      )}
    </section>
  );
}

function NotificationsWorkspace() {
  const access = useAttendanceAccess();
  const [page, setPage] = useState(1),
    [unread, setUnread] = useState(false),
    [selected, setSelected] = useState('');
  const query = useAttendanceQuery(
    '/me/notifications' + queryString({ page, pageSize: 25, unreadOnly: unread }),
    notificationsPage,
    access.ready && !selected,
  );
  return (
    <>
      <h1>My Notifications</h1>
      {selected ? (
        <NotificationDetail
          key={selected}
          notificationId={selected}
          close={() => {
            setSelected('');
            void query.refetch();
          }}
        />
      ) : (
        <>
          <div className="attendance-commands">
            <label className="attendance-check">
              <input
                type="checkbox"
                checked={unread}
                disabled={query.isFetching}
                onChange={(e) => {
                  setUnread(e.target.checked);
                  setPage(1);
                }}
              />
              Unread only
            </label>
            <Reload
              disabled={query.isFetching}
              onClick={() => {
                void query.refetch();
              }}
            />
          </div>
          <QueryState query={query} empty={query.data?.items.length === 0} />
          {query.data && !query.isError && (
            <>
              {query.data.items.map((n) => (
                <section className="attendance-section" key={n.id}>
                  <h2>{n.title}</h2>
                  <p>{n.body}</p>
                  <RecordFields values={{ Created: n.createdAt, Read: n.readAt }} />
                  <Command
                    icon={Mail}
                    variant="secondary"
                    disabled={query.isFetching}
                    onClick={() => setSelected(n.id)}
                  >
                    Open notification
                  </Command>
                </section>
              ))}
              <Pagination
                page={query.data.page}
                pageSize={query.data.pageSize}
                total={query.data.totalCount}
                onChange={setPage}
                disabled={query.isFetching}
              />
            </>
          )}
        </>
      )}
    </>
  );
}
export function MyNotificationsPage() {
  return (
    <FieldWorkspace>
      <NotificationsWorkspace />
    </FieldWorkspace>
  );
}
