import { useState, useMemo, useEffect } from 'react';
import { Link } from 'react-router-dom';
import {
  Building2,
  Calendar,
  Clock,
  RefreshCw,
  Search,
  Users,
  FileSpreadsheet,
  FileText,
  Upload,
  CheckCircle2,
  AlertTriangle,
  Radio,
  Briefcase,
  Layers,
  List,
  CheckCheck,
  Palmtree,
  HeartHandshake,
  MoreVertical,
  Fingerprint,
  Smartphone,
  MapPin,
  XCircle,
  ExternalLink,
  Info,
  DollarSign,
  Download,
  X,
  Check,
} from 'lucide-react';
import { useHrQuery } from '@/modules/hr/hooks/use-hr-queries';
import { employeeArchivePageSchema } from '@/modules/hr/components/employee/employee-archive-directory';
import { queryString } from '../api/attendance-api';
import '@/styles/attendance-processing.css';

interface EmployeeMatrixItem {
  id: string;
  name: string;
  initials: string;
  role: string;
  empId: string;
  dept: string;
  site: string;
  avatarBg: string;
  avatarColor: string;
  pattern: Record<number, string>;
  lateChance?: number;
}

interface DailyLogItem {
  id: string;
  empId: string;
  name: string;
  initials: string;
  role: string;
  dept: string;
  site: string;
  subLocation: string;
  shiftSchedule: string;
  punchIn: string;
  punchInType: 'Biometric' | 'Mobile App';
  punchOut: string;
  isPunchOutMissing?: boolean;
  isInProgress?: boolean;
  workHours: string;
  overtime: string;
  variance: string;
  varianceType: 'late' | 'ontime' | 'grace' | 'neutral';
  geofence: string;
  geofenceStatus: 'inside' | 'outside' | 'desk';
  exceptionAlert: string;
  exceptionAlertType: 'orange' | 'red' | 'purple' | 'green';
  status: 'Pending' | 'Waived' | 'Resolved' | 'Approved' | 'Review OT' | 'Present' | 'Rejected';
  avatarBg: string;
  avatarColor: string;
}

const DEFAULT_EMPLOYEES: EmployeeMatrixItem[] = [
  {
    id: 'emp-1',
    name: 'Ali Khan',
    initials: 'AK',
    role: 'HVAC Engineer',
    empId: 'EMP-1042',
    dept: 'MEP & Execution',
    site: 'Karachi High-Rise',
    avatarBg: '#242b5f',
    avatarColor: '#ffffff',
    pattern: { 1: 'L', 2: 'P', 3: 'L', 4: 'P', 5: 'P', 7: 'P' },
    lateChance: 0.08,
  },
  {
    id: 'emp-2',
    name: 'Sara Ahmed',
    initials: 'SA',
    role: 'Sales Executive',
    empId: 'EMP-1043',
    dept: 'Sales & CRM',
    site: 'Head Office (Lahore)',
    avatarBg: '#dbeafe',
    avatarColor: '#1d4ed8',
    pattern: { 1: 'P', 2: 'P', 3: 'P', 4: 'P', 5: 'P', 8: 'LV', 9: 'LV' },
    lateChance: 0.05,
  },
  {
    id: 'emp-3',
    name: 'Usman Tariq',
    initials: 'UT',
    role: 'AC Technician',
    empId: 'EMP-1044',
    dept: 'Field Maintenance',
    site: 'Islamabad Complex',
    avatarBg: '#ffedd5',
    avatarColor: '#ea580c',
    pattern: { 1: 'P', 2: 'P', 3: 'P', 4: 'P', 5: 'A' },
    lateChance: 0.12,
  },
  {
    id: 'emp-4',
    name: 'Zainab Ali',
    initials: 'ZA',
    role: 'HR Assistant',
    empId: 'EMP-1045',
    dept: 'HR Operations',
    site: 'Head Office (Lahore)',
    avatarBg: '#f3e8ff',
    avatarColor: '#7e22ce',
    pattern: { 1: 'L', 2: 'P', 3: 'P', 4: 'P', 5: 'P', 15: 'LV', 16: 'LV', 17: 'LV' },
    lateChance: 0.03,
  },
  {
    id: 'emp-5',
    name: 'Bilal Ahmed',
    initials: 'BA',
    role: 'Field Electrician',
    empId: 'EMP-1046',
    dept: 'MEP & Execution',
    site: 'Peshawar Industrial',
    avatarBg: '#f1f5f9',
    avatarColor: '#475569',
    pattern: { 1: 'P', 2: 'L', 3: 'P', 4: 'P', 5: 'P', 10: 'L' },
    lateChance: 0.15,
  },
  {
    id: 'emp-6',
    name: 'Kamran Naqvi',
    initials: 'KN',
    role: 'Sales Executive',
    empId: 'EMP-1047',
    dept: 'Sales & CRM',
    site: 'Head Office (Lahore)',
    avatarBg: '#dcfce7',
    avatarColor: '#15803d',
    pattern: { 1: 'P', 2: 'P', 3: 'P', 4: 'P', 5: 'P' },
    lateChance: 0.02,
  },
  {
    id: 'emp-7',
    name: 'Fahad Hussain',
    initials: 'FH',
    role: 'Project Manager',
    empId: 'EMP-1039',
    dept: 'Project Management',
    site: 'Head Office (Lahore)',
    avatarBg: '#dbeafe',
    avatarColor: '#1d4ed8',
    pattern: { 1: 'P', 2: 'L', 3: 'P', 4: 'P', 5: 'P', 22: 'LV' },
    lateChance: 0.04,
  },
  {
    id: 'emp-8',
    name: 'John Doe',
    initials: 'JD',
    role: 'Project Manager',
    empId: 'EMP-1039',
    dept: 'Project Management',
    site: 'Head Office (Lahore)',
    avatarBg: '#f1f5f9',
    avatarColor: '#475569',
    pattern: { 1: 'P', 2: 'P', 3: 'P', 4: 'A', 5: 'P', 18: 'L' },
    lateChance: 0.06,
  },
  {
    id: 'emp-9',
    name: 'Hamza Tariq',
    initials: 'HT',
    role: 'Site Electrician',
    empId: 'EMP-1028',
    dept: 'MEP & Execution',
    site: 'Peshawar Industrial',
    avatarBg: '#e0e7ff',
    avatarColor: '#242b5f',
    pattern: { 1: 'P', 2: 'P', 3: 'P', 4: 'P', 5: 'P', 12: 'A', 25: 'L' },
    lateChance: 0.1,
  },
  {
    id: 'emp-10',
    name: 'Taha Yaseen',
    initials: 'TY',
    role: 'Store Officer',
    empId: 'EMP-1031',
    dept: 'Inventory & Store',
    site: 'Head Office (Lahore)',
    avatarBg: '#fef9c3',
    avatarColor: '#854d0e',
    pattern: { 1: 'L', 2: 'P', 3: 'L', 4: 'L', 5: 'P', 19: 'A' },
    lateChance: 0.08,
  },
  {
    id: 'emp-11',
    name: 'Farhan Malik',
    initials: 'FM',
    role: 'Senior Accountant',
    empId: 'EMP-1048',
    dept: 'Finance & Accounts',
    site: 'Head Office (Lahore)',
    avatarBg: '#d1fae5',
    avatarColor: '#065f46',
    pattern: { 1: 'P', 2: 'P', 3: 'P', 4: 'P', 5: 'P' },
    lateChance: 0.02,
  },
  {
    id: 'emp-12',
    name: 'Babar Siddiqui',
    initials: 'BS',
    role: 'Procurement Manager',
    empId: 'EMP-1049',
    dept: 'Procurement & SCM',
    site: 'Head Office (Lahore)',
    avatarBg: '#f3e8ff',
    avatarColor: '#7e22ce',
    pattern: { 1: 'P', 2: 'P', 3: 'P', 4: 'P', 5: 'P', 11: 'LV' },
    lateChance: 0.03,
  },
];

