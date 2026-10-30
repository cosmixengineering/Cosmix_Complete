import type { RouteObject } from 'react-router-dom';
import { AllowancesPage } from './AllowancesPage';
import { AdvancesLoansPage } from './AdvancesLoansPage';
import { VehiclesPage } from './VehiclesPage';
import { PayrollHandoffPage } from './PayrollHandoffPage';
import { PayrollAccountingPage } from './PayrollAccountingPage';
import { StatutoryContributionsPage } from './StatutoryContributionsPage';
import { CompanyPayrollReadinessDashboard } from '../components/finance/CompanyPayrollReadinessDashboard';
import { PayrollReconciliationPage } from './PayrollReconciliationPage';

export const financeRoutes: RouteObject[] = [
  { path: '/hr/allowances', element: <AllowancesPage /> },
  { path: '/hr/advances-loans', element: <AdvancesLoansPage /> },
  { path: '/hr/vehicles', element: <VehiclesPage /> },
  { path: '/hr/payroll-handoff', element: <PayrollHandoffPage /> },
  { path: '/hr/payroll-accounting', element: <PayrollAccountingPage /> },
  { path: '/hr/statutory-contributions', element: <StatutoryContributionsPage /> },
  { path: '/hr/payroll-readiness', element: <CompanyPayrollReadinessDashboard /> },
  { path: '/hr/payroll-reconciliation', element: <PayrollReconciliationPage /> },
];
