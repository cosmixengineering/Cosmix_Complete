import { Link, useParams, useLocation } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { EmployeeRecordsWorkspace } from './EmployeeRecordsWorkspace';
import { familyOf } from './access';
import type { RecordKind } from './contracts';

function EmployeeRecordPage({ kind }: { kind: RecordKind }) {
  const { employeeId = '' } = useParams();
  const location = useLocation();
  const basePath = location.pathname.startsWith('/admin') ? '/admin' : '/hr';
  return (
    <>
      <Link className="hr-back" to={`${basePath}/employees`}>
        <ArrowLeft size={16} />
        Employees
      </Link>
      <div className="hr-page-heading">
        <h1>{familyOf(kind).label}</h1>
      </div>
      <EmployeeRecordsWorkspace employeeId={employeeId} kind={kind} />
    </>
  );
}
export function EmployeeAssetsPage() {
  return <EmployeeRecordPage kind="Asset" />;
}
export function EmployeeSimsPage() {
  return <EmployeeRecordPage kind="Sim" />;
}
export function EmployeeCertificationsPage() {
  return <EmployeeRecordPage kind="Certification" />;
}
