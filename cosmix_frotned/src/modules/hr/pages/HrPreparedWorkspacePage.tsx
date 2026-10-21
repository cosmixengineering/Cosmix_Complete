import { useParams } from 'react-router-dom';
import { useHrAccess } from '../hooks/use-hr-access';
import { hrPreparedWorkspaces } from '../workspaces';
import { PreparedWorkspace } from '../components/workspaces/PreparedWorkspace';
import { Denied } from '../components/Feedback';

export function HrPreparedWorkspacePage() {
  const { workspaceId } = useParams();
  const access = useHrAccess();
  const definition = hrPreparedWorkspaces.find((d) => d.id === workspaceId);
  if (!definition) return <p role="alert">Workspace not found.</p>;
  if (!access.can('hr.employees.read', ...(definition.permissions ?? []))) return <Denied />;
  return <PreparedWorkspace key={`${access.subject}:${definition.id}`} definition={definition} />;
}
