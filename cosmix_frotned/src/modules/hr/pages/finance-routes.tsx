import type { RouteObject } from 'react-router-dom';
import { AllowancesPage } from './AllowancesPage';
import { AdvancesLoansPage } from './AdvancesLoansPage';
import { VehiclesPage } from './VehiclesPage';
import { PayrollHandoffPage } from './PayrollHandoffPage';

export const financeRoutes: RouteObject[] = [
  { path: '/hr/allowances', element: <AllowancesPage /> },
  { path: '/hr/advances-loans', element: <AdvancesLoansPage /> },
  { path: '/hr/vehicles', element: <VehiclesPage /> },
  { path: '/hr/payroll-handoff', element: <PayrollHandoffPage /> },
];
