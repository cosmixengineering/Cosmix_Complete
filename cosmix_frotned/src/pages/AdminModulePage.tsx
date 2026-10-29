import { Link, Navigate } from 'react-router-dom';

const connected: Record<string, string> = {
  panel: '/admin/account',
  departments: '/hr/organization?group=departments',
  reports: '/hr/reports/hr',
  finance: '/hr/payroll-handoff',
};
const titles: Record<string, string> = {
  inventory: 'Inventory',
  procurement: 'Procurement',
  sales: 'Sales and CRM',
  operations: 'Operations',
  projects: 'Projects',
  settings: 'Company settings',
  help: 'Help and support',
};

export function AdminModulePage({ moduleKey }: { moduleKey: string }) {
  if (connected[moduleKey]) return <Navigate to={connected[moduleKey]} replace />;
  return (
    <section className="attendance" aria-labelledby="module-status-heading">
      <h1 id="module-status-heading">{titles[moduleKey] ?? 'Module unavailable'}</h1>
      <p role="status">This workspace is not connected in the current web application.</p>
      <p>Use the available employee, attendance and approval workflows from the dashboard.</p>
      <Link className="button button--secondary" to="/admin/dashboard">
        Back to dashboard
      </Link>
    </section>
  );
}
