import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import * as Primitive from '@radix-ui/react-dialog';
import { Building2, Check, ChevronDown, Shield, Wrench, X } from 'lucide-react';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { hrWrite } from '../../api/hr-api';
import { masterSchema } from '../../schemas/organization';
import { pageOf, employeeSchema } from '../../schemas/contracts';
import { siteSchema, type Site } from '../../schemas/sites';
import { InteractiveGeofenceMap } from '@/modules/attendance/components/geofences/InteractiveGeofenceMap';
import { useAttendanceQuery } from '@/modules/attendance/hooks/use-attendance-query';
import { shiftPage, shiftDetail } from '@/modules/attendance/schemas/shifts';
import { siteRosterHistory, rosterWrite } from '@/modules/attendance/schemas/site-rosters';
import { attendanceGet, attendanceWrite } from '@/modules/attendance/api/attendance-api';

const CITY_COORDINATES: Record<string, { lat: number; lon: number }> = {
  Faisalabad: { lat: 31.4504, lon: 73.135 },
  Lahore: { lat: 31.5204, lon: 74.3587 },
  Karachi: { lat: 24.8607, lon: 67.0011 },
  Islamabad: { lat: 33.6844, lon: 73.0479 },
  Rawalpindi: { lat: 33.5651, lon: 73.0169 },
  Multan: { lat: 30.1575, lon: 71.5249 },
  Peshawar: { lat: 34.0151, lon: 71.5249 },
  Sialkot: { lat: 32.4945, lon: 74.5229 },
  Gujranwala: { lat: 32.1877, lon: 74.1945 },
  Quetta: { lat: 30.1798, lon: 66.975 },
  Hyderabad: { lat: 25.396, lon: 68.3578 },
  Abbottabad: { lat: 34.1688, lon: 73.2215 },
};

const PROJECT_CATEGORIES = [
  'MEP High-Rise Building',
  'Industrial Substation',
  'Commercial Plaza',
  'Civil Infrastructure',
  'Solar & Renewable Plant',
  'Factory & Warehousing',
  'Residential Housing Scheme',
  'Corporate Regional Office',
  'Hospital & Healthcare Facility',
];

export interface CreateWorksiteModalProps {
  onClose: () => void;
  onSaved: (site: Site) => void;
}

