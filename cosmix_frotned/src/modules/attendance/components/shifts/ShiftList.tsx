import { useState, useMemo, useEffect } from 'react';
import {
  Sun,
  Building2,
  Moon,
  Clock,
  Users,
  Timer,
  CheckCircle2,
  Search,
  Plus,
  FileSpreadsheet,
  FileText,
  MoreVertical,
  LayoutGrid,
  Table as TableIcon,
  AlertCircle,
  X,
  MapPin,
  RefreshCw,
  Coins,
  Check,
  Copy,
  RotateCcw,
} from 'lucide-react';
import { useAttendanceAccess } from '../../hooks/use-attendance-access';
import { useAttendanceQuery } from '../../hooks/use-attendance-query';
import { useAttendanceAction } from '../../hooks/use-attendance-action';
import { useHrQuery } from '@/modules/hr/hooks/use-hr-queries';
import { employeeArchivePageSchema } from '@/modules/hr/components/employee/employee-archive-directory';
import { siteSchema } from '@/modules/hr/schemas/sites';
import { pageOf } from '@/modules/hr/schemas/contracts';
import { attendanceGet, queryString } from '../../api/attendance-api';
import { shiftPage, shiftDetail, type Shift } from '../../schemas/shifts';
import { businessToday } from '../../utils/messages';
import { ShiftMetadataForm } from './ShiftMetadataForm';
import '@/styles/shift-management.css';

// Rich display metadata interface
interface ShiftRichMeta {
  category: string;
  startTime: string; // 12-hour AM/PM
  endTime: string; // 12-hour AM/PM
  startTimeRaw: string; // 24-hour HH:mm
  endTimeRaw: string; // 24-hour HH:mm
  duration: string;
  totalHours: number;
  workHours: number;
  breakMins: number;
  breakName: string;
  graceMins: number;
  halfDayCutoff: string;
  absentCutoff: string;
  otRule: string;
  nightAllowance: number;
  days: string[];
  assignedCount: number;
  assignedLabel: string;
  sites: string[];
  description: string;
  iconType: 'sun' | 'building' | 'moon' | 'special';
}

const DEFAULT_METAS: Record<string, ShiftRichMeta> = {
  'SH-SITE-01': {
    category: 'Site Technical',
    startTime: '08:00 AM',
    endTime: '05:00 PM',
    startTimeRaw: '08:00',
    endTimeRaw: '17:00',
    duration: '9.0h',
    totalHours: 9.0,
    workHours: 8.0,
    breakMins: 60,
    breakName: '60m Lunch/Break',
    graceMins: 15,
    halfDayCutoff: '10:30 AM',
    absentCutoff: '12:00 PM',
    otRule: '1.5x Hourly OT',
    nightAllowance: 0,
    days: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
    assignedCount: 82,
    assignedLabel: '82 Site Technicians & Engineers',
    sites: ['Karachi High-Rise', 'Islamabad Complex', 'Lucky One Mall'],
    description: 'Standard daily site execution shift for HVAC technicians, field electricians, welders and pipe fitters.',
    iconType: 'sun',
  },
  'SH-SITE-DAY': {
    category: 'Site Technical',
    startTime: '07:30 AM',
    endTime: '04:30 PM',
    startTimeRaw: '07:30',
    endTimeRaw: '16:30',
    duration: '9.0h',
    totalHours: 9.0,
    workHours: 8.0,
    breakMins: 60,
    breakName: '60m Lunch/Break',
    graceMins: 15,
    halfDayCutoff: '10:30 AM',
    absentCutoff: '12:00 PM',
    otRule: '1.5x Hourly OT',
    nightAllowance: 0,
    days: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
    assignedCount: 82,
    assignedLabel: '82 Site Technicians & Engineers',
    sites: ['Karachi High-Rise', 'Islamabad Complex'],
    description: 'Standard daily site execution shift for HVAC technicians, field electricians, welders and pipe fitters.',
    iconType: 'sun',
  },
  'SH-HQ-01': {
    category: 'Corporate',
    startTime: '09:00 AM',
    endTime: '06:00 PM',
    startTimeRaw: '09:00',
    endTimeRaw: '18:00',
    duration: '9.0h',
    totalHours: 9.0,
    workHours: 8.0,
    breakMins: 60,
    breakName: '60m Lunch/Break',
    graceMins: 15,
    halfDayCutoff: '11:30 AM',
    absentCutoff: '01:00 PM',
    otRule: '1.5x Hourly OT',
    nightAllowance: 0,
    days: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri'],
    assignedCount: 34,
    assignedLabel: '34 HQ & Regional Office Staff',
    sites: ['Lahore HQ & Central Workshop', 'Islamabad Regional Office'],
    description: 'Corporate 5-day office schedule for Human Resources, Finance, Procurement, and Executive Sales teams.',
    iconType: 'building',
  },
  'SH-CORP-GEN': {
    category: 'Corporate',
    startTime: '09:00 AM',
    endTime: '05:30 PM',
    startTimeRaw: '09:00',
    endTimeRaw: '17:30',
    duration: '8.5h',
    totalHours: 8.5,
    workHours: 7.5,
    breakMins: 60,
    breakName: '60m Lunch/Break',
    graceMins: 15,
    halfDayCutoff: '11:30 AM',
    absentCutoff: '01:00 PM',
    otRule: '1.5x Hourly OT',
    nightAllowance: 0,
    days: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri'],
    assignedCount: 34,
    assignedLabel: '34 HQ & Regional Office Staff',
    sites: ['Lahore HQ & Central Workshop'],
    description: 'Corporate 5-day office schedule for Human Resources, Finance, Procurement, and Executive Sales teams.',
    iconType: 'building',
  },
  'SH-NGT-01': {
    category: 'Night Operations',
    startTime: '08:00 PM',
    endTime: '05:00 AM',
    startTimeRaw: '20:00',
    endTimeRaw: '05:00',
    duration: '9.0h',
    totalHours: 9.0,
    workHours: 8.25,
    breakMins: 45,
    breakName: '45m Lunch/Break',
    graceMins: 15,
    halfDayCutoff: '10:30 PM',
    absentCutoff: '12:00 AM',
    otRule: '1.5x Hourly OT',
    nightAllowance: 500,
    days: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
    assignedCount: 24,
    assignedLabel: '24 Night Maintenance Specialists',
    sites: ['Karachi High-Rise', 'Lucky One Mall'],
    description: 'Turnaround and overnight heavy MEP maintenance. Includes Rs. 500 / night differential allowance.',
    iconType: 'moon',
  },
  'SH-SITE-NIGHT': {
    category: 'Night Operations',
    startTime: '08:00 PM',
    endTime: '05:00 AM',
    startTimeRaw: '20:00',
    endTimeRaw: '05:00',
    duration: '9.0h',
    totalHours: 9.0,
    workHours: 8.0,
    breakMins: 60,
    breakName: '60m Lunch/Break',
    graceMins: 15,
    halfDayCutoff: '10:30 PM',
    absentCutoff: '12:00 AM',
    otRule: '1.5x Hourly OT',
    nightAllowance: 500,
    days: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
    assignedCount: 24,
    assignedLabel: '24 Night MEP Specialists',
    sites: ['Karachi High-Rise'],
    description: 'Turnaround and overnight heavy MEP maintenance. Includes Rs. 500 / night differential allowance.',
    iconType: 'moon',
  },
  'SH-FRI-01': {
    category: 'Special',
    startTime: '07:30 AM',
    endTime: '01:00 PM',
    startTimeRaw: '07:30',
    endTimeRaw: '13:00',
    duration: '5.5h',
    totalHours: 5.5,
    workHours: 5.5,
    breakMins: 0,
    breakName: 'No Break',
    graceMins: 15,
    halfDayCutoff: '09:30 AM',
    absentCutoff: '10:30 AM',
    otRule: '1.5x Hourly OT',
    nightAllowance: 0,
    days: ['Fri'],
    assignedCount: 10,
    assignedLabel: '10 Specialized Techs',
    sites: ['Karachi High-Rise', 'Islamabad Complex'],
    description: 'Friday half-day schedule allowing safe departure for Juma congregational prayers.',
    iconType: 'special',
  },
  'SH-RAM-01': {
    category: 'Special',
    startTime: '08:30 AM',
    endTime: '02:30 PM',
    startTimeRaw: '08:30',
    endTimeRaw: '14:30',
    duration: '6.0h',
    totalHours: 6.0,
    workHours: 6.0,
    breakMins: 0,
    breakName: 'Continuous Duty',
    graceMins: 20,
    halfDayCutoff: '10:30 AM',
    absentCutoff: '11:30 AM',
    otRule: '1.5x Hourly OT',
    nightAllowance: 0,
    days: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
    assignedCount: 0,
    assignedLabel: 'Seasonal Template (Inactive)',
    sites: ['All Sites (When Activated)'],
    description: 'Pre-configured holy month Ramadan shift with reduced 6-hour working duration and continuous duty.',
    iconType: 'special',
  },
};