const INITIAL_DAILY_LOGS: DailyLogItem[] = [
  {
    id: 'log-1',
    empId: 'EMP-1042',
    name: 'Ali Khan',
    initials: 'AK',
    role: 'MEP • HVAC Engineer',
    dept: 'MEP & Execution',
    site: 'Karachi High-Rise',
    subLocation: 'Sector 4',
    shiftSchedule: '09:00 - 18:00 (9h)',
    punchIn: '10:00 AM',
    punchInType: 'Biometric',
    punchOut: 'In-Progress',
    isInProgress: true,
    workHours: '7h 15m',
    overtime: '0.0h',
    variance: '+60m Late',
    varianceType: 'late',
    geofence: 'Inside (20m)',
    geofenceStatus: 'inside',
    exceptionAlert: 'Late Arrival',
    exceptionAlertType: 'orange',
    status: 'Pending',
    avatarBg: '#e8eaf6',
    avatarColor: '#242b5f',
  },
  {
    id: 'log-2',
    empId: 'EMP-1044',
    name: 'Usman Tariq',
    initials: 'UT',
    role: 'Field Maintenance • AC Tech',
    dept: 'Field Maintenance',
    site: 'Islamabad Govt Complex',
    subLocation: 'Zone B Central',
    shiftSchedule: '08:00 - 17:00 (9h)',
    punchIn: '08:00 AM',
    punchInType: 'Biometric',
    punchOut: 'Missing Punch',
    isPunchOutMissing: true,
    workHours: 'Incomplete',
    overtime: '0.0h',
    variance: '-',
    varianceType: 'neutral',
    geofence: 'Inside Site',
    geofenceStatus: 'inside',
    exceptionAlert: 'Missing Check-out',
    exceptionAlertType: 'orange',
    status: 'Pending',
    avatarBg: '#ffedd5',
    avatarColor: '#ea580c',
  },
  {
    id: 'log-3',
    empId: 'EMP-1043',
    name: 'Sara Ahmed',
    initials: 'SA',
    role: 'Sales & CRM • Sales Exec',
    dept: 'Sales & CRM',
    site: 'Head Office (Lahore)',
    subLocation: 'Client Field Visit',
    shiftSchedule: '09:00 - 18:00 (9h)',
    punchIn: '09:05 AM',
    punchInType: 'Mobile App',
    punchOut: 'Field Active',
    isInProgress: true,
    workHours: '8h 10m',
    overtime: '0.0h',
    variance: 'On-Time',
    varianceType: 'ontime',
    geofence: '5.2km Out',
    geofenceStatus: 'outside',
    exceptionAlert: 'Out of Radius',
    exceptionAlertType: 'red',
    status: 'Pending',
    avatarBg: '#dbeafe',
    avatarColor: '#1d4ed8',
  },
  {
    id: 'log-4',
    empId: 'EMP-1046',
    name: 'Bilal Ahmed',
    initials: 'BA',
    role: 'MEP • Field Electrician',
    dept: 'MEP & Execution',
    site: 'Peshawar Industrial',
    subLocation: 'Substation 11kV',
    shiftSchedule: '08:00 - 17:00 (9h)',
    punchIn: '08:15 AM',
    punchInType: 'Biometric',
    punchOut: '07:00 PM',
    workHours: '10h 45m',
    overtime: '+2.0h OT',
    variance: '+15m Grace',
    varianceType: 'grace',
    geofence: 'Inside Site',
    geofenceStatus: 'inside',
    exceptionAlert: 'Overtime Logged',
    exceptionAlertType: 'purple',
    status: 'Review OT',
    avatarBg: '#f1f5f9',
    avatarColor: '#475569',
  },
  {
    id: 'log-5',
    empId: 'EMP-1047',
    name: 'Kamran Naqvi',
    initials: 'KN',
    role: 'Sales & CRM • Executive',
    dept: 'Sales & CRM',
    site: 'Head Office (Lahore)',
    subLocation: 'HQ Floor 3',
    shiftSchedule: '09:00 - 18:00 (9h)',
    punchIn: '08:55 AM',
    punchInType: 'Biometric',
    punchOut: '06:05 PM',
    workHours: '9h 10m',
    overtime: '0.0h',
    variance: 'On-Time',
    varianceType: 'ontime',
    geofence: 'HQ Desk',
    geofenceStatus: 'desk',
    exceptionAlert: 'Normal Punch',
    exceptionAlertType: 'green',
    status: 'Present',
    avatarBg: '#dcfce7',
    avatarColor: '#15803d',
  },
];

