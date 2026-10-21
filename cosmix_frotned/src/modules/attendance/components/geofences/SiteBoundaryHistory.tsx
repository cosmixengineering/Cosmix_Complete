import { useState } from 'react';
import { Eye, RefreshCw } from 'lucide-react';
import { Pagination } from '@/components/data-display/Pagination';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { pageOf, organizationChange } from '../../schemas/common';
import { boundary, boundaryPage } from '../../schemas/site-geofences';
import { Command, Denied, QueryState } from '../SchedulingUi';
import { CirclePreview } from './CirclePreview';

export function SiteBoundaryHistory({
  siteId,
  administration = false,
  disabled = false,
}: {
  siteId: string;
  administration?: boolean;
  disabled?: boolean;
}) {
  const access = useAttendanceAccess();
  const allowed = access.can(administration ? 'hr.sites.manage' : 'hr.sites.read');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<string | null>(null);
  const path = `/hr/sites/${siteId}/geofences`;
  const versions = useAttendanceQuery(
    `${path}/versions?page=${page}&pageSize=20`,
    boundaryPage,
    allowed && !administration,
  );
  const history = useAttendanceQuery(
    `${path}/history?page=${page}&pageSize=20`,
    pageOf(organizationChange),
    allowed && administration,
  );
  const detail = useAttendanceQuery(
    `${path}/versions/${selected ?? ''}`,
    boundary,
    allowed && !administration && Boolean(selected),
  );
  if (!allowed)
    return (
      <Denied>All-scoped site {administration ? 'management' : 'read'} access is required.</Denied>
    );
  return (
    <section className="attendance-section">
      <div className="attendance-heading">
        <h2>{administration ? 'Administration History' : 'Immutable Boundary Versions'}</h2>
        <Command
          icon={RefreshCw}
          variant="secondary"
          disabled={disabled || (administration ? history.isFetching : versions.isFetching)}
          onClick={() => {
            void (administration ? history : versions).refetch();
            if (selected) void detail.refetch();
          }}
        >
          {administration ? 'Reload history' : 'Reload versions'}
        </Command>
      </div>
      {administration ? (
        <>
          <p>Administrator access is required for this history.</p>
          <QueryState
            query={history}
            empty={!history.data?.items.length}
            emptyText="No geofence administration history recorded."
          />
          {history.data && !history.isError && (
            <>
              <div className="hr-table-scroll">
                <table className="attendance-table">
                  <thead>
                    <tr>
                      <th>When / actor</th>
                      <th>Operation</th>
                      <th>Reason</th>
                      <th>Recorded evidence</th>
                    </tr>
                  </thead>
                  <tbody>
                    {history.data.items.map((row) => (
                      <tr key={row.id}>
                        <td>
                          {row.occurredAt}
                          <br />
                          {row.actorUserId}
                          <br />
                          {row.actorRoleCode}
                        </td>
                        <td>{row.operation}</td>
                        <td>{row.reason}</td>
                        <td>
                          <details>
                            <summary>Before / after evidence</summary>
                            <p>{row.beforeJson ?? 'No previous boundary'}</p>
                            <p>{row.afterJson}</p>
                          </details>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Pagination
                page={page}
                pageSize={20}
                total={history.data.totalCount}
                disabled={disabled || history.isFetching}
                onChange={setPage}
              />
            </>
          )}
        </>
      ) : (
        <>
          <QueryState
            query={versions}
            empty={!versions.data?.items.length}
            emptyText="No boundary versions published."
          />
          {versions.data && !versions.isError && (
            <>
              <div className="hr-table-scroll">
                <table className="attendance-table">
                  <thead>
                    <tr>
                      <th>Version</th>
                      <th>Effective from</th>
                      <th>Center</th>
                      <th>Radius / accuracy</th>
                      <th>Detail</th>
                    </tr>
                  </thead>
                  <tbody>
                    {versions.data.items.map((row) => (
                      <tr key={row.id}>
                        <td>v{row.versionNumber}</td>
                        <td>{row.effectiveFrom}</td>
                        <td>
                          {row.latitude}, {row.longitude}
                        </td>
                        <td>
                          {row.radiusMeters} m / {row.maximumAccuracyMeters} m
                        </td>
                        <td>
                          <Command
                            icon={Eye}
                            variant="ghost"
                            disabled={disabled}
                            onClick={() => setSelected(row.id)}
                          >
                            Open
                          </Command>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Pagination
                page={page}
                pageSize={20}
                total={versions.data.totalCount}
                disabled={disabled || versions.isFetching}
                onChange={setPage}
              />
            </>
          )}
          {selected && (
            <>
              <h3>Boundary Version Detail</h3>
              <QueryState query={detail} />
              {detail.data && !detail.isError && (
                <>
                  <p>
                    v{detail.data.versionNumber} from {detail.data.effectiveFrom}
                    <br />
                    {detail.data.id}
                  </p>
                  <CirclePreview {...detail.data} />
                </>
              )}
              {detail.isError && (
                <Command
                  icon={RefreshCw}
                  variant="secondary"
                  disabled={disabled}
                  onClick={() => void detail.refetch()}
                >
                  Reload detail
                </Command>
              )}
            </>
          )}
        </>
      )}
    </section>
  );
}
