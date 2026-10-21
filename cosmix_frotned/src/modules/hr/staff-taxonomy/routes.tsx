import type { RouteObject } from 'react-router-dom';
import {
  StaffTaxonomyNewRequestPage,
  StaffTaxonomyPage,
  StaffTaxonomyRequestPage,
  StaffTaxonomyRequestsPage,
} from './pages';
import { staffTaxonomyPaths } from './paths';

// The parent composes these within its existing authenticated application route.
export const staffTaxonomyRoutes: RouteObject[] = [
  { path: staffTaxonomyPaths.base, element: <StaffTaxonomyPage /> },
  { path: staffTaxonomyPaths.requests, element: <StaffTaxonomyRequestsPage /> },
  { path: staffTaxonomyPaths.newRequest, element: <StaffTaxonomyNewRequestPage /> },
  { path: staffTaxonomyPaths.request, element: <StaffTaxonomyRequestPage /> },
];