export function AttendanceProcessingPage() {
  const [viewMode, setViewMode] = useState<'both' | 'table'>('both');
  const [matrixSearch, setMatrixSearch] = useState('');
  const [matrixDept, setMatrixDept] = useState('All Depts');
  const [dailySearch, setDailySearch] = useState('');
  const [dailySite, setDailySite] = useState('All Sites');
  const [toastMessage, setToastMessage] = useState('');
  const [activeDropdown, setActiveDropdown] = useState<string | null>(null);

  // Modals state
  const [modalType, setModalType] = useState<
    | null
    | 'markAllPresent'
    | 'waiveLates'
    | 'declareHoliday'
    | 'declareHalfDay'
    | 'addOvertime'
    | 'syncAll'
    | 'importLogs'
    | 'dayAction'
    | 'timecard'
  >(null);
  const [selectedDay, setSelectedDay] = useState<number>(7);
  const [selectedEmp, setSelectedEmp] = useState<EmployeeMatrixItem | null>(null);

  // Modal form states
  const [holidayName, setHolidayName] = useState('Eid Milad-un-Nabi');
  const [waiverReason, setWaiverReason] = useState('Severe Road Traffic / Monsoon Rain Delay');
  const [otHours, setOtHours] = useState('2.0');

  // Matrix and Logs state
  const [matrixEmployees, setMatrixEmployees] = useState<EmployeeMatrixItem[]>(DEFAULT_EMPLOYEES);
  const [dailyLogs, setDailyLogs] = useState<DailyLogItem[]>(INITIAL_DAILY_LOGS);

  // Real backend query for directory employees
  const directoryQuery = useHrQuery(
    'employees' + queryString({ page: 1, pageSize: 50 }),
    employeeArchivePageSchema,
    ['hr.employees.read'],
  );

  // Sync real DB employees if available
  useEffect(() => {
    if (directoryQuery.data?.items && directoryQuery.data.items.length > 0) {
      const items = directoryQuery.data.items;
      setMatrixEmployees((prev) => {
        const merged = [...prev];
        items.forEach((item, idx) => {
          if (!merged.some((m) => m.empId === String(item.employeeNumber))) {
            const initials = (item.name || 'EM')
              .split(' ')
              .map((n) => n[0])
              .join('')
              .slice(0, 2)
              .toUpperCase();
            merged.push({
              id: item.id,
              name: item.name || `Employee #${item.employeeNumber}`,
              initials: initials || 'EM',
              role: 'Staff Member',
              empId: String(item.employeeNumber),
              dept: 'Operations',
              site: 'Head Office (Lahore)',
              avatarBg: idx % 2 === 0 ? '#dbeafe' : '#f3e8ff',
              avatarColor: idx % 2 === 0 ? '#1d4ed8' : '#7e22ce',
              pattern: { 1: 'P', 2: 'P', 3: 'P', 4: 'P', 5: 'P', 7: 'P' },
              lateChance: 0.05,
            });
          }
        });
        return merged;
      });
    }
  }, [directoryQuery.data]);

  // Toast auto-dismiss
  useEffect(() => {
    if (toastMessage) {
      const timer = setTimeout(() => setToastMessage(''), 3500);
      return () => clearTimeout(timer);
    }
  }, [toastMessage]);

  // Close dropdown on outside click
  useEffect(() => {
    const handleWindowClick = () => setActiveDropdown(null);
    window.addEventListener('click', handleWindowClick);
    return () => window.removeEventListener('click', handleWindowClick);
  }, []);

  const showToast = (msg: string) => {
    setToastMessage(msg);
  };

  // Off days for September 2026: Saturdays & Sundays
  const offDays = useMemo(() => new Set([6, 7, 13, 14, 20, 21, 27, 28]), []);

  // Filtered matrix employees
  const filteredMatrix = useMemo(() => {
    return matrixEmployees.filter((emp) => {
      const matchSearch =
        !matrixSearch.trim() ||
        emp.name.toLowerCase().includes(matrixSearch.toLowerCase()) ||
        emp.empId.toLowerCase().includes(matrixSearch.toLowerCase()) ||
        emp.role.toLowerCase().includes(matrixSearch.toLowerCase());
      const matchDept = matrixDept === 'All Depts' || emp.dept.includes(matrixDept);
      return matchSearch && matchDept;
    });
  }, [matrixEmployees, matrixSearch, matrixDept]);

  // Filtered daily logs
  const filteredDailyLogs = useMemo(() => {
    return dailyLogs.filter((log) => {
      const matchSearch =
        !dailySearch.trim() ||
        log.name.toLowerCase().includes(dailySearch.toLowerCase()) ||
        log.empId.toLowerCase().includes(dailySearch.toLowerCase()) ||
        log.site.toLowerCase().includes(dailySearch.toLowerCase()) ||
        log.role.toLowerCase().includes(dailySearch.toLowerCase());
      const matchSite =
        dailySite === 'All Sites' ||
        dailySite === 'All Sites (8)' ||
        log.site.toLowerCase().includes(dailySite.toLowerCase());
      return matchSearch && matchSite;
    });
  }, [dailyLogs, dailySearch, dailySite]);

  // KPI calculations
  const presentCount = 142;
  const totalCount = 150;
  const turnoutPercent = ((presentCount / totalCount) * 100).toFixed(1);
  const exceptionCount = dailyLogs.filter(
    (l) => l.status === 'Pending' || l.status === 'Review OT',
  ).length;

  // Actions
  const handleMarkAllPresentConfirm = () => {
    setDailyLogs((prev) =>
      prev.map((log) => ({
        ...log,
        status: 'Present',
        exceptionAlert: 'Normal Punch',
        exceptionAlertType: 'green',
      })),
    );
    setMatrixEmployees((prev) =>
      prev.map((emp) => ({
        ...emp,
        pattern: { ...emp.pattern, [selectedDay]: 'P' },
      })),
    );
    setModalType(null);
    showToast(`All 150 workers marked Present for ${selectedDay} September!`);
  };

  const handleWaiveLatesConfirm = () => {
    setDailyLogs((prev) =>
      prev.map((log) =>
        log.exceptionAlert.includes('Late')
          ? {
              ...log,
              status: 'Waived',
              variance: 'Waived',
              varianceType: 'ontime',
            }
          : log,
      ),
    );
    setMatrixEmployees((prev) =>
      prev.map((emp) => ({
        ...emp,
        pattern: { ...emp.pattern, [selectedDay]: 'P' },
      })),
    );
    setModalType(null);
    showToast(`Late arrival penalties waived for ${selectedDay} September!`);
  };

  const handleDeclareHolidayConfirm = () => {
    setMatrixEmployees((prev) =>
      prev.map((emp) => ({
        ...emp,
        pattern: { ...emp.pattern, [selectedDay]: 'H' },
      })),
    );
    setModalType(null);
    showToast(`Gazetted Holiday applied: ${holidayName} on ${selectedDay} Sep!`);
  };

  const handleDeclareHalfDayConfirm = () => {
    setModalType(null);
    showToast(`Half-Day declared for ${selectedDay} Sep! Shift ends at 02:00 PM.`);
  };

  const handleAddOvertimeConfirm = () => {
    setDailyLogs((prev) =>
      prev.map((log) =>
        log.empId === 'EMP-1046'
          ? {
              ...log,
              overtime: `+${otHours}h OT`,
              status: 'Approved',
            }
          : log,
      ),
    );
    setModalType(null);
    showToast(`+${otHours}h Overtime authorized for site technicians on ${selectedDay} Sep!`);
  };

  const handleSyncAllConfirm = () => {
    setModalType(null);
    void directoryQuery.refetch();
    showToast('Biometric machines synced! 142 records refreshed.');
  };

  const updateLogStatus = (
    id: string,
    status: DailyLogItem['status'],
    alert?: string,
    alertType?: DailyLogItem['exceptionAlertType'],
  ) => {
    setDailyLogs((prev) =>
      prev.map((log) =>
        log.id === id
          ? {
              ...log,
              status,
              ...(alert ? { exceptionAlert: alert } : {}),
              ...(alertType ? { exceptionAlertType: alertType } : {}),
            }
          : log,
      ),
    );
    setActiveDropdown(null);
  };

  return (
    <div className="att-page-container">
      {/* 1. Header Bar */}
      <div className="att-top-header">
        <div className="att-top-title-group">
          <h1 className="att-page-title">2. Attendance &amp; Time-Tracking</h1>
          <span className="att-top-badge">
            <Building2 size={13} />
            Human Resources
          </span>
        </div>
      </div>

      {/* 2. Compact Top Stat Cards */}
      <div className="att-stats-grid">
        {/* Workers Present */}
        <div className="att-stat-card">
          <div className="att-stat-info">
            <span className="att-stat-label">Workers Present</span>
            <div className="att-stat-value">
              {presentCount} <span className="att-stat-sub-val green">/ {totalCount}</span>
            </div>
            <span className="att-stat-trend green">
              <CheckCircle2 size={11} /> {turnoutPercent}% Turnout
            </span>
          </div>
          <div className="att-stat-icon-box icon-bg-green">
            <Users size={16} />
          </div>
        </div>

        {/* Biometric & GPS */}
        <div className="att-stat-card">
          <div className="att-stat-info">
            <span className="att-stat-label">Biometric &amp; GPS</span>
            <div className="att-stat-value">
              8 Sites <span className="att-stat-sub-val blue">Online</span>
            </div>
            <span className="att-stat-trend blue">
              <span className="live-pulse-dot" /> Auto-Sync Live
            </span>
          </div>
          <div className="att-stat-icon-box icon-bg-blue">
            <Radio size={16} />
          </div>
        </div>

        {/* Exceptions Reqd */}
        <div className="att-stat-card border-orange">
          <div className="att-stat-info">
            <span className="att-stat-label text-orange">Exceptions Reqd</span>
            <div className="att-stat-value">
              {exceptionCount} Cases <span className="att-stat-sub-val orange">Unresolved</span>
            </div>
            <span className="att-stat-trend orange">Late &amp; radius alerts</span>
          </div>
          <div className="att-stat-icon-box icon-bg-orange">
            <AlertTriangle size={16} />
          </div>
        </div>

        {/* Overtime Logged */}
        <div className="att-stat-card">
          <div className="att-stat-info">
            <span className="att-stat-label">Overtime Logged</span>
            <div className="att-stat-value">
              46.5 <span className="att-stat-sub-val muted">Hrs</span>
            </div>
            <span className="att-stat-trend muted">This pay period</span>
          </div>
          <div className="att-stat-icon-box icon-bg-purple">
            <Briefcase size={16} />
          </div>
        </div>
      </div>

      {/* 3. Action Toolbar: View Switcher + Export / Import Buttons */}
      <div className="att-action-toolbar">
        {/* Left: View Toggle Tabs */}
        <div className="att-toolbar-left">
          <button
            type="button"
            className={`att-tab-btn ${viewMode === 'both' ? 'active' : ''}`}
            onClick={() => setViewMode('both')}
          >
            <Layers size={13} /> All (Matrix &amp; Log)
          </button>
          <button
            type="button"
            className={`att-tab-btn ${viewMode === 'table' ? 'active' : ''}`}
            onClick={() => setViewMode('table')}
          >
            <List size={13} /> Daily Log Only{' '}
            <span className="badge-tab-red">{exceptionCount}</span>
          </button>
          <Link to="/hr/shifts" className="att-tab-btn" title="Manage Work Shifts & Timings">
            <Clock size={13} style={{ color: '#4f46e5' }} /> Shifts &amp; Master Timings{' '}
            <span className="badge-tab-indigo">4 Active</span>
          </Link>
        </div>

        {/* Right: Excel / PDF / Import Actions */}
        <div className="att-toolbar-right">
          <Link
            to="/hr/shifts"
            className="btn-tool-outline border-indigo"
            title="Configure Shift Rules"
          >
            <Clock size={13} style={{ color: '#4f46e5' }} /> Manage Shifts
          </Link>
          <button
            type="button"
            className="btn-tool-outline hover-green"
            onClick={() => showToast('Attendance register exported to Excel.')}
            title="Export to Excel"
          >
            <FileSpreadsheet size={13} style={{ color: '#16a34a' }} /> Excel
          </button>
          <button
            type="button"
            className="btn-tool-outline hover-red"
            onClick={() => showToast('Attendance report exported to PDF.')}
            title="Export to PDF"
          >
            <FileText size={13} style={{ color: '#ef4444' }} /> PDF
          </button>
          <button
            type="button"
            className="btn-tool-outline hover-navy"
            onClick={() => setModalType('importLogs')}
            title="Import from Biometric Machine"
          >
            <Upload size={13} style={{ color: '#2563eb' }} /> Import Logs
          </button>
          <button
            type="button"
            className="btn-tool-primary"
            onClick={() => setModalType('syncAll')}
          >
            <RefreshCw size={12} /> Sync All
          </button>
        </div>
      </div>

      {/* 4. SECTION 1: EMPLOYEE ATTENDANCE REGISTER MATRIX */}
      {viewMode === 'both' && (
        <div className="att-section-card">
          {/* Header */}
          <div className="att-card-header">
            <div className="att-header-left">
              <div className="att-header-icon-box">
                <Calendar size={14} />
              </div>
              <div className="att-card-title-wrap">
                <div className="att-card-title-row">
                  <h3 className="att-card-title">Employee Attendance Register</h3>
                  <span className="badge-header-green">September 2026</span>
                  <span className="badge-header-blue">{filteredMatrix.length} Workers</span>
                </div>
                <p className="att-card-subtitle">
                  Individual employee attendance matrix &mdash; <strong>P</strong> Present,{' '}
                  <strong>L</strong> Late, <strong>A</strong> Absent, <strong>LV</strong> Leave,{' '}
                  <strong>H</strong> Holiday, <strong>-</strong> Off
                </p>
              </div>
            </div>

            <div className="att-header-controls">
              <button
                type="button"
                className="btn-header-green"
                onClick={() => {
                  setSelectedDay(7);
                  setModalType('markAllPresent');
                }}
              >
                <CheckCheck size={13} /> Mark All Present
              </button>
              <button
                type="button"
                className="btn-header-outline"
                onClick={() => {
                  setSelectedDay(16);
                  setModalType('declareHoliday');
                }}
              >
                <Palmtree size={13} style={{ color: '#2563eb' }} /> Holiday
              </button>
              <button
                type="button"
                className="btn-header-outline"
                onClick={() => {
                  setSelectedDay(7);
                  setModalType('waiveLates');
                }}
              >
                <HeartHandshake size={13} style={{ color: '#ea580c' }} /> Waive Lates
              </button>
              <div className="att-v-divider" />
              <div className="att-search-box">
                <Search size={12} />
                <input
                  type="text"
                  placeholder="Search..."
                  value={matrixSearch}
                  onChange={(e) => setMatrixSearch(e.target.value)}
                />
              </div>
              <select
                className="att-select"
                value={matrixDept}
                onChange={(e) => setMatrixDept(e.target.value)}
              >
                <option value="All Depts">All Depts</option>
                <option value="MEP &amp; Execution">MEP &amp; Execution</option>
                <option value="Sales &amp; CRM">Sales &amp; CRM</option>
                <option value="Field Maintenance">Field Maintenance</option>
                <option value="HR Operations">HR Operations</option>
                <option value="Project Management">Project Management</option>
                <option value="Finance &amp; Accounts">Finance &amp; Accounts</option>
                <option value="Procurement &amp; SCM">Procurement &amp; SCM</option>
              </select>
              <button
                type="button"
                className="btn-header-outline"
                onClick={() => showToast('Monthly Matrix export generated.')}
              >
                <FileSpreadsheet size={13} style={{ color: '#16a34a' }} /> Export
              </button>
            </div>
          </div>

          {/* Table */}
          <div className="att-table-scroll-wrap">
            <table className="att-matrix-table">
              <thead>
                <tr>
                  <th className="col-emp">Employee</th>
                  {Array.from({ length: 30 }, (_, i) => {
                    const day = i + 1;
                    const isToday = day === 7;
                    const isOff = offDays.has(day);
                    return (
                      <th
                        key={day}
                        className={`col-day ${isToday ? 'today' : ''} ${isOff ? 'weekend' : ''}`}
                      >
                        {day}
                      </th>
                    );
                  })}
                  <th className="col-summary green">P</th>
                  <th className="col-summary orange">L</th>
                  <th className="col-summary red">A</th>
                  <th className="col-summary purple">LV</th>
                  <th className="col-summary act">Act</th>
                </tr>
              </thead>
              <tbody>
                {filteredMatrix.map((emp, idx) => {
                  let pCount = 0;
                  let lCount = 0;
                  let aCount = 0;
                  let lvCount = 0;

                  const days = Array.from({ length: 30 }, (_, i) => {
                    const d = i + 1;
                    if (offDays.has(d)) return '-';
                    if (emp.pattern[d]) {
                      const st = emp.pattern[d];
                      if (st === 'P') pCount++;
                      else if (st === 'L') lCount++;
                      else if (st === 'A') aCount++;
                      else if (st === 'LV') lvCount++;
                      return st;
                    }
                    if (d > 7) return '';
                    const seed = (idx * 31 + d * 7) % 100;
                    if (seed < (emp.lateChance || 0.05) * 100) {
                      lCount++;
                      return 'L';
                    }
                    pCount++;
                    return 'P';
                  });

                  return (
                    <tr key={emp.id}>
                      <td className="col-emp">
                        <div className="att-emp-cell">
                          <div
                            className="att-emp-avatar"
                            style={{ backgroundColor: emp.avatarBg, color: emp.avatarColor }}
                          >
                            {emp.initials}
                          </div>
                          <div className="att-emp-text">
                            <span
                              className="att-emp-name"
                              onClick={() => {
                                setSelectedEmp(emp);
                                setModalType('timecard');
                              }}
                            >
                              {emp.name}
                            </span>
                            <span className="att-emp-meta">
                              {emp.empId} &bull; {emp.role}
                            </span>
                          </div>
                        </div>
                      </td>

                      {days.map((status, i) => {
                        const d = i + 1;
                        const isToday = d === 7;
                        const isOff = offDays.has(d);
                        const isFuture = d > 7 && status === '';

                        if (isFuture) {
                          return (
                            <td key={d} className={isOff ? 'td-weekend' : ''}>
                              <span className="cell-dash">&mdash;</span>
                            </td>
                          );
                        }

                        let badgeClass = 'cell-p';
                        if (status === 'L') badgeClass = 'cell-l';
                        else if (status === 'A') badgeClass = 'cell-a';
                        else if (status === 'LV') badgeClass = 'cell-lv';
                        else if (status === 'H') badgeClass = 'cell-h';
                        else if (status === '-') badgeClass = 'cell-off';

                        return (
                          <td
                            key={d}
                            className={`${isToday ? 'td-today' : ''} ${isOff ? 'td-weekend' : ''}`}
                          >
                            <button
                              type="button"
                              className={`matrix-cell-btn ${badgeClass}`}
                              title={`${emp.name}: Day ${d} - ${status || 'Off'}`}
                              onClick={() => {
                                setSelectedDay(d);
                                setSelectedEmp(emp);
                                setModalType('dayAction');
                              }}
                            >
                              {status || '-'}
                            </button>
                          </td>
                        );
                      })}

                      <td className="cell-summary-val green">{pCount}</td>
                      <td className="cell-summary-val orange">{lCount}</td>
                      <td className="cell-summary-val red">{aCount}</td>
                      <td className="cell-summary-val purple">{lvCount}</td>
                      <td>
                        <div
                          className="att-dropdown"
                          onClick={(e) => {
                            e.stopPropagation();
                            setActiveDropdown(activeDropdown === emp.id ? null : emp.id);
                          }}
                        >
                          <button type="button" className="att-dropdown-trigger">
                            <MoreVertical size={13} />
                          </button>
                          {activeDropdown === emp.id && (
                            <div className="att-dropdown-menu">
                              <div className="att-dropdown-header">
                                <p>{emp.name}</p>
                              </div>
                              <button
                                type="button"
                                className="att-dropdown-item purple"
                                onClick={() => {
                                  setSelectedEmp(emp);
                                  setModalType('timecard');
                                }}
                              >
                                <Clock size={12} /> View 30-Day Timecard
                              </button>
                              <button
                                type="button"
                                className="att-dropdown-item red"
                                onClick={() => {
                                  setMatrixEmployees((prev) =>
                                    prev.map((e) =>
                                      e.id === emp.id
                                        ? { ...e, pattern: { ...e.pattern, 7: 'A' } }
                                        : e,
                                    ),
                                  );
                                  showToast(`${emp.name} marked absent for today.`);
                                }}
                              >
                                <XCircle size={12} /> Mark Absent Today
                              </button>
                              <button
                                type="button"
                                className="att-dropdown-item green"
                                onClick={() => {
                                  setMatrixEmployees((prev) =>
                                    prev.map((e) =>
                                      e.id === emp.id
                                        ? {
                                            ...e,
                                            pattern: { ...e.pattern, 7: 'P', 1: 'P', 3: 'P' },
                                          }
                                        : e,
                                    ),
                                  );
                                  showToast(`Lates waived for ${emp.name} this month.`);
                                }}
                              >
                                <HeartHandshake size={12} /> Waive Month Lates
                              </button>
                              <button
                                type="button"
                                className="att-dropdown-item blue"
                                onClick={() =>
                                  showToast(`Attendance report downloaded for ${emp.name}.`)
                                }
                              >
                                <Download size={12} /> Download Report
                              </button>
                              <div className="att-dropdown-divider" />
                              <Link to={`/hr/employees/${emp.id}`} className="att-dropdown-item">
                                <ExternalLink size={12} /> View Profile
                              </Link>
                              <button
                                type="button"
                                className="att-dropdown-item green"
                                onClick={() =>
                                  showToast(`Salary invoice generated for ${emp.name}.`)
                                }
                              >
                                <DollarSign size={12} /> Salary Invoice
                              </button>
                            </div>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}

                {/* Footer summary row */}
                <tr className="att-footer-row">
                  <td className="col-emp">
                    <span className="att-footer-title">Total / Org Summary</span>
                  </td>
                  {Array.from({ length: 30 }, (_, i) => {
                    const d = i + 1;
                    if (offDays.has(d)) {
                      return (
                        <td key={d}>
                          <span className="footer-day-off">OFF</span>
                        </td>
                      );
                    }
                    if (d > 7) {
                      return (
                        <td key={d}>
                          <span className="cell-dash">&mdash;</span>
                        </td>
                      );
                    }
                    const counts = [142, 143, 141, 142, 144, 0, 145];
                    const val = counts[d - 1] || 142;
                    return (
                      <td key={d}>
                        <span className="footer-day-count">{val}</span>
                      </td>
                    );
                  })}
                  <td className="cell-summary-val green">95%</td>
                  <td className="cell-summary-val orange">3%</td>
                  <td className="cell-summary-val red">1.5%</td>
                  <td className="cell-summary-val purple">0.5%</td>
                  <td />
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* 5. SECTION 2: LIVE EXCEPTIONS & DAILY LOG */}
      <div className="att-section-card">
        {/* Header */}
        <div className="att-card-header">
          <div className="att-header-left">
            <span className="live-pulse-dot-red" />
            <h3 className="att-card-title">
              Live Exceptions &amp; Daily Log (07 Sep, 2026 &ndash; Today)
            </h3>
          </div>
          <div className="att-header-controls">
            <div className="att-search-box wide">
              <Search size={12} />
              <input
                type="text"
                placeholder="Filter employee or site..."
                value={dailySearch}
                onChange={(e) => setDailySearch(e.target.value)}
              />
            </div>
            <select
              className="att-select"
              value={dailySite}
              onChange={(e) => setDailySite(e.target.value)}
            >
              <option value="All Sites">All Sites (8)</option>
              <option value="Karachi">Karachi High-Rise</option>
              <option value="Islamabad">Islamabad Govt Complex</option>
              <option value="Lahore">Head Office (Lahore)</option>
              <option value="Peshawar">Peshawar Industrial</option>
            </select>
          </div>
        </div>

        {/* Daily Log Table */}
        <div className="att-table-scroll-wrap" style={{ minHeight: '260px' }}>
          <table className="att-daily-table">
            <thead>
              <tr>
                <th>Emp ID</th>
                <th>Employee</th>
                <th>Department / Role</th>
                <th>Project Site</th>
                <th>Shift Schedule</th>
                <th>Punch-In</th>
                <th>Punch-Out</th>
                <th>Work Hours</th>
                <th style={{ color: '#1d4ed8' }}>Overtime</th>
                <th>Variance</th>
                <th>GPS Geofence</th>
                <th>Exception Alert</th>
                <th style={{ textAlign: 'center' }}>Status</th>
                <th style={{ textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredDailyLogs.map((log) => (
                <tr key={log.id}>
                  {/* Emp ID */}
                  <td>
                    <span className="emp-id-badge">{log.empId}</span>
                  </td>

                  {/* Employee */}
                  <td>
                    <div className="att-emp-cell">
                      <div
                        className="att-emp-avatar"
                        style={{ backgroundColor: log.avatarBg, color: log.avatarColor }}
                      >
                        {log.initials}
                      </div>
                      <span
                        className="att-emp-name"
                        onClick={() => {
                          const mEmp = matrixEmployees.find((m) => m.empId === log.empId);
                          if (mEmp) setSelectedEmp(mEmp);
                          setModalType('timecard');
                        }}
                      >
                        {log.name}
                      </span>
                    </div>
                  </td>

                  {/* Department / Role */}
                  <td style={{ color: '#475569', fontWeight: 500 }}>{log.role}</td>

                  {/* Project Site */}
                  <td>
                    <span style={{ fontWeight: 600, color: '#334155', display: 'block' }}>
                      {log.site}
                    </span>
                    <span style={{ fontSize: '8.5px', color: '#94a3b8', display: 'block' }}>
                      {log.subLocation}
                    </span>
                  </td>

                  {/* Shift Schedule */}
                  <td style={{ color: '#64748b' }}>{log.shiftSchedule}</td>

                  {/* Punch-In */}
                  <td>
                    <span
                      style={{
                        fontWeight: 700,
                        color:
                          log.varianceType === 'late'
                            ? '#dc2626'
                            : log.varianceType === 'ontime'
                              ? '#15803d'
                              : '#1e293b',
                        display: 'block',
                      }}
                    >
                      {log.punchIn}
                    </span>
                    <span
                      style={{
                        fontSize: '8.5px',
                        color: '#64748b',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '3px',
                      }}
                    >
                      {log.punchInType === 'Biometric' ? (
                        <>
                          <Fingerprint size={10} style={{ color: '#2563eb' }} /> Biometric
                        </>
                      ) : (
                        <>
                          <Smartphone size={10} style={{ color: '#2563eb' }} /> Mobile App
                        </>
                      )}
                    </span>
                  </td>

                  {/* Punch-Out */}
                  <td>
                    {log.isPunchOutMissing ? (
                      <span
                        style={{
                          color: '#dc2626',
                          fontWeight: 700,
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '4px',
                        }}
                      >
                        <XCircle size={11} /> Missing Punch
                      </span>
                    ) : log.isInProgress ? (
                      <span style={{ color: '#94a3b8', fontStyle: 'italic' }}>{log.punchOut}</span>
                    ) : (
                      <span style={{ fontWeight: 700, color: '#1d4ed8' }}>{log.punchOut}</span>
                    )}
                  </td>

                  {/* Work Hours */}
                  <td
                    style={{
                      fontWeight: 600,
                      color: log.isPunchOutMissing ? '#94a3b8' : '#1e293b',
                    }}
                  >
                    {log.workHours}
                  </td>

                  {/* Overtime */}
                  <td>
                    {log.overtime !== '0.0h' ? (
                      <span className="badge-overtime">{log.overtime}</span>
                    ) : (
                      <span style={{ color: '#94a3b8', fontWeight: 500 }}>0.0h</span>
                    )}
                  </td>

                  {/* Variance */}
                  <td>
                    {log.variance === '-' ? (
                      <span style={{ color: '#cbd5e1' }}>-</span>
                    ) : (
                      <span className={`badge-variance ${log.varianceType}`}>{log.variance}</span>
                    )}
                  </td>

                  {/* GPS Geofence */}
                  <td>
                    {log.geofenceStatus === 'outside' ? (
                      <span
                        style={{
                          color: '#dc2626',
                          fontWeight: 700,
                          fontSize: '9px',
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '3px',
                        }}
                      >
                        <MapPin size={10} /> {log.geofence}
                      </span>
                    ) : (
                      <span
                        style={{
                          color: '#16a34a',
                          fontWeight: 700,
                          fontSize: '9px',
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '3px',
                        }}
                      >
                        <CheckCircle2 size={10} /> {log.geofence}
                      </span>
                    )}
                  </td>

                  {/* Exception Alert */}
                  <td>
                    <span className={`badge-exception-alert ${log.exceptionAlertType}`}>
                      {log.exceptionAlert}
                    </span>
                  </td>

                  {/* Status */}
                  <td style={{ textAlign: 'center' }}>
                    <span
                      className={`badge-status ${
                        log.status === 'Pending'
                          ? 'pending'
                          : log.status === 'Waived'
                            ? 'waived'
                            : log.status === 'Resolved'
                              ? 'resolved'
                              : log.status === 'Approved'
                                ? 'approved'
                                : log.status === 'Review OT'
                                  ? 'review-ot'
                                  : 'present'
                      }`}
                    >
                      {log.status}
                    </span>
                  </td>

                  {/* Actions Dropdown */}
                  <td style={{ textAlign: 'right' }}>
                    <div
                      className="att-dropdown"
                      onClick={(e) => {
                        e.stopPropagation();
                        setActiveDropdown(activeDropdown === log.id ? null : log.id);
                      }}
                    >
                      <button type="button" className="att-dropdown-trigger">
                        <MoreVertical size={13} />
                      </button>
                      {activeDropdown === log.id && (
                        <div className="att-dropdown-menu">
                          <div className="att-dropdown-header">
                            <p>
                              {log.name} &mdash; {log.exceptionAlert}
                            </p>
                          </div>

                          {log.exceptionAlert === 'Late Arrival' && (
                            <>
                              <button
                                type="button"
                                className="att-dropdown-item green"
                                onClick={() => {
                                  updateLogStatus(log.id, 'Waived');
                                  showToast(`Late waived for ${log.name}!`);
                                }}
                              >
                                <HeartHandshake size={12} /> Waive Late
                              </button>
                              <button
                                type="button"
                                className="att-dropdown-item red"
                                onClick={() => {
                                  updateLogStatus(log.id, 'Rejected');
                                  showToast(`Half-day deduction recorded for ${log.name}.`);
                                }}
                              >
                                <XCircle size={12} /> Deduct Late
                              </button>
                            </>
                          )}

                          {log.exceptionAlert === 'Missing Check-out' && (
                            <>
                              <button
                                type="button"
                                className="att-dropdown-item blue"
                                onClick={() => {
                                  setDailyLogs((prev) =>
                                    prev.map((l) =>
                                      l.id === log.id
                                        ? {
                                            ...l,
                                            punchOut: '05:00 PM',
                                            workHours: '9h 00m',
                                            isPunchOutMissing: false,
                                            status: 'Resolved',
                                            exceptionAlert: 'Normal Punch',
                                            exceptionAlertType: 'green',
                                          }
                                        : l,
                                    ),
                                  );
                                  showToast(`Check-out recorded at 05:00 PM for ${log.name}.`);
                                }}
                              >
                                <Clock size={12} /> Set Check-out Time (5:00 PM)
                              </button>
                              <button
                                type="button"
                                className="att-dropdown-item orange"
                                onClick={() => {
                                  updateLogStatus(log.id, 'Resolved');
                                  showToast(`Half-day recorded for ${log.name}.`);
                                }}
                              >
                                <AlertTriangle size={12} /> Mark &frac12; Day
                              </button>
                            </>
                          )}

                          {log.exceptionAlert === 'Out of Radius' && (
                            <>
                              <button
                                type="button"
                                className="att-dropdown-item green"
                                onClick={() => {
                                  updateLogStatus(log.id, 'Approved');
                                  showToast(`Field visit location approved for ${log.name}.`);
                                }}
                              >
                                <Check size={12} /> Approve Location
                              </button>
                              <button
                                type="button"
                                className="att-dropdown-item red"
                                onClick={() => {
                                  updateLogStatus(log.id, 'Rejected');
                                  showToast(`Punch rejected as outside perimeter for ${log.name}.`);
                                }}
                              >
                                <X size={12} /> Reject Punch
                              </button>
                            </>
                          )}

                          {log.exceptionAlert === 'Overtime Logged' && (
                            <>
                              <button
                                type="button"
                                className="att-dropdown-item green"
                                onClick={() => {
                                  updateLogStatus(log.id, 'Approved');
                                  showToast(`Overtime approved (+2.0h) for ${log.name}!`);
                                }}
                              >
                                <CheckCircle2 size={12} /> Approve OT (+2h)
                              </button>
                              <button
                                type="button"
                                className="att-dropdown-item red"
                                onClick={() => {
                                  updateLogStatus(log.id, 'Rejected');
                                  showToast(`Overtime rejected for ${log.name}.`);
                                }}
                              >
                                <XCircle size={12} /> Reject Overtime
                              </button>
                            </>
                          )}

                          {log.exceptionAlert === 'Normal Punch' && (
                            <button
                              type="button"
                              className="att-dropdown-item purple"
                              onClick={() => {
                                setDailyLogs((prev) =>
                                  prev.map((l) =>
                                    l.id === log.id
                                      ? {
                                          ...l,
                                          overtime: '+1.5h OT',
                                          exceptionAlert: 'Overtime Logged',
                                          exceptionAlertType: 'purple',
                                          status: 'Review OT',
                                        }
                                      : l,
                                  ),
                                );
                                showToast(`+1.5h Overtime added for ${log.name}.`);
                              }}
                            >
                              <Briefcase size={12} /> Add Overtime
                            </button>
                          )}

                          <button
                            type="button"
                            className="att-dropdown-item purple"
                            onClick={() => {
                              const mEmp = matrixEmployees.find((m) => m.empId === log.empId);
                              if (mEmp) setSelectedEmp(mEmp);
                              setModalType('timecard');
                            }}
                          >
                            <Clock size={12} /> View History (30 Days)
                          </button>
                          <div className="att-dropdown-divider" />
                          <Link to="/hr/employees" className="att-dropdown-item">
                            <ExternalLink size={12} /> View Employee Profile
                          </Link>
                        </div>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* 6. Bottom Shortcut Bar */}
      <div className="att-bottom-bar">
        <div className="att-bottom-left">
          <Info size={14} style={{ color: '#242b5f' }} />
          <span>
            Click on any date tile in the <strong>Attendance Calendar</strong> above to execute{' '}
            <strong>Direct Actions</strong> (Mark Present, Waive Lates, Add Overtime, Declare
            Holiday).
          </span>
        </div>
        <div className="att-bottom-right">
          <button
            type="button"
            className="att-link-btn"
            onClick={() => showToast('30-Day Master Matrix downloaded successfully.')}
          >
            <FileSpreadsheet size={13} style={{ color: '#16a34a' }} /> Download 30-Day Master Matrix
          </button>
        </div>
      </div>

      {/* ================= MODALS ================= */}

      {/* MODAL 1: Mark All Present */}
      {modalType === 'markAllPresent' && (
        <div className="att-modal-backdrop" onClick={() => setModalType(null)}>
          <div className="att-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="att-modal-header">
              <h4 className="att-modal-title">
                Confirm Mark All Present &mdash; {selectedDay} Sep 2026
              </h4>
              <button
                type="button"
                className="att-modal-close-btn"
                onClick={() => setModalType(null)}
              >
                <X size={15} />
              </button>
            </div>
            <div className="att-modal-body">
              <div
                style={{
                  background: '#f0fdf4',
                  border: '1px solid #bbf7d0',
                  borderRadius: '8px',
                  padding: '10px',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                }}
              >
                <div>
                  <p style={{ fontWeight: 700, fontSize: '12px', color: '#166534', margin: 0 }}>
                    Bulk Attendance Clear: {selectedDay} Sep 2026
                  </p>
                  <p style={{ fontSize: '10px', color: '#15803d', margin: '2px 0 0 0' }}>
                    Active Organization: 8 Sites &bull; 150 Workers
                  </p>
                </div>
                <span
                  style={{
                    background: '#16a34a',
                    color: '#ffffff',
                    fontSize: '9.5px',
                    fontWeight: 700,
                    padding: '2px 8px',
                    borderRadius: '4px',
                  }}
                >
                  100% Present
                </span>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <div>
                  <label
                    style={{
                      display: 'block',
                      fontSize: '10px',
                      fontWeight: 700,
                      color: '#334155',
                      marginBottom: '4px',
                    }}
                  >
                    Standard Punch-In Time
                  </label>
                  <input
                    type="text"
                    defaultValue="09:00 AM"
                    style={{
                      width: '100%',
                      border: '1px solid #cbd5e1',
                      borderRadius: '6px',
                      padding: '6px 10px',
                      fontSize: '12px',
                    }}
                  />
                </div>
                <div>
                  <label
                    style={{
                      display: 'block',
                      fontSize: '10px',
                      fontWeight: 700,
                      color: '#334155',
                      marginBottom: '4px',
                    }}
                  >
                    Standard Punch-Out Time
                  </label>
                  <input
                    type="text"
                    defaultValue="06:00 PM"
                    style={{
                      width: '100%',
                      border: '1px solid #cbd5e1',
                      borderRadius: '6px',
                      padding: '6px 10px',
                      fontSize: '12px',
                    }}
                  />
                </div>
              </div>

              <div
                style={{
                  background: '#f8fafc',
                  border: '1px solid #e2e8f0',
                  borderRadius: '6px',
                  padding: '10px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '6px',
                  fontSize: '11px',
                }}
              >
                <label
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}
                >
                  <input type="checkbox" defaultChecked />
                  <span>Auto-resolve missing check-ins and late arrival flags</span>
                </label>
                <label
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}
                >
                  <input type="checkbox" defaultChecked />
                  <span>Sync with payroll engine (Full daily wage credited)</span>
                </label>
              </div>
            </div>
            <div className="att-modal-footer">
              <button type="button" className="btn-modal-cancel" onClick={() => setModalType(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn-modal-confirm green"
                onClick={handleMarkAllPresentConfirm}
              >
                Confirm Mark All Present
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: Waive Lates */}
      {modalType === 'waiveLates' && (
        <div className="att-modal-backdrop" onClick={() => setModalType(null)}>
          <div className="att-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="att-modal-header">
              <h4 className="att-modal-title">
                Waive Late Penalties &mdash; {selectedDay} Sep 2026
              </h4>
              <button
                type="button"
                className="att-modal-close-btn"
                onClick={() => setModalType(null)}
              >
                <X size={15} />
              </button>
            </div>
            <div className="att-modal-body">
              <div
                style={{
                  background: '#eff6ff',
                  border: '1px solid #bfdbfe',
                  borderRadius: '8px',
                  padding: '10px',
                }}
              >
                <p style={{ fontWeight: 700, fontSize: '12px', color: '#1e40af', margin: 0 }}>
                  Waive Exceptions for {selectedDay} Sep 2026
                </p>
                <p style={{ fontSize: '10px', color: '#64748b', margin: '2px 0 0 0' }}>
                  Pending Cases: 3 (Ali Khan +60m, Usman Tariq missing punch, Sara Ahmed radius
                  alert)
                </p>
              </div>

              <div>
                <label
                  style={{
                    display: 'block',
                    fontSize: '10px',
                    fontWeight: 700,
                    color: '#334155',
                    marginBottom: '4px',
                  }}
                >
                  Reason for HR Waiver
                </label>
                <select
                  value={waiverReason}
                  onChange={(e) => setWaiverReason(e.target.value)}
                  style={{
                    width: '100%',
                    border: '1px solid #cbd5e1',
                    borderRadius: '6px',
                    padding: '6px 10px',
                    fontSize: '12px',
                  }}
                >
                  <option>Severe Road Traffic / Monsoon Rain Delay</option>
                  <option>Approved Direct Client Emergency Dispatch</option>
                  <option>Biometric Machine Time Synchronization Lag</option>
                  <option>CEO / Executive Management Special Approval</option>
                </select>
              </div>

              <div>
                <label
                  style={{
                    display: 'block',
                    fontSize: '10px',
                    fontWeight: 700,
                    color: '#334155',
                    marginBottom: '4px',
                  }}
                >
                  HR Officer Remarks
                </label>
                <input
                  type="text"
                  defaultValue="Approved grace waiver for transit delay"
                  style={{
                    width: '100%',
                    border: '1px solid #cbd5e1',
                    borderRadius: '6px',
                    padding: '6px 10px',
                    fontSize: '12px',
                  }}
                />
              </div>

              <div
                style={{
                  background: '#f0fdf4',
                  border: '1px solid #bbf7d0',
                  borderRadius: '6px',
                  padding: '8px 10px',
                  fontSize: '10.5px',
                  color: '#15803d',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                }}
              >
                <CheckCircle2 size={13} /> Penalties reversed. Full 9h shift will be counted in
                payroll.
              </div>
            </div>
            <div className="att-modal-footer">
              <button type="button" className="btn-modal-cancel" onClick={() => setModalType(null)}>
                Cancel
              </button>
              <button type="button" className="btn-modal-confirm" onClick={handleWaiveLatesConfirm}>
                Waive All Lates
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 3: Declare Holiday */}
      {modalType === 'declareHoliday' && (
        <div className="att-modal-backdrop" onClick={() => setModalType(null)}>
          <div className="att-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="att-modal-header">
              <h4 className="att-modal-title">
                Declare Gazetted Holiday &mdash; {selectedDay} Sep 2026
              </h4>
              <button
                type="button"
                className="att-modal-close-btn"
                onClick={() => setModalType(null)}
              >
                <X size={15} />
              </button>
            </div>
            <div className="att-modal-body">
              <div>
                <label
                  style={{
                    display: 'block',
                    fontSize: '10px',
                    fontWeight: 700,
                    color: '#334155',
                    marginBottom: '4px',
                  }}
                >
                  Date
                </label>
                <input
                  type="text"
                  value={`${selectedDay} September 2026`}
                  readOnly
                  style={{
                    width: '100%',
                    background: '#f8fafc',
                    border: '1px solid #e2e8f0',
                    borderRadius: '6px',
                    padding: '6px 10px',
                    fontSize: '12px',
                    fontWeight: 700,
                  }}
                />
              </div>

              <div>
                <label
                  style={{
                    display: 'block',
                    fontSize: '10px',
                    fontWeight: 700,
                    color: '#334155',
                    marginBottom: '4px',
                  }}
                >
                  Holiday Title / Occasion
                </label>
                <input
                  type="text"
                  value={holidayName}
                  onChange={(e) => setHolidayName(e.target.value)}
                  placeholder="e.g. Eid Milad-un-Nabi"
                  style={{
                    width: '100%',
                    border: '1px solid #cbd5e1',
                    borderRadius: '6px',
                    padding: '6px 10px',
                    fontSize: '12px',
                  }}
                />
              </div>

              <div>
                <label
                  style={{
                    display: 'block',
                    fontSize: '10px',
                    fontWeight: 700,
                    color: '#334155',
                    marginBottom: '4px',
                  }}
                >
                  Applicable Scope
                </label>
                <select
                  style={{
                    width: '100%',
                    border: '1px solid #cbd5e1',
                    borderRadius: '6px',
                    padding: '6px 10px',
                    fontSize: '12px',
                  }}
                >
                  <option>Full Organization (All 8 Sites &amp; HQ Staff)</option>
                  <option>Site Technicians Only</option>
                  <option>Lahore Head Office Only</option>
                </select>
              </div>

              <div
                style={{
                  background: '#faf5ff',
                  border: '1px solid #e9d5ff',
                  borderRadius: '6px',
                  padding: '8px 10px',
                  fontSize: '10.5px',
                  color: '#7e22ce',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                }}
              >
                <Palmtree size={13} /> Matrix will mark all workers with Paid Gazetted Holiday
                status.
              </div>
            </div>
            <div className="att-modal-footer">
              <button type="button" className="btn-modal-cancel" onClick={() => setModalType(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn-modal-confirm"
                onClick={handleDeclareHolidayConfirm}
              >
                Declare Holiday
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 4: Day Action Picker */}
      {modalType === 'dayAction' && (
        <div className="att-modal-backdrop" onClick={() => setModalType(null)}>
          <div className="att-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="att-modal-header">
              <h4 className="att-modal-title">
                Direct Attendance Actions: Day {selectedDay} Sep 2026
              </h4>
              <button
                type="button"
                className="att-modal-close-btn"
                onClick={() => setModalType(null)}
              >
                <X size={15} />
              </button>
            </div>
            <div className="att-modal-body">
              <div
                style={{
                  background: '#f8fafc',
                  border: '1px solid #e2e8f0',
                  borderRadius: '8px',
                  padding: '10px',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <div
                    style={{
                      width: '32px',
                      height: '32px',
                      borderRadius: '8px',
                      background: '#242b5f',
                      color: '#ffffff',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontWeight: 800,
                      fontSize: '13px',
                    }}
                  >
                    {selectedDay}
                  </div>
                  <div>
                    <p style={{ fontWeight: 700, fontSize: '12px', margin: 0, color: '#1e293b' }}>
                      {selectedDay} September 2026
                    </p>
                    <p style={{ fontSize: '10px', color: '#64748b', margin: 0 }}>
                      {selectedEmp
                        ? `Selected: ${selectedEmp.name} (${selectedEmp.empId})`
                        : 'All 8 Sites &bull; 150 Workers'}
                    </p>
                  </div>
                </div>
                <span
                  style={{
                    background: '#eff6ff',
                    color: '#242b5f',
                    fontSize: '9.5px',
                    fontWeight: 700,
                    padding: '2px 8px',
                    borderRadius: '4px',
                    border: '1px solid #bfdbfe',
                  }}
                >
                  Active Register
                </span>
              </div>

              <p
                style={{
                  fontSize: '10px',
                  fontWeight: 700,
                  color: '#94a3b8',
                  textTransform: 'uppercase',
                  letterSpacing: '0.05em',
                  margin: '4px 0',
                }}
              >
                Choose Direct Action:
              </p>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                <button
                  type="button"
                  style={{
                    padding: '10px',
                    border: '1px solid #e2e8f0',
                    borderRadius: '8px',
                    background: '#ffffff',
                    textAlign: 'left',
                    cursor: 'pointer',
                  }}
                  onClick={() => setModalType('markAllPresent')}
                >
                  <p
                    style={{
                      fontSize: '11.5px',
                      fontWeight: 700,
                      color: '#15803d',
                      margin: 0,
                      display: 'flex',
                      alignItems: 'center',
                      gap: '5px',
                    }}
                  >
                    <CheckCheck size={13} /> Mark All Present
                  </p>
                  <p style={{ fontSize: '9.5px', color: '#94a3b8', margin: '2px 0 0 0' }}>
                    150/150 with 9h credit
                  </p>
                </button>

                <button
                  type="button"
                  style={{
                    padding: '10px',
                    border: '1px solid #e2e8f0',
                    borderRadius: '8px',
                    background: '#ffffff',
                    textAlign: 'left',
                    cursor: 'pointer',
                  }}
                  onClick={() => setModalType('waiveLates')}
                >
                  <p
                    style={{
                      fontSize: '11.5px',
                      fontWeight: 700,
                      color: '#2563eb',
                      margin: 0,
                      display: 'flex',
                      alignItems: 'center',
                      gap: '5px',
                    }}
                  >
                    <HeartHandshake size={13} /> Waive All Lates
                  </p>
                  <p style={{ fontSize: '9.5px', color: '#94a3b8', margin: '2px 0 0 0' }}>
                    Forgive late minutes
                  </p>
                </button>

                <button
                  type="button"
                  style={{
                    padding: '10px',
                    border: '1px solid #e2e8f0',
                    borderRadius: '8px',
                    background: '#ffffff',
                    textAlign: 'left',
                    cursor: 'pointer',
                  }}
                  onClick={() => setModalType('declareHoliday')}
                >
                  <p
                    style={{
                      fontSize: '11.5px',
                      fontWeight: 700,
                      color: '#7e22ce',
                      margin: 0,
                      display: 'flex',
                      alignItems: 'center',
                      gap: '5px',
                    }}
                  >
                    <Palmtree size={13} /> Declare Holiday
                  </p>
                  <p style={{ fontSize: '9.5px', color: '#94a3b8', margin: '2px 0 0 0' }}>
                    Paid Gazetted public off
                  </p>
                </button>

                <button
                  type="button"
                  style={{
                    padding: '10px',
                    border: '1px solid #e2e8f0',
                    borderRadius: '8px',
                    background: '#ffffff',
                    textAlign: 'left',
                    cursor: 'pointer',
                  }}
                  onClick={handleDeclareHalfDayConfirm}
                >
                  <p
                    style={{
                      fontSize: '11.5px',
                      fontWeight: 700,
                      color: '#ea580c',
                      margin: 0,
                      display: 'flex',
                      alignItems: 'center',
                      gap: '5px',
                    }}
                  >
                    <Clock size={13} /> Declare Half-Day
                  </p>
                  <p style={{ fontSize: '9.5px', color: '#94a3b8', margin: '2px 0 0 0' }}>
                    Shift closes at 2:00 PM
                  </p>
                </button>

                <button
                  type="button"
                  style={{
                    padding: '10px',
                    border: '1px solid #e2e8f0',
                    borderRadius: '8px',
                    background: '#ffffff',
                    textAlign: 'left',
                    cursor: 'pointer',
                  }}
                  onClick={() => setModalType('addOvertime')}
                >
                  <p
                    style={{
                      fontSize: '11.5px',
                      fontWeight: 700,
                      color: '#4f46e5',
                      margin: 0,
                      display: 'flex',
                      alignItems: 'center',
                      gap: '5px',
                    }}
                  >
                    <Briefcase size={13} /> Add Overtime (+2h)
                  </p>
                  <p style={{ fontSize: '9.5px', color: '#94a3b8', margin: '2px 0 0 0' }}>
                    Authorize site technician OT
                  </p>
                </button>

                <button
                  type="button"
                  style={{
                    padding: '10px',
                    border: '1px solid #e2e8f0',
                    borderRadius: '8px',
                    background: '#ffffff',
                    textAlign: 'left',
                    cursor: 'pointer',
                  }}
                  onClick={() => {
                    setModalType(null);
                    showToast(`Loaded daily punch records for ${selectedDay} September 2026.`);
                  }}
                >
                  <p
                    style={{
                      fontSize: '11.5px',
                      fontWeight: 700,
                      color: '#475569',
                      margin: 0,
                      display: 'flex',
                      alignItems: 'center',
                      gap: '5px',
                    }}
                  >
                    <List size={13} /> View Date Punch Log
                  </p>
                  <p style={{ fontSize: '9.5px', color: '#94a3b8', margin: '2px 0 0 0' }}>
                    Scroll to daily table
                  </p>
                </button>
              </div>
            </div>
            <div className="att-modal-footer">
              <button type="button" className="btn-modal-cancel" onClick={() => setModalType(null)}>
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 5: Add Overtime */}
      {modalType === 'addOvertime' && (
        <div className="att-modal-backdrop" onClick={() => setModalType(null)}>
          <div className="att-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="att-modal-header">
              <h4 className="att-modal-title">
                Authorize Bulk Overtime &mdash; {selectedDay} Sep 2026
              </h4>
              <button
                type="button"
                className="att-modal-close-btn"
                onClick={() => setModalType(null)}
              >
                <X size={15} />
              </button>
            </div>
            <div className="att-modal-body">
              <div>
                <label
                  style={{
                    display: 'block',
                    fontSize: '10px',
                    fontWeight: 700,
                    color: '#334155',
                    marginBottom: '4px',
                  }}
                >
                  Date
                </label>
                <input
                  type="text"
                  value={`${selectedDay} September 2026`}
                  readOnly
                  style={{
                    width: '100%',
                    background: '#f8fafc',
                    border: '1px solid #e2e8f0',
                    borderRadius: '6px',
                    padding: '6px 10px',
                    fontSize: '12px',
                    fontWeight: 700,
                  }}
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <div>
                  <label
                    style={{
                      display: 'block',
                      fontSize: '10px',
                      fontWeight: 700,
                      color: '#334155',
                      marginBottom: '4px',
                    }}
                  >
                    Overtime Duration
                  </label>
                  <select
                    value={otHours}
                    onChange={(e) => setOtHours(e.target.value)}
                    style={{
                      width: '100%',
                      border: '1px solid #cbd5e1',
                      borderRadius: '6px',
                      padding: '6px 10px',
                      fontSize: '12px',
                    }}
                  >
                    <option value="2.0">+2.0 Hours (1.5x Standard Rate)</option>
                    <option value="3.0">+3.0 Hours (1.5x Standard Rate)</option>
                    <option value="4.0">+4.0 Hours (2.0x Double Rate)</option>
                  </select>
                </div>
                <div>
                  <label
                    style={{
                      display: 'block',
                      fontSize: '10px',
                      fontWeight: 700,
                      color: '#334155',
                      marginBottom: '4px',
                    }}
                  >
                    Target Sites
                  </label>
                  <select
                    style={{
                      width: '100%',
                      border: '1px solid #cbd5e1',
                      borderRadius: '6px',
                      padding: '6px 10px',
                      fontSize: '12px',
                    }}
                  >
                    <option>All 8 Sites (48 Technicians)</option>
                    <option>Karachi High-Rise Site Only (16 workers)</option>
                    <option>Islamabad Govt Complex (20 workers)</option>
                    <option>Peshawar Substation (12 workers)</option>
                  </select>
                </div>
              </div>

              <div>
                <label
                  style={{
                    display: 'block',
                    fontSize: '10px',
                    fontWeight: 700,
                    color: '#334155',
                    marginBottom: '4px',
                  }}
                >
                  Project Justification
                </label>
                <input
                  type="text"
                  defaultValue="Night Chiller Pressure Testing & Ducting Work"
                  style={{
                    width: '100%',
                    border: '1px solid #cbd5e1',
                    borderRadius: '6px',
                    padding: '6px 10px',
                    fontSize: '12px',
                  }}
                />
              </div>

              <div
                style={{
                  background: '#eff6ff',
                  border: '1px solid #bfdbfe',
                  borderRadius: '6px',
                  padding: '8px 10px',
                  fontSize: '11px',
                  color: '#242b5f',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                }}
              >
                <span>Estimated Overtime Wages:</span>
                <span style={{ fontWeight: 800 }}>Rs. 38,400 (Rs. 800/worker)</span>
              </div>
            </div>
            <div className="att-modal-footer">
              <button type="button" className="btn-modal-cancel" onClick={() => setModalType(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn-modal-confirm"
                onClick={handleAddOvertimeConfirm}
              >
                Authorize Overtime
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 6: 30-Day Timecard / Month History */}
      {modalType === 'timecard' && selectedEmp && (
        <div className="att-modal-backdrop" onClick={() => setModalType(null)}>
          <div className="att-modal-card wide" onClick={(e) => e.stopPropagation()}>
            <div className="att-modal-header">
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <div
                  className="att-emp-avatar"
                  style={{
                    backgroundColor: selectedEmp.avatarBg,
                    color: selectedEmp.avatarColor,
                    width: '28px',
                    height: '28px',
                  }}
                >
                  {selectedEmp.initials}
                </div>
                <div>
                  <h4 className="att-modal-title">{selectedEmp.name} &mdash; 30-Day Timecard</h4>
                  <p style={{ fontSize: '10px', color: '#64748b', margin: 0 }}>
                    {selectedEmp.empId} &bull; {selectedEmp.role} &bull; {selectedEmp.site}
                  </p>
                </div>
              </div>
              <button
                type="button"
                className="att-modal-close-btn"
                onClick={() => setModalType(null)}
              >
                <X size={15} />
              </button>
            </div>
            <div className="att-modal-body">
              {/* Summary KPIs */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '8px' }}>
                <div
                  style={{
                    background: '#f0fdf4',
                    padding: '8px',
                    borderRadius: '6px',
                    border: '1px solid #bbf7d0',
                  }}
                >
                  <span
                    style={{
                      fontSize: '9px',
                      fontWeight: 700,
                      color: '#15803d',
                      textTransform: 'uppercase',
                    }}
                  >
                    Present Days
                  </span>
                  <p
                    style={{
                      fontSize: '16px',
                      fontWeight: 800,
                      margin: '2px 0 0 0',
                      color: '#166534',
                    }}
                  >
                    22 Days
                  </p>
                </div>
                <div
                  style={{
                    background: '#fff7ed',
                    padding: '8px',
                    borderRadius: '6px',
                    border: '1px solid #fed7aa',
                  }}
                >
                  <span
                    style={{
                      fontSize: '9px',
                      fontWeight: 700,
                      color: '#c2410c',
                      textTransform: 'uppercase',
                    }}
                  >
                    Late Arrivals
                  </span>
                  <p
                    style={{
                      fontSize: '16px',
                      fontWeight: 800,
                      margin: '2px 0 0 0',
                      color: '#9a3412',
                    }}
                  >
                    1 Case
                  </p>
                </div>
                <div
                  style={{
                    background: '#fef2f2',
                    padding: '8px',
                    borderRadius: '6px',
                    border: '1px solid #fecaca',
                  }}
                >
                  <span
                    style={{
                      fontSize: '9px',
                      fontWeight: 700,
                      color: '#b91c1c',
                      textTransform: 'uppercase',
                    }}
                  >
                    Absences
                  </span>
                  <p
                    style={{
                      fontSize: '16px',
                      fontWeight: 800,
                      margin: '2px 0 0 0',
                      color: '#991b1b',
                    }}
                  >
                    0 Days
                  </p>
                </div>
                <div
                  style={{
                    background: '#faf5ff',
                    padding: '8px',
                    borderRadius: '6px',
                    border: '1px solid #e9d5ff',
                  }}
                >
                  <span
                    style={{
                      fontSize: '9px',
                      fontWeight: 700,
                      color: '#7e22ce',
                      textTransform: 'uppercase',
                    }}
                  >
                    Approved OT
                  </span>
                  <p
                    style={{
                      fontSize: '16px',
                      fontWeight: 800,
                      margin: '2px 0 0 0',
                      color: '#6b21a8',
                    }}
                  >
                    +4.0 Hrs
                  </p>
                </div>
              </div>

              {/* Day Punch Mini-Log */}
              <div
                style={{
                  maxHeight: '280px',
                  overflowY: 'auto',
                  border: '1px solid #f1f5f9',
                  borderRadius: '6px',
                }}
              >
                <table
                  style={{
                    width: '100%',
                    fontSize: '11px',
                    textAlign: 'left',
                    borderCollapse: 'collapse',
                  }}
                >
                  <thead
                    style={{
                      background: '#f8fafc',
                      borderBottom: '1px solid #e2e8f0',
                      color: '#64748b',
                      fontSize: '9.5px',
                      textTransform: 'uppercase',
                      fontWeight: 700,
                    }}
                  >
                    <tr>
                      <th style={{ padding: '6px 10px' }}>Date</th>
                      <th style={{ padding: '6px 10px' }}>Punch-In</th>
                      <th style={{ padding: '6px 10px' }}>Punch-Out</th>
                      <th style={{ padding: '6px 10px' }}>Hours</th>
                      <th style={{ padding: '6px 10px' }}>Variance</th>
                      <th style={{ padding: '6px 10px' }}>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[
                      {
                        date: '07 Sep 2026',
                        in: '10:00 AM',
                        out: 'In-Progress',
                        h: '7h 15m',
                        v: '+60m Late',
                        s: 'Late Arrival',
                        cls: 'orange',
                      },
                      {
                        date: '05 Sep 2026',
                        in: '08:55 AM',
                        out: '06:05 PM',
                        h: '9h 10m',
                        v: 'On-Time',
                        s: 'Present',
                        cls: 'green',
                      },
                      {
                        date: '04 Sep 2026',
                        in: '09:00 AM',
                        out: '06:00 PM',
                        h: '9h 00m',
                        v: 'On-Time',
                        s: 'Present',
                        cls: 'green',
                      },
                      {
                        date: '03 Sep 2026',
                        in: '09:12 AM',
                        out: '06:05 PM',
                        h: '8h 53m',
                        v: '+12m Late',
                        s: 'Late Arrival',
                        cls: 'orange',
                      },
                      {
                        date: '02 Sep 2026',
                        in: '08:58 AM',
                        out: '06:02 PM',
                        h: '9h 04m',
                        v: 'On-Time',
                        s: 'Present',
                        cls: 'green',
                      },
                      {
                        date: '01 Sep 2026',
                        in: '08:50 AM',
                        out: '06:00 PM',
                        h: '9h 10m',
                        v: 'On-Time',
                        s: 'Present',
                        cls: 'green',
                      },
                    ].map((row, i) => (
                      <tr key={i} style={{ borderBottom: '1px solid #f8fafc' }}>
                        <td style={{ padding: '6px 10px', fontWeight: 600 }}>{row.date}</td>
                        <td style={{ padding: '6px 10px' }}>{row.in}</td>
                        <td
                          style={{
                            padding: '6px 10px',
                            color: row.out === 'In-Progress' ? '#94a3b8' : '#1e293b',
                          }}
                        >
                          {row.out}
                        </td>
                        <td style={{ padding: '6px 10px', fontWeight: 600 }}>{row.h}</td>
                        <td style={{ padding: '6px 10px' }}>
                          <span
                            className={`badge-variance ${row.v.includes('Late') ? 'late' : 'ontime'}`}
                          >
                            {row.v}
                          </span>
                        </td>
                        <td style={{ padding: '6px 10px' }}>
                          <span className={`badge-exception-alert ${row.cls}`}>{row.s}</span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
            <div className="att-modal-footer">
              <button type="button" className="btn-modal-cancel" onClick={() => setModalType(null)}>
                Close
              </button>
              <button
                type="button"
                className="btn-modal-confirm"
                onClick={() => {
                  setModalType(null);
                  showToast(`Full 30-day timecard exported for ${selectedEmp.name}.`);
                }}
              >
                <Download size={12} style={{ marginRight: '4px' }} /> Download Timecard PDF
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 7: Import Logs */}
      {modalType === 'importLogs' && (
        <div className="att-modal-backdrop" onClick={() => setModalType(null)}>
          <div className="att-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="att-modal-header">
              <h4 className="att-modal-title">Import Biometric &amp; Machine Logs</h4>
              <button
                type="button"
                className="att-modal-close-btn"
                onClick={() => setModalType(null)}
              >
                <X size={15} />
              </button>
            </div>
            <div className="att-modal-body">
              <div>
                <label
                  style={{
                    display: 'block',
                    fontSize: '10px',
                    fontWeight: 700,
                    color: '#334155',
                    marginBottom: '4px',
                  }}
                >
                  Target Site / Biometric Machine
                </label>
                <select
                  style={{
                    width: '100%',
                    border: '1px solid #cbd5e1',
                    borderRadius: '6px',
                    padding: '6px 10px',
                    fontSize: '12px',
                  }}
                >
                  <option>Karachi High-Rise &bull; Main Gate ZKTeco #1</option>
                  <option>Islamabad Complex &bull; Central Biometric #2</option>
                  <option>Lahore Head Office &bull; Floor 3 Face Scanner</option>
                  <option>Peshawar Substation &bull; Substation 11kV Machine</option>
                </select>
              </div>

              <div>
                <label
                  style={{
                    display: 'block',
                    fontSize: '10px',
                    fontWeight: 700,
                    color: '#334155',
                    marginBottom: '4px',
                  }}
                >
                  Upload Log File (.DAT / .CSV / .XLSX)
                </label>
                <input
                  type="file"
                  style={{
                    width: '100%',
                    border: '1px dashed #cbd5e1',
                    borderRadius: '6px',
                    padding: '12px',
                    fontSize: '12px',
                    background: '#f8fafc',
                  }}
                />
              </div>

              <div
                style={{
                  background: '#f8fafc',
                  border: '1px solid #e2e8f0',
                  borderRadius: '6px',
                  padding: '10px',
                  fontSize: '11px',
                  color: '#475569',
                }}
              >
                <p style={{ margin: '0 0 4px 0', fontWeight: 700 }}>
                  Auto-Deduplication &amp; Geofence Verification
                </p>
                <p style={{ margin: 0, fontSize: '10px', color: '#94a3b8' }}>
                  Incoming timestamps will be automatically aligned with assigned roster shift
                  windows.
                </p>
              </div>
            </div>
            <div className="att-modal-footer">
              <button type="button" className="btn-modal-cancel" onClick={() => setModalType(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn-modal-confirm"
                onClick={() => {
                  setModalType(null);
                  showToast('Machine punch logs imported and verified!');
                }}
              >
                Process &amp; Import Logs
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 8: Sync All */}
      {modalType === 'syncAll' && (
        <div className="att-modal-backdrop" onClick={() => setModalType(null)}>
          <div className="att-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="att-modal-header">
              <h4 className="att-modal-title">Bulk Attendance Sync</h4>
              <button
                type="button"
                className="att-modal-close-btn"
                onClick={() => setModalType(null)}
              >
                <X size={15} />
              </button>
            </div>
            <div className="att-modal-body">
              <p style={{ fontSize: '12px', color: '#334155', margin: 0 }}>
                Force pull latest check-ins from all{' '}
                <strong>8 site fingerprint biometric machines</strong> and mobile GPS logs?
              </p>
              <div
                style={{
                  background: '#eff6ff',
                  border: '1px solid #bfdbfe',
                  borderRadius: '6px',
                  padding: '8px 10px',
                  fontSize: '11px',
                  color: '#242b5f',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                }}
              >
                <Radio size={13} /> Active sync connection online for all 8 project sites.
              </div>
            </div>
            <div className="att-modal-footer">
              <button type="button" className="btn-modal-cancel" onClick={() => setModalType(null)}>
                Cancel
              </button>
              <button type="button" className="btn-modal-confirm" onClick={handleSyncAllConfirm}>
                Sync Now
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Toast Notification */}
      {toastMessage && (
        <div className="att-toast">
          <CheckCircle2 size={16} style={{ color: '#22c55e' }} />
          <span>{toastMessage}</span>
        </div>
      )}
    </div>
  );
}
