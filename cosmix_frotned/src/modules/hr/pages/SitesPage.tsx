import { useHrAccess } from '../hooks/use-hr-access';
import { SitesWorkspace } from '../components/sites/SitesWorkspace';

export function SitesPage() {
  const access = useHrAccess();
  return <SitesWorkspace key={JSON.stringify([access.subject, access.ready, access.user])} />;
}