export function CreateWorksiteModal({ onClose, onSaved }: CreateWorksiteModalProps) {
  const lifetime = useHrLifetime();
  const cache = useQueryClient();

  // Queries for active Pakistani cities and approved supervisors
  const citiesQuery = useHrQuery('cities?page=1&pageSize=30&isActive=true', pageOf(masterSchema), [
    'hr.organization.read',
  ]);

  const employeesQuery = useHrQuery(
    'employees?page=1&pageSize=50&status=2',
    pageOf(employeeSchema),
    ['hr.employees.read'],
  );

  const shiftsQuery = useAttendanceQuery('/hr/shifts?pageSize=50&isActive=true', shiftPage, true);

  // Form state matching Image 3 mockup
  const [siteName, setSiteName] = useState('');
  const [clientName, setClientName] = useState('');
  const [category, setCategory] = useState('MEP High-Rise Building');
  const [customCode, setCustomCode] = useState('');
  const [isCustomCodeOpen, setIsCustomCodeOpen] = useState(false);

  const [cityId, setCityId] = useState('');
  const [supervisorId, setSupervisorId] = useState('');
  const [assignedShiftId, setAssignedShiftId] = useState('');
  const [address, setAddress] = useState('');

  // Map & Coordinates state
  const [latitude, setLatitude] = useState(31.4504);
  const [longitude, setLongitude] = useState(73.135);
  const [radiusMeters, setRadiusMeters] = useState(100);

  // Submission state
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Auto-select Faisalabad or first available city
  useEffect(() => {
    if (citiesQuery.data?.items.length && !cityId) {
      const fsd = citiesQuery.data.items.find((c) => c.name.toLowerCase().includes('faisalabad'));
      const initial = fsd ?? citiesQuery.data.items[0];
      if (initial) {
        setCityId(initial.id);
        const coords = CITY_COORDINATES[initial.name] ?? { lat: 31.4504, lon: 73.135 };
        setLatitude(coords.lat);
        setLongitude(coords.lon);
      }
    }
  }, [citiesQuery.data, cityId]);

  // When city changes, update coordinates accordingly
  const handleCityChange = (newCityId: string) => {
    setCityId(newCityId);
    const selected = citiesQuery.data?.items.find((c) => c.id === newCityId);
    if (selected) {
      const coords = CITY_COORDINATES[selected.name];
      if (coords) {
        setLatitude(coords.lat);
        setLongitude(coords.lon);
      }
    }
  };

  // Generate clean site code
  const derivedCode = () => {
    if (customCode.trim()) return customCode.trim();
    const city = citiesQuery.data?.items.find((c) => c.id === cityId);
    const prefix = city ? city.code.toUpperCase() : 'SITE';
    const slug = siteName
      .trim()
      .replace(/[^A-Za-z0-9]/g, '-')
      .replace(/-+/g, '-')
      .replace(/^-|-$/g, '')
      .toUpperCase()
      .slice(0, 18);
    const suffix = slug ? `-${slug}` : `-${Math.floor(1000 + Math.random() * 9000)}`;
    return `${prefix}${suffix}`.slice(0, 30);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!siteName.trim()) {
      setErrorMessage('Please enter the Working Site / Project Name.');
      return;
    }
    if (!clientName.trim()) {
      setErrorMessage('Please enter the Client / Owner Name.');
      return;
    }
    if (!cityId) {
      setErrorMessage('Please select a City.');
      return;
    }
    if (!address.trim()) {
      setErrorMessage('Please enter the Complete Physical Site Address.');
      return;
    }

    const finalCode = derivedCode();
    if (!/^[A-Za-z0-9][A-Za-z0-9_.-]{0,29}$/.test(finalCode)) {
      setErrorMessage(
        'Site Code must be up to 30 characters using letters, digits, dots, hyphens or underscores.',
      );
      return;
    }

    const signal = lifetime();
    setSubmitting(true);

    try {
      // Step 1: Create the Site record
      const sitePayload = {
        code: finalCode,
        name: siteName.trim(),
        address: address.trim(),
        cityId: cityId,
        clientName: clientName.trim() || null,
        projectId: null,
        startsOn: new Date().toISOString().slice(0, 10),
        endsOn: null,
        supervisorEmployeeId: supervisorId || null,
      };

      const siteRaw = await hrWrite('sites', sitePayload, 'POST', crypto.randomUUID(), signal);
      const createdSite = siteSchema.parse(siteRaw);

      // Step 2: Publish the initial Geofence boundary
      // Backend enforces that EffectiveFrom must be strictly greater than today
      const tomorrow = new Date();
      tomorrow.setDate(tomorrow.getDate() + 1);
      const effectiveFromStr = tomorrow.toISOString().slice(0, 10);

      const geofencePayload = {
        effectiveFrom: effectiveFromStr,
        latitude: Number(latitude.toFixed(7)),
        longitude: Number(longitude.toFixed(7)),
        radiusMeters: Number(radiusMeters.toFixed(2)),
        maximumAccuracyMeters: 25,
        expectedSiteVersion: createdSite.version,
        expectedLatestVersionNumber: 0,
        managementApproved: true,
        approvalReference: 'Auto-calibrated at Initial Site Setup',
        reason: `Initial boundary & geofence perimeter for ${createdSite.name}`,
      };

      try {
        await hrWrite(
          `sites/${createdSite.id}/geofences/versions`,
          geofencePayload,
          'POST',
          crypto.randomUUID(),
          signal,
        );
      } catch (geoError) {
        console.warn('Geofence auto-publish notice:', geoError);
        // Site was already created, boundary can still be updated manually if needed
      }

      // Step 3: Assign initial Operational Shift to site if selected
      if (assignedShiftId) {
        try {
          const shiftDetailData = await attendanceGet(
            `/hr/shifts/${assignedShiftId}`,
            shiftDetail,
            signal,
          );
          const history = await attendanceGet(
            `/hr/sites/${createdSite.id}/shift-assignments`,
            siteRosterHistory,
            signal,
          );
          await attendanceWrite(
            `/hr/sites/${createdSite.id}/shift-assignments`,
            {
              shiftId: assignedShiftId,
              effectiveFrom: effectiveFromStr,
              effectiveTo: null,
              holidayCalendarId: null,
              expectedTimeline: history.timeline,
              expectedShiftVersion: shiftDetailData.version,
              reason: `Initial operational shift assigned during site setup for ${createdSite.name}`,
            },
            'POST',
            rosterWrite,
            crypto.randomUUID(),
            signal,
          );
        } catch (shiftErr) {
          console.warn('Initial site shift assignment notice:', shiftErr);
        }
      }

      await cache.invalidateQueries({ queryKey: ['hr'] });
      await cache.invalidateQueries({ queryKey: ['attendance'] });

      onSaved(createdSite);
    } catch (err: unknown) {
      if (err instanceof Error) {
        setErrorMessage(err.message);
      } else {
        setErrorMessage('Failed to create working site. Please review input values.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  const cities = citiesQuery.data?.items ?? [];
  const employees = employeesQuery.data?.items ?? [];

  return (
    <Primitive.Root open onOpenChange={(open) => !open && !submitting && onClose()}>
      <Primitive.Portal>
        <Primitive.Overlay className="dialog-overlay" />
        <Primitive.Content className="dialog-content worksite-modal-container">
          {/* Modal Header matching reference mockup */}
          <div className="worksite-modal-header">
            <div className="worksite-modal-title-wrap">
              <div className="worksite-header-icon-badge">
                <Building2 size={20} />
              </div>
              <div>
                <Primitive.Title className="worksite-modal-title">
                  Add New Working Site & Geofence
                </Primitive.Title>
                <Primitive.Description className="worksite-modal-subtitle">
                  Configure site location, Project Manager, GPS radius & Biometric hardware
                </Primitive.Description>
              </div>
            </div>
            <Primitive.Close
              className="icon-button worksite-close-btn"
              aria-label="Close dialog"
              disabled={submitting}
            >
              <X size={18} />
            </Primitive.Close>
          </div>

          {errorMessage && (
            <div
              role="alert"
              className="hr-message hr-message--error"
              style={{ margin: '14px 0 6px' }}
            >
              {errorMessage}
            </div>
          )}

          <form onSubmit={handleSubmit} className="worksite-modal-form">
            {/* 1. Site Identity & Client Information */}
            <section className="worksite-form-card">
              <div className="worksite-card-title">
                <span className="card-emoji-icon" aria-hidden="true">
                  🏢
                </span>
                <span>1. Site Identity & Client Information</span>
              </div>

              <div className="worksite-card-body">
                <div className="form-group">
                  <label className="worksite-label">
                    Working Site / Project Name <span className="req">*</span>
                  </label>
                  <input
                    type="text"
                    className="worksite-input"
                    placeholder="e.g. Faisalabad Textile Industrial Substation"
                    value={siteName}
                    onChange={(e) => setSiteName(e.target.value)}
                    required
                    maxLength={150}
                    disabled={submitting}
                  />
                </div>

                <div className="worksite-two-col-grid">
                  <div className="form-group">
                    <label className="worksite-label">
                      Client / Owner Name <span className="req">*</span>
                    </label>
                    <input
                      type="text"
                      className="worksite-input"
                      placeholder="e.g. Crescent Bahuman Ltd"
                      value={clientName}
                      onChange={(e) => setClientName(e.target.value)}
                      required
                      maxLength={200}
                      disabled={submitting}
                    />
                  </div>

                  <div className="form-group">
                    <label className="worksite-label">Project Category</label>
                    <div className="select-wrapper">
                      <select
                        className="worksite-select"
                        value={category}
                        onChange={(e) => setCategory(e.target.value)}
                        disabled={submitting}
                      >
                        {PROJECT_CATEGORIES.map((cat) => (
                          <option key={cat} value={cat}>
                            {cat}
                          </option>
                        ))}
                      </select>
                      <ChevronDown size={15} className="select-caret" />
                    </div>
                  </div>
                </div>

                {/* Optional Site Code Details */}
                <div className="worksite-code-preview-row">
                  <div className="code-text">
                    Site Code: <code>{derivedCode()}</code>
                  </div>
                  <button
                    type="button"
                    className="btn-text-action"
                    onClick={() => setIsCustomCodeOpen(!isCustomCodeOpen)}
                  >
                    {isCustomCodeOpen ? 'Use Auto Code' : 'Customize Code'}
                  </button>
                  {isCustomCodeOpen && (
                    <input
                      type="text"
                      className="worksite-input input-sm"
                      placeholder="CUSTOM-CODE-01"
                      value={customCode}
                      onChange={(e) => setCustomCode(e.target.value.toUpperCase())}
                      maxLength={30}
                    />
                  )}
                </div>
              </div>
            </section>

            {/* 2. Location & Site Incharge */}
            <section className="worksite-form-card">
              <div className="worksite-card-title">
                <span className="card-emoji-icon" aria-hidden="true">
                  📍
                </span>
                <span>2. Location & Site Incharge</span>
              </div>

              <div className="worksite-card-body">
                <div className="worksite-two-col-grid">
                  <div className="form-group">
                    <label className="worksite-label">
                      City <span className="req">*</span>
                    </label>
                    <div className="select-wrapper">
                      <select
                        className="worksite-select"
                        value={cityId}
                        onChange={(e) => handleCityChange(e.target.value)}
                        required
                        disabled={submitting}
                      >
                        <option value="">Select City</option>
                        {cities.map((city) => (
                          <option key={city.id} value={city.id}>
                            {city.name}
                          </option>
                        ))}
                      </select>
                      <ChevronDown size={15} className="select-caret" />
                    </div>
                  </div>

                  <div className="form-group">
                    <label className="worksite-label">
                      Project Manager / Incharge <span className="req">*</span>
                    </label>
                    <div className="select-wrapper">
                      <select
                        className="worksite-select"
                        value={supervisorId}
                        onChange={(e) => setSupervisorId(e.target.value)}
                        disabled={submitting}
                      >
                        <option value="">e.g. Engr. Imran Saeed</option>
                        {employees.map((emp) => (
                          <option key={emp.id} value={emp.id}>
                            {emp.name ?? `Employee #${emp.employeeNumber}`} (#{emp.employeeNumber})
                          </option>
                        ))}
                      </select>
                      <ChevronDown size={15} className="select-caret" />
                    </div>
                  </div>

                  <div className="form-group">
                    <label className="worksite-label">Assigned Operational Shift</label>
                    <div className="select-wrapper">
                      <select
                        className="worksite-select"
                        value={assignedShiftId}
                        onChange={(e) => setAssignedShiftId(e.target.value)}
                        disabled={submitting}
                      >
                        <option value="">Select or leave for default day shift...</option>
                        {shiftsQuery.data?.items?.map((s) => (
                          <option key={s.id} value={s.id}>
                            {s.code} - {s.name}
                          </option>
                        ))}
                      </select>
                      <ChevronDown size={15} className="select-caret" />
                    </div>
                  </div>
                </div>

                <div className="form-group">
                  <label className="worksite-label">
                    Complete Physical Site Address <span className="req">*</span>
                  </label>
                  <input
                    type="text"
                    className="worksite-input"
                    placeholder="e.g. Plot 45, Khurrianwala Industrial Estate, Faisalabad"
                    value={address}
                    onChange={(e) => setAddress(e.target.value)}
                    required
                    maxLength={500}
                    disabled={submitting}
                  />
                </div>
              </div>
            </section>

            {/* 3. GPS Coordinates & Geofencing Map */}
            <section className="worksite-form-card">
              <div className="worksite-card-title worksite-card-title--with-badge">
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <Wrench size={16} color="#2563eb" />
                  <span>3. GPS Coordinates & Geofencing Map</span>
                </div>
                <div className="google-picker-badge">
                  <span className="g-logo">G</span> Google Map Picker
                </div>
              </div>

              <div className="worksite-card-body">
                {/* Interactive Map Picker with Geofence overlay */}
                <InteractiveGeofenceMap
                  latitude={latitude}
                  longitude={longitude}
                  radiusMeters={radiusMeters}
                  onCoordinatesChange={(lat, lon) => {
                    setLatitude(lat);
                    setLongitude(lon);
                  }}
                  onRadiusChange={setRadiusMeters}
                />

                {/* Coordinate Fields Grid */}
                <div className="worksite-coords-grid">
                  <div className="form-group">
                    <label className="worksite-label">Latitude</label>
                    <input
                      type="number"
                      step="0.0000001"
                      className="worksite-input"
                      value={latitude}
                      onChange={(e) => setLatitude(Number(e.target.value))}
                      disabled={submitting}
                    />
                  </div>
                  <div className="form-group">
                    <label className="worksite-label">Longitude</label>
                    <input
                      type="number"
                      step="0.0000001"
                      className="worksite-input"
                      value={longitude}
                      onChange={(e) => setLongitude(Number(e.target.value))}
                      disabled={submitting}
                    />
                  </div>
                  <div className="form-group">
                    <label className="worksite-label">Radial Perimeter (m)</label>
                    <input
                      type="number"
                      step="1"
                      min="10"
                      max="10000"
                      className="worksite-input"
                      value={radiusMeters}
                      onChange={(e) => setRadiusMeters(Number(e.target.value))}
                      disabled={submitting}
                    />
                  </div>
                </div>
              </div>
            </section>

            {/* Modal Footer matching reference mockup */}
            <div className="worksite-modal-footer">
              <div className="footer-status-pill">
                <Shield size={14} className="status-shield-icon" />
                <span>WILL ACTIVATE SITE GPS & LIST</span>
              </div>

              <div className="footer-actions-group">
                <button
                  type="button"
                  className="worksite-btn-cancel"
                  onClick={onClose}
                  disabled={submitting}
                >
                  Cancel
                </button>
                <button type="submit" className="worksite-btn-submit" disabled={submitting}>
                  <Check size={16} />
                  <span>{submitting ? 'Creating Site & GPS...' : 'Create Working Site'}</span>
                </button>
              </div>
            </div>
          </form>
        </Primitive.Content>
      </Primitive.Portal>
    </Primitive.Root>
  );
}
