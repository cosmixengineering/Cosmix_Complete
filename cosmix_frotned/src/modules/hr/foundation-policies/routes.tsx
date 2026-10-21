import type { RouteObject } from 'react-router-dom';
import { FoundationPoliciesPage } from './pages';

export const foundationPolicyRoutes: RouteObject[] = [
  { path: '/hr/foundation-policies', element: <FoundationPoliciesPage /> },
  { path: '/hr/foundation-policies/:kind', element: <FoundationPoliciesPage /> },
  { path: '/hr/foundation-policies/:kind/requests', element: <FoundationPoliciesPage requests /> },
  {
    path: '/hr/foundation-policies/:kind/requests/new',
    element: <FoundationPoliciesPage newProposal />,
  },
  {
    path: '/hr/foundation-policies/:kind/requests/:requestId',
    element: <FoundationPoliciesPage requests />,
  },
];