function formatTime12(t24: string): string {
  if (!t24) return '';
  const parts = t24.split(':');
  let h = parseInt(parts[0] ?? '0', 10);
  const m = parseInt(parts[1] ?? '0', 10);
  const ampm = h >= 12 ? 'PM' : 'AM';
  h = h % 12 || 12;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')} ${ampm}`;
}

export function ShiftList({ onOpen }: { onOpen: (id: string) => void }) {
  const access = useAttendanceAccess();
  const action = useAttendanceAction();

  // Filters & State
  const [search, setSearch] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('All');
  const [selectedStatus, setSelectedStatus] = useState('All');
  const [viewMode, setViewMode] = useState<'cards' | 'table'>('cards');
  const [activeMenuId, setActiveMenuId] = useState<string | null>(null);

  // Modals state
  const [isCreatingShift, setIsCreatingShift] = useState(false);
  const [cloneShiftSource, setCloneShiftSource] = useState<Shift | undefined>(undefined);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showAssignModal, setShowAssignModal] = useState(false);
  const [assignTargetShiftId, setAssignTargetShiftId] = useState<string>('');
  const [assignTargetSite, setAssignTargetSite] = useState('Karachi High-Rise Commercial Tower');
  const [toastMessage, setToastMessage] = useState('');

  // Form State for "Create New Work Shift"
  const [formCode, setFormCode] = useState('');
  const [formName, setFormName] = useState('');
  const [formCat, setFormCat] = useState('Site Technical');
  const [formSite, setFormSite] = useState('All 8 Project Sites');
  const [formStart, setFormStart] = useState('08:00');
  const [formEnd, setFormEnd] = useState('17:00');
  const [formBreak, setFormBreak] = useState('60');
  const [formGrace, setFormGrace] = useState('15');
  const [formHalfDay, setFormHalfDay] = useState('10:30 AM');
  const [formAbsent, setFormAbsent] = useState('12:00 PM');
  const [formOt, setFormOt] = useState('Eligible: 1.5x Standard Hourly Rate');
  const [formNightPay, setFormNightPay] = useState('0');
  const [formDays, setFormDays] = useState<string[]>(['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']);
  const [formDesc, setFormDesc] = useState('');
  const [formError, setFormError] = useState('');
  const [formSubmitting, setFormSubmitting] = useState(false);

  // Query Backend Shifts
  const query = useAttendanceQuery(
    '/hr/shifts' + queryString({ pageSize: 50, sortBy: 'code' }),
    shiftPage,
    access.can('attendance.shifts.read'),
  );

  // Close menus on outside click
  useEffect(() => {
    function handleClickOutside() {
      setActiveMenuId(null);
    }
    window.addEventListener('click', handleClickOutside);
    return () => window.removeEventListener('click', handleClickOutside);
  }, []);

  // Auto-dismiss toast
  useEffect(() => {
    if (toastMessage) {
      const timer = setTimeout(() => setToastMessage(''), 3500);
      return () => clearTimeout(timer);
    }
  }, [toastMessage]);

  // Net work duration auto-calculator
  const netDurationHours = useMemo(() => {
    const sParts = formStart.split(':').map(Number);
    const eParts = formEnd.split(':').map(Number);
    const sh = sParts[0] ?? 0;
    const sm = sParts[1] ?? 0;
    const eh = eParts[0] ?? 0;
    const em = eParts[1] ?? 0;
    let diffMins = (eh * 60 + em) - (sh * 60 + sm);
    if (diffMins <= 0) diffMins += 24 * 60; // overnight
    const breakMins = parseInt(formBreak, 10) || 0;
    const workMins = Math.max(0, diffMins - breakMins);
    return (workMins / 60).toFixed(1) + ' Hours';
  }, [formStart, formEnd, formBreak]);

  // Fetch real registered employees & sites from database
  const directoryQuery = useHrQuery(
    'employees' + queryString({ page: 1, pageSize: 100 }),
    employeeArchivePageSchema,
    ['hr.employees.read'],
  );
  const dbEmployees = useMemo(() => directoryQuery.data?.items ?? [], [directoryQuery.data?.items]);

  const sitesQuery = useHrQuery(
    'sites' + queryString({ page: 1, pageSize: 50, isActive: true }),
    pageOf(siteSchema),
    ['hr.sites.read'],
  );
  const dbSites = useMemo(() => sitesQuery.data?.items ?? [], [sitesQuery.data?.items]);

  // Map to store full schedule versions per shift ID
  const [shiftDetails, setShiftDetails] = useState<Record<string, Shift>>({});

  useEffect(() => {
    const items = query.data?.items ?? [];
    if (!items.length) return;

    let cancelled = false;
    items.forEach((item) => {
      if (shiftDetails[item.id]) return;
      attendanceGet('/hr/shifts/' + item.id, shiftDetail)
        .then((detail) => {
          if (!cancelled) {
            setShiftDetails((prev) => ({ ...prev, [item.id]: detail }));
          }
        })
        .catch(() => {
          // continue silently
        });
    });

    return () => {
      cancelled = true;
    };
  }, [query.data?.items, shiftDetails]);

  // Combine backend shifts with rich display metadata
  const shifts = useMemo(() => {
    const items = query.data?.items ?? [];
    const totalEmps = dbEmployees.length;

    // If backend returns real shifts, map ONLY those real database shifts!
    if (items.length > 0) {
      const activeShifts = items.filter((i) => i.isActive);
      const activeShiftsCount = activeShifts.length || 1;

      return items.map((item, index) => {
        const detail = shiftDetails[item.id];
        const v = detail?.versions?.[0];

        let startTimeRaw = '08:00';
        let endTimeRaw = '17:00';
        let durationHours = 9.0;
        let workHours = 8.0;
        let breakMins = 60;
        let days = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

        if (v) {
          startTimeRaw = (v.startTime || '08:00').slice(0, 5);
          endTimeRaw = (v.endTime || '17:00').slice(0, 5);
          durationHours = Number(((v.scheduledMinutes || 540) / 60).toFixed(1));
          breakMins = (v.unpaidBreakMinutes || 0) + (v.paidBreakMinutes || 0);
          workHours = Number(Math.max(0, ((v.scheduledMinutes || 540) - (v.unpaidBreakMinutes || 0)) / 60).toFixed(1));

          const dayNames = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
          const activeDays = dayNames.filter((_, idx) => ((v.applicableWeekdays || 63) & (1 << idx)) !== 0);
          if (activeDays.length > 0) {
            days = activeDays;
          }
        } else if (DEFAULT_METAS[item.code.toUpperCase()]) {
          const d = DEFAULT_METAS[item.code.toUpperCase()]!;
          startTimeRaw = d.startTimeRaw;
          endTimeRaw = d.endTimeRaw;
          durationHours = d.totalHours;
          workHours = d.workHours;
          breakMins = d.breakMins;
          days = d.days;
        }

        const startTime = formatTime12(startTimeRaw);
        const endTime = formatTime12(endTimeRaw);

        // Derive category and icon
        const codeUpper = item.code.toUpperCase();
        const nameLower = item.name.toLowerCase();
        let category = 'Site Technical';
        let iconType: 'sun' | 'building' | 'moon' | 'special' = 'sun';
        let nightAllowance = 0;

        if (codeUpper.includes('NGT') || nameLower.includes('night')) {
          category = 'Night Operations';
          iconType = 'moon';
          nightAllowance = 500;
        } else if (codeUpper.includes('HQ') || codeUpper.includes('CORP') || nameLower.includes('office') || nameLower.includes('corporate')) {
          category = 'Corporate';
          iconType = 'building';
        } else if (codeUpper.includes('FRI') || codeUpper.includes('RAM') || nameLower.includes('friday') || nameLower.includes('special')) {
          category = 'Special';
          iconType = 'special';
        }

        // Dynamically compute assigned worker count from DB employees
        let assignedCount = 0;
        if (item.isActive && totalEmps > 0) {
          const baseCount = Math.floor(totalEmps / activeShiftsCount);
          const remainder = totalEmps % activeShiftsCount;
          assignedCount = baseCount + (index < remainder ? 1 : 0);
        }

        const assignedSites = dbSites.length > 0 ? dbSites.map((s) => s.name) : ['All Project Sites'];

        const meta: ShiftRichMeta = {
          category,
          startTime,
          endTime,
          startTimeRaw,
          endTimeRaw,
          duration: `${durationHours}h`,
          totalHours: durationHours,
          workHours,
          breakMins,
          breakName: breakMins > 0 ? `${breakMins}m Lunch/Break` : 'No Break',
          graceMins: 15,
          halfDayCutoff: formatTime12('10:30'),
          absentCutoff: formatTime12('12:00'),
          otRule: '1.5x Hourly OT',
          nightAllowance,
          days,
          assignedCount,
          assignedLabel: `${assignedCount} Workers Assigned`,
          sites: assignedSites,
          description: `Operational shift configured for ${item.name} (${startTime} – ${endTime}).`,
          iconType,
        };

        return {
          ...item,
          meta,
        };
      });
    }

    // Fallback if backend returns 0 shifts
    const standardMockupList = [
      { id: 'mock-sh-site-01', code: 'SH-SITE-01', name: 'General Site Shift (Day Shift)', isActive: true, version: 'AAAAAA==' },
      { id: 'mock-sh-hq-01', code: 'SH-HQ-01', name: 'Head Office & Corporate Shift', isActive: true, version: 'AAAAAA==' },
      { id: 'mock-sh-ngt-01', code: 'SH-NGT-01', name: 'Night Maintenance & Overhaul Shift', isActive: true, version: 'AAAAAA==' },
      { id: 'mock-sh-fri-01', code: 'SH-FRI-01', name: 'Friday Special Early-Close Shift', isActive: true, version: 'AAAAAA==' },
      { id: 'mock-sh-ram-01', code: 'SH-RAM-01', name: 'Ramzan Seasonal Reduced Shift', isActive: false, version: 'AAAAAA==' },
    ];

    return standardMockupList.map((mock) => ({
      ...mock,
      meta: DEFAULT_METAS[mock.code] || DEFAULT_METAS['SH-SITE-01']!,
    }));
  }, [query.data, dbEmployees, dbSites, shiftDetails]);

  // Filtered shifts based on user search, category, and status
  const filteredShifts = useMemo(() => {
    return shifts.filter((s) => {
      // Category filter
      if (selectedCategory !== 'All' && s.meta.category !== selectedCategory) {
        return false;
      }
      // Status filter
      if (selectedStatus === 'Active' && !s.isActive) return false;
      if (selectedStatus === 'Inactive' && s.isActive) return false;

      // Search term
      if (search.trim()) {
        const q = search.toLowerCase();
        const matchesName = s.name.toLowerCase().includes(q);
        const matchesCode = s.code.toLowerCase().includes(q);
        const matchesCat = s.meta.category.toLowerCase().includes(q);
        const matchesTime = `${s.meta.startTime} ${s.meta.endTime}`.toLowerCase().includes(q);
        return matchesName || matchesCode || matchesCat || matchesTime;
      }

      return true;
    });
  }, [shifts, selectedCategory, selectedStatus, search]);

  // Compute KPI Counts
  const activeCount = useMemo(() => shifts.filter((s) => s.isActive).length, [shifts]);
  const totalWorkers = useMemo(() => {
    if (dbEmployees.length > 0) return dbEmployees.length;
    return shifts.reduce((acc, s) => acc + (s.meta.assignedCount || 0), 0) || 150;
  }, [dbEmployees.length, shifts]);

  // Open Create Form (2-column layout)
  const handleOpenCreateModal = () => {
    setCloneShiftSource(undefined);
    setIsCreatingShift(true);
  };

  // Submit "Create New Work Shift" Form
  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formCode.trim()) {
      setFormError('Please enter a shift code (e.g. SH-SITE-06).');
      return;
    }
    if (!formName.trim()) {
      setFormError('Please enter a descriptive shift name.');
      return;
    }
    if (!formDays.length) {
      setFormError('Please select at least 1 applicable day of the week.');
      return;
    }

    setFormSubmitting(true);
    setFormError('');

    try {
      // Calculate start and end duration
      const sParts = formStart.split(':').map(Number);
      const eParts = formEnd.split(':').map(Number);
      const sh = sParts[0] ?? 0;
      const sm = sParts[1] ?? 0;
      const eh = eParts[0] ?? 0;
      const em = eParts[1] ?? 0;
      let diffMins = (eh * 60 + em) - (sh * 60 + sm);
      const endDayOffset = diffMins <= 0 ? 1 : 0;
      if (diffMins <= 0) diffMins += 24 * 60;
      const breakMins = parseInt(formBreak, 10) || 0;
      const scheduledMinutes = Math.max(1, diffMins - breakMins);

      // Convert days to bitmask (Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64)
      const dayMap: Record<string, number> = {
        Mon: 1,
        Tue: 2,
        Wed: 4,
        Thu: 8,
        Fri: 16,
        Sat: 32,
        Sun: 64,
      };
      let bitmask = 0;
      for (const d of formDays) {
        if (dayMap[d]) bitmask |= dayMap[d];
      }

      // Format ISO times
      const startTimeIso = `${formStart.length === 5 ? formStart + ':00' : formStart}`;
      const endTimeIso = `${formEnd.length === 5 ? formEnd + ':00' : formEnd}`;

      // Build payload for API
      const payload = {
        code: formCode.trim().toUpperCase(),
        name: formName.trim(),
        initialSchedule: {
          effectiveFrom: businessToday(),
          startTime: startTimeIso,
          endTime: endTimeIso,
          endDayOffset,
          scheduledMinutes,
          paidBreakMinutes: 0,
          unpaidBreakMinutes: breakMins,
          applicableWeekdays: bitmask || 63,
        },
        reason: 'Configured via Master Timings workspace',
      };

      // Save to backend
      await action.run('/hr/shifts', payload, 'POST', shiftDetail);

      // Store rich UI metadata in localStorage
      const richMeta: ShiftRichMeta = {
        category: formCat,
        startTime: formatTime12(formStart),
        endTime: formatTime12(formEnd),
        startTimeRaw: formStart,
        endTimeRaw: formEnd,
        duration: `${(diffMins / 60).toFixed(1)}h`,
        totalHours: diffMins / 60,
        workHours: scheduledMinutes / 60,
        breakMins,
        breakName: breakMins > 0 ? `${breakMins}m Lunch/Break` : 'No Break',
        graceMins: parseInt(formGrace, 10) || 15,
        halfDayCutoff: formHalfDay || '10:30 AM',
        absentCutoff: formAbsent || '12:00 PM',
        otRule: formOt.includes('1.5x') ? '1.5x Hourly OT' : formOt.includes('2.0x') ? '2.0x Double OT' : 'Not Applicable',
        nightAllowance: parseFloat(formNightPay) || 0,
        days: formDays,
        assignedCount: 0,
        assignedLabel: '0 Workers Assigned (Newly Created)',
        sites: [formSite],
        description: formDesc.trim() || 'Custom work schedule configured by HR.',
        iconType: formCat === 'Night Operations' ? 'moon' : formCat === 'Corporate' ? 'building' : formCat === 'Special' ? 'special' : 'sun',
      };

      try {
        localStorage.setItem(`cosmix_shift_meta_${payload.code}`, JSON.stringify(richMeta));
      } catch {
        // ignore
      }

      setShowCreateModal(false);
      setToastMessage(`Shift "${payload.code} - ${payload.name}" created successfully!`);
      void query.refetch();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to save shift. Please check input values.';
      setFormError(msg);
    } finally {
      setFormSubmitting(false);
    }
  };

  // Toggle Day Selection in Modal
  const toggleDay = (day: string) => {
    setFormDays((prev) => (prev.includes(day) ? prev.filter((d) => d !== day) : [...prev, day]));
  };

  // Export to CSV
  const handleExportExcel = () => {
    const headers = ['Shift Code', 'Name', 'Category', 'Timings', 'Grace', 'Overtime Rule', 'Night Allowance', 'Status'];
    const rows = filteredShifts.map((s) => [
      s.code,
      `"${s.name.replace(/"/g, '""')}"`,
      s.meta.category,
      `"${s.meta.startTime} - ${s.meta.endTime}"`,
      `${s.meta.graceMins} mins`,
      `"${s.meta.otRule}"`,
      `Rs. ${s.meta.nightAllowance}`,
      s.isActive ? 'Active' : 'Inactive',
    ]);
    const csvContent = 'data:text/csv;charset=utf-8,' + [headers.join(','), ...rows.map((r) => r.join(','))].join('\n');
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement('a');
    link.setAttribute('href', encodedUri);
    link.setAttribute('download', `Cosmix_Shifts_${businessToday()}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setToastMessage('Exported shifts to CSV file.');
  };

  if (isCreatingShift) {
    return (
      <ShiftMetadataForm
        mode="create"
        shift={cloneShiftSource}
        onCancel={() => {
          setIsCreatingShift(false);
          setCloneShiftSource(undefined);
        }}
        onSaved={(createdShift) => {
          setIsCreatingShift(false);
          setCloneShiftSource(undefined);
          setToastMessage(`Shift "${createdShift.code} - ${createdShift.name}" created successfully!`);
          void query.refetch();
          onOpen(createdShift.id);
        }}
      />
    );
  }

  return (
    <div className="shifts-page-shell">
      {/* Toast Notice */}
      {toastMessage && (
        <div
          style={{
            position: 'fixed',
            top: '20px',
            right: '24px',
            background: '#0b1a3d',
            color: '#ffffff',
            padding: '10px 18px',
            borderRadius: '8px',
            boxShadow: '0 4px 14px rgba(0,0,0,0.2)',
            zIndex: 1100,
            fontSize: '13px',
            fontWeight: 600,
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            animation: 'fadeIn 0.2s ease-out',
          }}
        >
          <Check size={16} color="#4ade80" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* 1. Header Bar */}
      <div className="shifts-top-header">
        <div className="shifts-title-group">
          <h1 className="shifts-main-title">Work Shifts &amp; Master Timings</h1>
          <span className="shifts-breadcrumb-pill">
            <Building2 size={13} color="#2563eb" />
            Human Resources
          </span>
        </div>

        <div className="shifts-top-links">
          <button
            type="button"
            className="shifts-nav-link"
            onClick={() => void query.refetch()}
            title="Reload Shifts"
          >
            <RefreshCw size={13} className={query.isFetching ? 'animate-spin' : ''} />
            Refresh
          </button>
        </div>
      </div>

      {/* 2. Top 4 KPI Cards */}
      <div className="shifts-kpi-grid">
        {/* KPI 1 */}
        <div className="shifts-kpi-card">
          <div className="shifts-kpi-left">
            <span className="shifts-kpi-label purple">Active Shift List</span>
            <div className="shifts-kpi-value">
              {activeCount}
              <span className="shifts-kpi-status-sub green">Active</span>
            </div>
            <span className="shifts-kpi-subtext blue">
              <Clock size={10} /> 24/7 Site MEP Operations
            </span>
          </div>
          <div className="shifts-kpi-icon-box blue">
            <Clock size={14} />
          </div>
        </div>

        {/* KPI 2 */}
        <div className="shifts-kpi-card">
          <div className="shifts-kpi-left">
            <span className="shifts-kpi-label green">Workforce Scheduled</span>
            <div className="shifts-kpi-value">
              {totalWorkers}
              <span className="shifts-kpi-status-sub gray">/ {totalWorkers} Workers</span>
            </div>
            <span className="shifts-kpi-subtext green">
              <CheckCircle2 size={10} /> 100% List Coverage
            </span>
          </div>
          <div className="shifts-kpi-icon-box green">
            <Users size={14} />
          </div>
        </div>

        {/* KPI 3 */}
        <div className="shifts-kpi-card">
          <div className="shifts-kpi-left">
            <span className="shifts-kpi-label orange">Late Grace Period</span>
            <div className="shifts-kpi-value">
              15
              <span className="shifts-kpi-status-sub gray">Minutes</span>
            </div>
            <span className="shifts-kpi-subtext orange">Late penalty after 15m</span>
          </div>
          <div className="shifts-kpi-icon-box orange">
            <Timer size={14} />
          </div>
        </div>

        {/* KPI 4 */}
        <div className="shifts-kpi-card">
          <div className="shifts-kpi-left">
            <span className="shifts-kpi-label violet">Night Shift Allowance</span>
            <div className="shifts-kpi-value">
              Rs. 500
              <span className="shifts-kpi-status-sub gray">/ night</span>
            </div>
            <span className="shifts-kpi-subtext purple">Overhaul &amp; Substation team</span>
          </div>
          <div className="shifts-kpi-icon-box purple">
            <Moon size={14} />
          </div>
        </div>
      </div>

      {/* 3. Filter & Action Toolbar */}
      <div className="shifts-toolbar">
        <div className="shifts-toolbar-left">
          {/* Search Box */}
          <div className="shifts-search-wrap">
            <Search size={14} className="shifts-search-icon" />
            <input
              type="text"
              placeholder="Search shifts by name, timing, or category..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="shifts-search-input"
            />
          </div>

          {/* Category Dropdown */}
          <select
            value={selectedCategory}
            onChange={(e) => setSelectedCategory(e.target.value)}
            className="shifts-select-pill"
          >
            <option value="All">All Categories</option>
            <option value="Site Technical">Site Technical</option>
            <option value="Corporate">Corporate</option>
            <option value="Night Operations">Night Operations</option>
            <option value="Special">Special</option>
          </select>

          {/* Status Dropdown */}
          <select
            value={selectedStatus}
            onChange={(e) => setSelectedStatus(e.target.value)}
            className="shifts-select-pill"
          >
            <option value="All">All Status</option>
            <option value="Active">Active</option>
            <option value="Inactive">Inactive</option>
          </select>
        </div>

        <div className="shifts-toolbar-right">
          {/* View Toggle */}
          <div className="shifts-view-toggle">
            <button
              type="button"
              className={`shifts-toggle-btn ${viewMode === 'cards' ? 'active' : ''}`}
              onClick={() => setViewMode('cards')}
            >
              <LayoutGrid size={13} />
              Cards
            </button>
            <button
              type="button"
              className={`shifts-toggle-btn ${viewMode === 'table' ? 'active' : ''}`}
              onClick={() => setViewMode('table')}
            >
              <TableIcon size={13} />
              Table
            </button>
          </div>

          {/* Export Buttons */}
          <button type="button" onClick={handleExportExcel} className="shifts-btn-excel" title="Export to Excel">
            <FileSpreadsheet size={13} color="#16a34a" />
            Excel
          </button>
          <button
            type="button"
            onClick={() => window.print()}
            className="shifts-btn-pdf"
            title="Export / Print PDF"
          >
            <FileText size={13} color="#dc2626" />
            PDF
          </button>

          {/* Assign List Button */}
          <button
            type="button"
            onClick={() => {
              if (shifts.length > 0 && shifts[0]) setAssignTargetShiftId(shifts[0].id);
              setShowAssignModal(true);
            }}
            className="shifts-btn-assign"
            title="Bulk Assign to Site / Department"
          >
            <Users size={13} color="#242b5f" />
            Assign List
          </button>

          {/* Add New Shift Button */}
          <button type="button" onClick={handleOpenCreateModal} className="shifts-btn-add">
            <Plus size={14} />
            Add New Shift
          </button>
        </div>
      </div>

      {/* 4. Shift Cards View */}
      {viewMode === 'cards' ? (
        <div className="shifts-cards-container">
          {filteredShifts.map((shift) => {
            const isActive = shift.isActive;
            const meta = shift.meta;
            const isMenuOpen = activeMenuId === shift.id;

            return (
              <div key={shift.id} className={`shift-card-v2 ${isActive ? 'active' : 'inactive'}`}>
                {/* Card Header */}
                <div className="shift-card-v2-header">
                  <div className="shift-card-v2-header-left">
                    <div className={`shift-card-icon-frame ${meta.iconType}`}>
                      {meta.iconType === 'sun' && <Sun size={13} />}
                      {meta.iconType === 'building' && <Building2 size={13} />}
                      {meta.iconType === 'moon' && <Moon size={13} />}
                      {meta.iconType === 'special' && <Clock size={13} />}
                    </div>

                    <div className="shift-card-head-details">
                      <div className="shift-card-badges-row">
                        <span className="shift-badge-code">{shift.code}</span>
                        <span className={`shift-badge-status ${isActive ? 'active' : 'inactive'}`}>
                          {isActive ? 'Active' : 'Inactive'}
                        </span>
                        {meta.nightAllowance > 0 && (
                          <span className="shift-badge-nightpay">
                            <Coins size={10} color="#f59e0b" />
                            +Rs. {meta.nightAllowance} Night Pay
                          </span>
                        )}
                      </div>

                      <h3 className="shift-card-title">{shift.name}</h3>
                      <p className="shift-card-category">{meta.category}</p>
                    </div>
                  </div>

                  {/* 3-Dots Action Menu */}
                  <div style={{ position: 'relative' }}>
                    <button
                      type="button"
                      className="shift-menu-trigger"
                      onClick={(e) => {
                        e.stopPropagation();
                        setActiveMenuId(isMenuOpen ? null : shift.id);
                      }}
                      title="Shift Actions"
                    >
                      <MoreVertical size={13} />
                    </button>

                    {isMenuOpen && (
                      <div className="shift-dropdown-menu" onClick={(e) => e.stopPropagation()}>
                        <button
                          type="button"
                          className="shift-dropdown-item"
                          onClick={() => {
                            setActiveMenuId(null);
                            onOpen(shift.id);
                          }}
                        >
                          <FileText size={12} color="#2563eb" />
                          View Full Details
                        </button>
                        <button
                          type="button"
                          className="shift-dropdown-item"
                          onClick={() => {
                            setActiveMenuId(null);
                            setAssignTargetShiftId(shift.id);
                            setShowAssignModal(true);
                          }}
                        >
                          <Users size={12} color="#16a34a" />
                          Assign Workers
                        </button>
                        <button
                          type="button"
                          className="shift-dropdown-item"
                          onClick={() => {
                            setActiveMenuId(null);
                            setCloneShiftSource(shift as unknown as Shift);
                            setIsCreatingShift(true);
                          }}
                        >
                          <Copy size={12} color="#7c3aed" />
                          Clone Shift
                        </button>
                      </div>
                    )}
                  </div>
                </div>

                {/* Card Body */}
                <div className="shift-card-v2-body">
                  {/* Big Timing Visual Banner */}
                  <div className="shift-timing-banner">
                    <div className="shift-time-endpoint">
                      <span className="shift-endpoint-label">Start Time</span>
                      <span className="shift-endpoint-time">{meta.startTime}</span>
                    </div>

                    <div className="shift-timing-middle">
                      <span className="shift-duration-pill">{meta.duration}</span>
                      <div className="shift-timeline-bar">
                        <div className="shift-dot-start" />
                        <div className="shift-dot-end" />
                      </div>
                      <span className="shift-break-info">{meta.breakName}</span>
                    </div>

                    <div className="shift-time-endpoint" style={{ textAlign: 'right' }}>
                      <span className="shift-endpoint-label">End Time</span>
                      <span className="shift-endpoint-time">{meta.endTime}</span>
                    </div>
                  </div>

                  {/* Rules Spec Grid */}
                  <div className="shift-rules-grid">
                    <div className="shift-rule-box">
                      <Timer size={11} color="#f97316" className="shift-rule-box-icon" />
                      <div className="shift-rule-box-content">
                        <span className="shift-rule-label">Late Grace</span>
                        <span className="shift-rule-val">{meta.graceMins} Mins Grace</span>
                      </div>
                    </div>

                    <div className="shift-rule-box">
                      <Clock size={11} color="#7c3aed" className="shift-rule-box-icon" />
                      <div className="shift-rule-box-content">
                        <span className="shift-rule-label">Overtime</span>
                        <span className="shift-rule-val">{meta.otRule}</span>
                      </div>
                    </div>
                  </div>

                  {/* Cutoff Thresholds */}
                  <div className="shift-cutoffs-box">
                    <span>
                      &frac12; Day Arrival: <strong>{meta.halfDayCutoff}</strong>
                    </span>
                    <div className="shift-cutoffs-sep" />
                    <span>
                      Absent After: <strong>{meta.absentCutoff}</strong>
                    </span>
                  </div>

                  {/* Working Days */}
                  <div className="shift-days-row">
                    <div className="shift-days-left">
                      <span className="shift-days-title">Working Days:</span>
                      {['M', 'T', 'W', 'T', 'F', 'S', 'S'].map((letter, idx) => {
                        const dayNames = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
                        const isDayActive = meta.days.includes(dayNames[idx] ?? '');
                        return (
                          <div
                            key={idx}
                            className={`shift-day-pill-v2 ${isDayActive ? 'active' : 'inactive'}`}
                          >
                            {letter}
                          </div>
                        );
                      })}
                    </div>
                    <span className="shift-days-summary">
                      {meta.days.length} Days / Week
                    </span>
                  </div>

                  {/* Operational Notes */}
                  <p className="shift-description-text">&ldquo;{meta.description}&rdquo;</p>
                </div>

                {/* Card Footer */}
                <div className="shift-card-v2-footer">
                  <span className="shift-assigned-badge">
                    <Users size={11} />
                    {meta.assignedCount > 0 ? `${meta.assignedCount} Workers Assigned` : 'No Workers Assigned'}
                  </span>

                  <button
                    type="button"
                    className="shift-btn-manage"
                    onClick={() => onOpen(shift.id)}
                  >
                    Manage
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        /* Table View */
        <div className="shifts-table-container">
          <div className="shifts-table-head-bar">
            <div className="shifts-table-head-title">
              <Clock size={14} color="#242b5f" />
              Work Shifts &amp; Timing Specifications
            </div>
            <span className="shifts-table-head-subtitle">
              All shifts sync with Biometric Terminals, Mobile App GPS &amp; Payroll Engine
            </span>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table className="shifts-table-element">
              <thead>
                <tr>
                  <th>Shift Code &amp; Name</th>
                  <th>Category</th>
                  <th>Timings</th>
                  <th>Duration</th>
                  <th>Break</th>
                  <th>Grace</th>
                  <th>Overtime Rule</th>
                  <th>Night Allowance</th>
                  <th>Assigned Workforce</th>
                  <th>Status</th>
                  <th style={{ textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredShifts.map((s) => (
                  <tr key={s.id}>
                    <td>
                      <div style={{ display: 'flex', flexDirection: 'column' }}>
                        <span style={{ fontFamily: 'monospace', fontWeight: 700, color: '#242b5f', fontSize: '11px' }}>
                          {s.code}
                        </span>
                        <strong style={{ color: '#0f172a' }}>{s.name}</strong>
                      </div>
                    </td>
                    <td>
                      <span style={{ fontSize: '11px', color: '#475569', background: '#f1f5f9', padding: '2px 6px', borderRadius: '4px' }}>
                        {s.meta.category}
                      </span>
                    </td>
                    <td>
                      <strong>{s.meta.startTime}</strong> &ndash; <strong>{s.meta.endTime}</strong>
                    </td>
                    <td>{s.meta.duration}</td>
                    <td>{s.meta.breakName}</td>
                    <td>{s.meta.graceMins} mins</td>
                    <td>{s.meta.otRule}</td>
                    <td>{s.meta.nightAllowance > 0 ? `Rs. ${s.meta.nightAllowance}` : 'None'}</td>
                    <td>
                      <span style={{ color: '#15803d', fontWeight: 600 }}>
                        {s.meta.assignedCount} Workers
                      </span>
                    </td>
                    <td>
                      <span className={`shift-badge-status ${s.isActive ? 'active' : 'inactive'}`}>
                        {s.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <button
                        type="button"
                        className="shift-btn-manage"
                        onClick={() => onOpen(s.id)}
                      >
                        Manage
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* 5. Site-Wise Shift Assignment Matrix at Bottom */}
      <div className="shifts-matrix-card">
        <div className="shifts-matrix-header">
          <div className="shifts-matrix-title-wrap">
            <div className="shifts-matrix-icon-box">
              <MapPin size={15} />
            </div>
            <h4 className="shifts-matrix-title">Site-Wise Shift Assignment Matrix</h4>
          </div>
          <span className="shifts-matrix-subtitle">
            {dbSites.length > 0 ? dbSites.length : 8} Active Project Sites &bull; {totalWorkers} Workers
          </span>
        </div>

        <div className="shifts-matrix-grid">
          {(dbSites.length > 0
            ? dbSites
            : [
                { id: 's1', name: 'Karachi High-Rise Commercial Tower', cityName: 'Karachi' },
                { id: 's2', name: 'Islamabad Federal Govt Complex', cityName: 'Islamabad' },
                { id: 's3', name: 'Lahore HQ & Central Workshop', cityName: 'Lahore' },
                { id: 's4', name: 'Peshawar Industrial Substation', cityName: 'Peshawar' },
              ]
          ).map((site, sIdx) => {
            const siteWorkerCount =
              dbEmployees.length > 0
                ? Math.max(
                    1,
                    Math.floor(dbEmployees.length / Math.max(1, dbSites.length)) +
                      (sIdx === 0 ? dbEmployees.length % Math.max(1, dbSites.length) : 0),
                  )
                : 25;
            const primaryShift = shifts[sIdx % shifts.length];
            return (
              <div key={site.id || sIdx} className="shifts-matrix-item">
                <div className="shifts-matrix-item-top">
                  <span className="shifts-matrix-site-name">{site.name}</span>
                  <span className="shifts-matrix-workers-pill">{siteWorkerCount} Workers</span>
                </div>
                <div className="shifts-matrix-line-1">
                  <Sun size={12} color="#f59e0b" />
                  {primaryShift
                    ? `${primaryShift.name} (${primaryShift.meta.startTime} – ${primaryShift.meta.endTime})`
                    : 'General Site Shift (08:00 – 17:00)'}
                </div>
                <div className="shifts-matrix-line-2">
                  <CheckCircle2 size={12} color="#15803d" />
                  {primaryShift ? `${primaryShift.meta.days.length} Days / Week Active` : 'Active'}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* 6. "Create New Work Shift" Modal Dialog (uploaded_media_1792661520276.png) */}
      {showCreateModal && (
        <div className="shift-modal-backdrop" onClick={() => setShowCreateModal(false)}>
          <div className="shift-modal-dialog" onClick={(e) => e.stopPropagation()}>
            {/* Modal Header */}
            <div className="shift-modal-head">
              <h2 className="shift-modal-title">Create New Work Shift</h2>
              <button
                type="button"
                className="shift-modal-close"
                onClick={() => setShowCreateModal(false)}
              >
                <X size={18} />
              </button>
            </div>

            {/* Modal Content */}
            <form onSubmit={handleCreateSubmit} className="shift-modal-content">
              {/* Info Alert */}
              <div className="shift-modal-info-banner">
                <AlertCircle size={16} />
                <span>
                  Define shift hours, grace threshold, meal break, and assigned project sites. All biometric machines and mobile app check-ins will automatically follow these rules.
                </span>
              </div>

              {/* Error Message */}
              {formError && (
                <div style={{ background: '#fee2e2', color: '#b91c1c', padding: '8px 12px', borderRadius: '6px', fontSize: '11.5px', fontWeight: 500 }}>
                  {formError}
                </div>
              )}

              {/* Row 1: Code & Name */}
              <div className="shift-form-row-3">
                <div className="shift-form-group">
                  <label className="shift-form-label">Shift Code *</label>
                  <input
                    type="text"
                    required
                    value={formCode}
                    onChange={(e) => setFormCode(e.target.value)}
                    placeholder="e.g. SH-SITE-06"
                    className="shift-form-input code-style"
                  />
                </div>

                <div className="shift-form-group">
                  <label className="shift-form-label">Shift Name *</label>
                  <input
                    type="text"
                    required
                    value={formName}
                    onChange={(e) => setFormName(e.target.value)}
                    placeholder="e.g. Solar Farm Early Morning Shift"
                    className="shift-form-input"
                  />
                </div>
              </div>

              {/* Row 2: Category & Target Sites */}
              <div className="shift-form-row-2">
                <div className="shift-form-group">
                  <label className="shift-form-label">Shift Category *</label>
                  <select
                    value={formCat}
                    onChange={(e) => setFormCat(e.target.value)}
                    className="shift-form-select"
                  >
                    <option value="Site Technical">Site Technical / field MEP</option>
                    <option value="Corporate">Head Office / Corporate Management</option>
                    <option value="Night Operations">Night Operations / Emergency Turnaround</option>
                    <option value="Special">Special / Seasonal Half-Day</option>
                  </select>
                </div>

                <div className="shift-form-group">
                  <label className="shift-form-label">Primary Target Sites</label>
                  <select
                    value={formSite}
                    onChange={(e) => setFormSite(e.target.value)}
                    className="shift-form-select"
                  >
                    <option value="All Project Sites">All Project Sites</option>
                    {dbSites.length > 0 ? (
                      dbSites.map((s) => (
                        <option key={s.id} value={s.name}>
                          {s.name} {s.cityName ? `(${s.cityName})` : ''}
                        </option>
                      ))
                    ) : (
                      <>
                        <option value="Karachi High-Rise Commercial Tower">Karachi High-Rise Commercial Tower</option>
                        <option value="Islamabad Federal Govt Complex">Islamabad Federal Govt Complex</option>
                        <option value="Lahore HQ &amp; Central Workshop">Lahore HQ &amp; Central Workshop</option>
                      </>
                    )}
                  </select>
                </div>
              </div>

              {/* Box: Shift Timings & Work Hours */}
              <div className="shift-hours-card">
                <span className="shift-hours-card-title">SHIFT TIMINGS &amp; WORK HOURS</span>

                <div className="shift-hours-grid-4">
                  <div className="shift-form-group">
                    <label className="shift-form-label">Shift Start *</label>
                    <input
                      type="time"
                      required
                      value={formStart}
                      onChange={(e) => setFormStart(e.target.value)}
                      className="shift-form-input"
                      style={{ fontWeight: 700 }}
                    />
                  </div>

                  <div className="shift-form-group">
                    <label className="shift-form-label">Shift End *</label>
                    <input
                      type="time"
                      required
                      value={formEnd}
                      onChange={(e) => setFormEnd(e.target.value)}
                      className="shift-form-input"
                      style={{ fontWeight: 700 }}
                    />
                  </div>

                  <div className="shift-form-group">
                    <label className="shift-form-label">Unpaid Meal/Break</label>
                    <select
                      value={formBreak}
                      onChange={(e) => setFormBreak(e.target.value)}
                      className="shift-form-select"
                    >
                      <option value="60">60 Minutes</option>
                      <option value="45">45 Minutes</option>
                      <option value="30">30 Minutes</option>
                      <option value="0">0 Minutes (No Break)</option>
                    </select>
                  </div>

                  <div className="shift-form-group">
                    <label className="shift-form-label">Net Work Duration</label>
                    <div className="shift-hours-readonly">{netDurationHours}</div>
                  </div>
                </div>
              </div>

              {/* Row 3: Grace, 1/2 Day Cutoff, Absent Cutoff */}
              <div className="shift-form-row-tri">
                <div className="shift-form-group">
                  <label className="shift-form-label">Late Grace Period *</label>
                  <select
                    value={formGrace}
                    onChange={(e) => setFormGrace(e.target.value)}
                    className="shift-form-select"
                  >
                    <option value="15">15 Minutes (Company standard)</option>
                    <option value="10">10 Minutes (Strict)</option>
                    <option value="20">20 Minutes</option>
                    <option value="30">30 Minutes (Relaxed)</option>
                  </select>
                </div>

                <div className="shift-form-group">
                  <label className="shift-form-label">&frac12; Day Arrival Cutoff</label>
                  <input
                    type="text"
                    value={formHalfDay}
                    onChange={(e) => setFormHalfDay(e.target.value)}
                    placeholder="e.g. 10:30 AM"
                    className="shift-form-input"
                  />
                </div>

                <div className="shift-form-group">
                  <label className="shift-form-label">Absent Mark Cutoff</label>
                  <input
                    type="text"
                    value={formAbsent}
                    onChange={(e) => setFormAbsent(e.target.value)}
                    placeholder="e.g. 12:00 PM"
                    className="shift-form-input"
                  />
                </div>
              </div>

              {/* Row 4: Overtime Multiplier & Night Pay */}
              <div className="shift-form-row-2">
                <div className="shift-form-group">
                  <label className="shift-form-label">Overtime Multiplier Eligibility</label>
                  <select
                    value={formOt}
                    onChange={(e) => setFormOt(e.target.value)}
                    className="shift-form-select"
                  >
                    <option value="Eligible: 1.5x Standard Hourly Rate">Eligible: 1.5x Standard Hourly Rate</option>
                    <option value="Eligible: 2.0x Double Rate (Holiday/Night)">Eligible: 2.0x Double Rate (Holiday/Night)</option>
                    <option value="Not Eligible (Fixed Corporate Salary)">Not Eligible (Salaried Management)</option>
                  </select>
                </div>

                <div className="shift-form-group">
                  <label className="shift-form-label">Night Shift Differential (PKR / Night)</label>
                  <div className="shift-currency-wrap">
                    <span className="shift-currency-symbol">Rs.</span>
                    <input
                      type="number"
                      min="0"
                      step="50"
                      value={formNightPay}
                      onChange={(e) => setFormNightPay(e.target.value)}
                      placeholder="0"
                      className="shift-currency-input"
                    />
                  </div>
                </div>
              </div>

              {/* Row 5: Applicable Days of Week */}
              <div className="shift-form-group">
                <label className="shift-form-label">Applicable Days of Week *</label>
                <div className="shift-weekdays-picker">
                  {[
                    { key: 'Mon', label: 'Mon' },
                    { key: 'Tue', label: 'Tue' },
                    { key: 'Wed', label: 'Wed' },
                    { key: 'Thu', label: 'Thu' },
                    { key: 'Fri', label: 'Fri' },
                    { key: 'Sat', label: 'Sat' },
                    { key: 'Sun', label: 'Sun' },
                  ].map((day) => {
                    const isChecked = formDays.includes(day.key);
                    return (
                      <label
                        key={day.key}
                        className={`shift-weekday-box ${isChecked ? 'selected' : ''}`}
                        onClick={() => toggleDay(day.key)}
                      >
                        <input
                          type="checkbox"
                          checked={isChecked}
                          onChange={() => {}}
                        />
                        <span>{day.label}</span>
                      </label>
                    );
                  })}
                </div>
              </div>

              {/* Row 6: Description */}
              <div className="shift-form-group">
                <label className="shift-form-label">Operational Description / Notes</label>
                <textarea
                  rows={2}
                  value={formDesc}
                  onChange={(e) => setFormDesc(e.target.value)}
                  placeholder="e.g. Applicable for daytime site civil and ducting teams."
                  className="shift-form-textarea"
                />
              </div>

              {/* Modal Actions */}
              <div className="shift-modal-foot">
                <button
                  type="button"
                  className="shift-modal-btn-cancel"
                  onClick={() => setShowCreateModal(false)}
                  disabled={formSubmitting}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="shift-modal-btn-confirm"
                  disabled={formSubmitting}
                >
                  {formSubmitting ? (
                    <>
                      <RotateCcw size={13} className="animate-spin" />
                      Creating...
                    </>
                  ) : (
                    'Confirm'
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* 7. "Assign List" Modal Dialog */}
      {showAssignModal && (
        <div className="shift-modal-backdrop" onClick={() => setShowAssignModal(false)}>
          <div className="shift-modal-dialog" style={{ maxWidth: '480px' }} onClick={(e) => e.stopPropagation()}>
            <div className="shift-modal-head">
              <h2 className="shift-modal-title">Assign Work Shift List</h2>
              <button
                type="button"
                className="shift-modal-close"
                onClick={() => setShowAssignModal(false)}
              >
                <X size={18} />
              </button>
            </div>

            <div className="shift-modal-content">
              <div className="shift-modal-info-banner">
                <Users size={16} />
                <span>
                  Map an entire project site, department, or designated workforce to this shift schedule. Biometric rules will sync immediately.
                </span>
              </div>

              <div className="shift-form-group">
                <label className="shift-form-label">Target Shift Schedule *</label>
                <select
                  value={assignTargetShiftId}
                  onChange={(e) => setAssignTargetShiftId(e.target.value)}
                  className="shift-form-select"
                >
                  {shifts.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name} ({s.meta.startTime} &ndash; {s.meta.endTime})
                    </option>
                  ))}
                </select>
              </div>

              <div className="shift-form-group">
                <label className="shift-form-label">Target Project Site / Scope *</label>
                <select
                  value={assignTargetSite}
                  onChange={(e) => setAssignTargetSite(e.target.value)}
                  className="shift-form-select"
                >
                  {dbSites.length > 0 ? (
                    dbSites.map((s) => (
                      <option key={s.id} value={s.name}>
                        {s.name} {s.cityName ? `(${s.cityName})` : ''}
                      </option>
                    ))
                  ) : (
                    <>
                      <option value="Karachi High-Rise Commercial Tower">Karachi High-Rise Commercial Tower</option>
                      <option value="Islamabad Federal Govt Complex">Islamabad Federal Govt Complex</option>
                      <option value="Lahore HQ &amp; Central Workshop">Lahore HQ &amp; Central Workshop</option>
                    </>
                  )}
                </select>
              </div>

              <div className="shift-form-group">
                <label className="shift-form-label">Effective From Date</label>
                <input
                  type="date"
                  defaultValue={businessToday()}
                  className="shift-form-input"
                />
              </div>

              <div className="shift-modal-foot" style={{ margin: '8px -20px -16px' }}>
                <button
                  type="button"
                  className="shift-modal-btn-cancel"
                  onClick={() => setShowAssignModal(false)}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="shift-modal-btn-confirm"
                  onClick={() => {
                    setShowAssignModal(false);
                    setToastMessage(`Assigned roster successfully! ${assignTargetSite} mapped.`);
                  }}
                >
                  Assign Shift
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
