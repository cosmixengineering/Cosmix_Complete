import { useState, type FormEvent } from 'react';
import {
  Calendar,
  Car,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  FileText,
  Fuel,
  KeyRound,
  Plus,
  RefreshCw,
  Route,
  ShieldAlert,
  Users,
  Wrench,
  X,
} from 'lucide-react';
import { Denied, QueryState, ActionMessage } from '../components/Feedback';
import { FinanceStatus } from '../components/finance/FinanceChrome';
import {
  approvalStateOptions,
  journeyPurposeOptions,
  serviceKindOptions,
} from '../components/finance/labels';
import { useFinanceAccess } from '../hooks/use-finance-access';
import { useFinanceAction, useFinanceQuery } from '../hooks/use-finance-queries';
import { queryString } from '../api/hr-finance-api';
import { labelDate } from '../utils/messages';
import { VehiclePolicyManagement } from '../components/vehicle-management/VehiclePolicyManagement';
import { VehicleMonthlyManagement } from '../components/vehicle-management/VehicleMonthlyManagement';
import { VehicleIncidentManagement } from '../components/vehicle-management/VehicleIncidentManagement';
import { VehicleTripAttribution } from '../components/vehicle-management/VehicleTripAttribution';
import {
  VehicleManagementIntents,
  useVehicleNavigation,
} from '../components/vehicle-management/VehicleManagementShared';
import { vehicleManagementPermissions } from '../api/vehicle-management';
import { LiveFuelRatesWidget } from '../components/vehicle-management/LiveFuelRatesWidget';
import '@/styles/vehicle-management.css';
import {
  commuteDistance,
  custodyList,
  fuelClaim,
  fuelClaimPage,
  vehicle,
  vehicleCustody,
  vehicleIncidentPage,
  vehicleList,
  vehicleService,
  vehicleServicePage,
  vehicleTrip,
  vehicleTripPage,
  type Vehicle,
  type VehicleCustody,
} from '../schemas/finance';

const permissions = {
  manage: 'attendance.vehicle.manage',
  custody: 'attendance.vehicle.custody.manage',
  trips: 'attendance.vehicle.trips.manage',
  fuel: 'attendance.vehicle.fuel.manage',
  maintenance: 'attendance.vehicle.maintenance.manage',
  incident: 'attendance.vehicle.incident.manage',
  commute: 'attendance.vehicle.commute.manage',
};

export function VehiclesPage() {
  const access = useFinanceAccess();
  return (
    <VehicleManagementIntents key={JSON.stringify([access.subject, access.user])}>
      <VehiclesManagementWorkspace />
    </VehicleManagementIntents>
  );
}

function VehiclesManagementWorkspace() {
  const access = useFinanceAccess();
  const navigation = useVehicleNavigation();
  const [tab, setTab] = useState('trips');

  const canTrips = access.can(permissions.trips);
  const canFuel = access.can(permissions.fuel);
  const canMaintenance = access.can(permissions.maintenance);
  const canCustody = access.can(permissions.custody);
  const canIncident = access.can(permissions.incident);
  const canManage = access.can(permissions.manage);
  const canCommute = access.can(permissions.commute);
  const canPolicy = access.can(vehicleManagementPermissions.policy);

  // Stats for Executive KPI Cards
  const vehiclesCountQuery = useFinanceQuery('/hr/vehicles', vehicleList, canManage);
  const pendingTripsQuery = useFinanceQuery(
    '/hr/vehicles/trips?state=0&pageSize=1',
    vehicleTripPage,
    canTrips,
  );
  const pendingFuelQuery = useFinanceQuery(
    '/hr/vehicles/fuel-claims?state=0&pageSize=1',
    fuelClaimPage,
    canFuel,
  );
  const custodyHistoryQuery = useFinanceQuery(
    '/hr/vehicles/custody/history',
    custodyList,
    canCustody || canManage,
  );

  const tabs = [
    ...(canTrips ? [{ id: 'trips', label: 'Trips', icon: Car }] : []),
    ...(canFuel || canCommute ? [{ id: 'fuel', label: 'Fuel Claims', icon: Fuel }] : []),
    ...(canMaintenance ? [{ id: 'maintenance', label: 'Maintenance', icon: Wrench }] : []),
    ...(canCustody || canManage
      ? [{ id: 'custody', label: 'Custody & Fleet', icon: KeyRound }]
      : []),
    ...(canIncident ? [{ id: 'incidents', label: 'Incidents', icon: ShieldAlert }] : []),
    ...(canFuel ? [{ id: 'monthly', label: 'Monthly Adjustments', icon: Calendar }] : []),
    ...(canPolicy ? [{ id: 'policies', label: 'Policies', icon: FileText }] : []),
  ];

  const activeTab = tabs.some((item) => item.id === tab) ? tab : (tabs[0]?.id ?? '');

  if (
    !canTrips &&
    !canFuel &&
    !canMaintenance &&
    !canCustody &&
    !canIncident &&
    !canManage &&
    !canCommute &&
    !canPolicy
  )
    return <Denied />;

  const activeCustodyCount = (custodyHistoryQuery.data ?? []).filter((c) => c.isActive).length;

  return (
    <section className="vehicles-page-container" aria-label="Vehicles and fleet management">
      {/* Top Header & Breadcrumb Ribbon */}
      <header className="veh-header-container">
        <div>
          <div className="veh-breadcrumb">
            <span style={{ color: '#64748b' }}>Human Resources</span>
            <span>/</span>
            <span style={{ color: '#0f172a', fontWeight: 600 }}>Vehicles & Fleet Management</span>
          </div>
          <div className="veh-title-row">
            <h1 className="veh-main-title">Vehicles & Fleet Management</h1>
          </div>
          <p className="veh-subtitle">
            Audit trip intake, approve fuel reimbursement claims, monitor vehicle custody, and track
            maintenance.
          </p>
        </div>

        <div className="veh-top-actions">
          <button
            type="button"
            className="btn-veh-secondary"
            onClick={() => {
              void vehiclesCountQuery.refetch();
              void pendingTripsQuery.refetch();
              void pendingFuelQuery.refetch();
              void custodyHistoryQuery.refetch();
            }}
            title="Refresh fleet statistics"
          >
            <RefreshCw size={13} className={pendingTripsQuery.isFetching ? 'spin' : ''} />
            <span>Refresh</span>
          </button>
        </div>
      </header>

      {/* 4 Executive KPI Cards */}
      <div className="veh-kpis-grid">
        <div className="veh-kpi-card">
          <div className="veh-kpi-icon-wrap veh-bg-blue">
            <Car size={20} />
          </div>
          <div className="veh-kpi-content">
            <span className="veh-kpi-label">Registered Fleet</span>
            <div className="veh-kpi-value">{vehiclesCountQuery.data?.length ?? 0} Vehicles</div>
            <span className="veh-kpi-sub">Total company pool & assigned</span>
          </div>
        </div>

        <div className="veh-kpi-card">
          <div className="veh-kpi-icon-wrap veh-bg-amber">
            <Route size={20} />
          </div>
          <div className="veh-kpi-content">
            <span className="veh-kpi-label">Pending Trips</span>
            <div className="veh-kpi-value" style={{ color: '#b45309' }}>
              {pendingTripsQuery.data?.items.length ?? 0} Trips
            </div>
            <span className="veh-kpi-sub">Awaiting verification & approval</span>
          </div>
        </div>

        <div className="veh-kpi-card">
          <div className="veh-kpi-icon-wrap veh-bg-emerald">
            <Fuel size={20} />
          </div>
          <div className="veh-kpi-content">
            <span className="veh-kpi-label">Fuel Claims</span>
            <div className="veh-kpi-value" style={{ color: '#047857' }}>
              {pendingFuelQuery.data?.items.length ?? 0} Pending
            </div>
            <span className="veh-kpi-sub">Reimbursement settlement queue</span>
          </div>
        </div>

        <div className="veh-kpi-card">
          <div className="veh-kpi-icon-wrap veh-bg-purple">
            <KeyRound size={20} />
          </div>
          <div className="veh-kpi-content">
            <span className="veh-kpi-label">Active Custody</span>
            <div className="veh-kpi-value" style={{ color: '#7c3aed' }}>
              {activeCustodyCount} In Field
            </div>
            <span className="veh-kpi-sub">Vehicles currently in employee hands</span>
          </div>
        </div>
      </div>

      {/* Tabs Navigation Strip */}
      <div className="veh-nav-tabs" role="tablist" aria-label="Vehicle views">
        {tabs.map((t) => {
          const Icon = t.icon;
          const isActive = activeTab === t.id;
          return (
            <button
              type="button"
              key={t.id}
              role="tab"
              aria-selected={isActive}
              className={`veh-tab-btn ${isActive ? 'active' : ''}`}
              onClick={() => {
                if (t.id !== activeTab) navigation.navigate(() => setTab(t.id));
              }}
            >
              <Icon size={14} />
              <span>{t.label}</span>
            </button>
          );
        })}
      </div>

      {/* Tab Workspaces */}
      {activeTab === 'trips' && <VehicleTrips canManage={canTrips} canVehicles={canManage} />}
      {activeTab === 'fuel' && (
        <VehicleFuel canManage={canFuel} canCommute={canCommute} canVehicles={canManage} />
      )}
      {activeTab === 'maintenance' && (
        <VehicleMaintenance canManage={canMaintenance} canVehicles={canManage} />
      )}
      {activeTab === 'custody' && (
        <VehicleCustodyPanel canManage={canCustody} canVehicles={canManage} />
      )}
      {activeTab === 'incidents' && <VehicleIncidents canManage={canIncident} />}
      {activeTab === 'monthly' && <VehicleMonthlyManagement />}
      {activeTab === 'policies' && <VehiclePolicyManagement />}
    </section>
  );
}

function VehicleTrips({ canManage, canVehicles }: { canManage: boolean; canVehicles: boolean }) {
  const navigation = useVehicleNavigation();
  const [page, setPage] = useState(1);
  const [state, setState] = useState('0'); // Default: Pending (0)
  const [employeeId, setEmployeeId] = useState('');
  const [vehicleId, setVehicleId] = useState('');
  const [selectedTripId, setSelectedTripId] = useState('');
  const action = useFinanceAction();

  const trips = useFinanceQuery(
    '/hr/vehicles/trips' +
      queryString({
        employeeId: employeeId.trim() || undefined,
        vehicleId: vehicleId.trim() || undefined,
        state: state === '' ? undefined : Number(state),
        page,
        pageSize: 20,
      }),
    vehicleTripPage,
    canManage,
  );

  async function approve(tripId: string) {
    if (navigation.retained) return;
    if (!window.confirm('Approve this trip log for vehicle audit and fuel eligibility?')) return;
    const result = await action.run(
      `/hr/vehicles/trips/${tripId}/approve`,
      null,
      'POST',
      vehicleTrip,
    );
    if (result) void trips.refetch();
  }

  return (
    <div className="veh-card">
      <div className="veh-card-header">
        <div>
          <h2 className="veh-card-title">
            <Route size={16} color="#2563eb" />
            <span>Vehicle Trip Logs & Audit Queue</span>
          </h2>
          <p className="veh-card-desc">
            Review official and inter-site journeys, verify GPS map distance vs odometer logs, and
            authorize trips.
          </p>
          {!canVehicles && (
            <p className="muted" style={{ fontSize: '11px', color: '#dc2626', marginTop: 4 }}>
              Vehicle registration and custody administration need the vehicle management
              permission.
            </p>
          )}
        </div>
      </div>

      {/* Filter Bar */}
      <div className="veh-filter-wrap">
        <div className="veh-presets-bar">
          <span style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>Quick Status:</span>
          <button
            type="button"
            className={`preset-chip-btn ${state === '0' ? 'active' : ''}`}
            onClick={() => {
              navigation.navigate(() => {
                setState('0');
                setPage(1);
              });
            }}
          >
            Pending Trips Only
          </button>
          <button
            type="button"
            className={`preset-chip-btn ${state === '1' ? 'active' : ''}`}
            onClick={() => {
              navigation.navigate(() => {
                setState('1');
                setPage(1);
              });
            }}
          >
            Approved Trips
          </button>
          <button
            type="button"
            className={`preset-chip-btn ${state === '' ? 'active' : ''}`}
            onClick={() => {
              navigation.navigate(() => {
                setState('');
                setPage(1);
              });
            }}
          >
            All States
          </button>
        </div>

        <div className="veh-filter-inputs-grid">
          <div className="veh-form-group">
            <label className="veh-form-label">Approval State</label>
            <select
              className="veh-select"
              disabled={navigation.pending}
              value={state}
              onChange={(e) => {
                navigation.navigate(() => {
                  setState(e.target.value);
                  setPage(1);
                });
              }}
            >
              <option value="">All States</option>
              {approvalStateOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          <div className="veh-form-group">
            <label className="veh-form-label">Employee ID (UUID)</label>
            <div style={{ position: 'relative' }}>
              <input
                type="text"
                className="veh-input"
                style={{ width: '100%', paddingRight: employeeId ? '28px' : '10px' }}
                placeholder="Filter by employee..."
                disabled={navigation.pending}
                value={employeeId}
                onChange={(e) => {
                  navigation.navigate(() => {
                    setEmployeeId(e.target.value);
                    setPage(1);
                  });
                }}
              />
              {employeeId && (
                <button
                  type="button"
                  onClick={() => {
                    navigation.navigate(() => {
                      setEmployeeId('');
                      setPage(1);
                    });
                  }}
                  style={{
                    position: 'absolute',
                    right: '6px',
                    top: '50%',
                    transform: 'translateY(-50%)',
                    background: 'none',
                    border: 'none',
                    color: '#94a3b8',
                    cursor: 'pointer',
                    padding: 0,
                  }}
                  title="Clear employee filter"
                >
                  <X size={14} />
                </button>
              )}
            </div>
          </div>

          <div className="veh-form-group">
            <label className="veh-form-label">Vehicle ID (UUID)</label>
            <div style={{ position: 'relative' }}>
              <input
                type="text"
                className="veh-input"
                style={{ width: '100%', paddingRight: vehicleId ? '28px' : '10px' }}
                placeholder="Filter by vehicle..."
                disabled={navigation.pending}
                value={vehicleId}
                onChange={(e) => {
                  navigation.navigate(() => {
                    setVehicleId(e.target.value);
                    setPage(1);
                  });
                }}
              />
              {vehicleId && (
                <button
                  type="button"
                  onClick={() => {
                    navigation.navigate(() => {
                      setVehicleId('');
                      setPage(1);
                    });
                  }}
                  style={{
                    position: 'absolute',
                    right: '6px',
                    top: '50%',
                    transform: 'translateY(-50%)',
                    background: 'none',
                    border: 'none',
                    color: '#94a3b8',
                    cursor: 'pointer',
                    padding: 0,
                  }}
                  title="Clear vehicle filter"
                >
                  <X size={14} />
                </button>
              )}
            </div>
          </div>

          <div style={{ paddingBottom: '2px' }}>
            {(state !== '' || employeeId || vehicleId) && (
              <button
                type="button"
                className="btn-veh-secondary"
                style={{ height: '30px', fontSize: '11.5px' }}
                onClick={() => {
                  navigation.navigate(() => {
                    setState('0');
                    setEmployeeId('');
                    setVehicleId('');
                    setPage(1);
                  });
                }}
              >
                Reset Filters
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Trips List / Empty State */}
      <QueryState query={trips}>
        {!trips.data?.items.length ? (
          <div className="veh-empty-state">
            <div className="veh-empty-icon">
              <Car size={24} />
            </div>
            <h4 className="veh-empty-title">No vehicle trips match this filter</h4>
            <p className="veh-empty-desc">
              There are currently no logged trips matching the selected criteria. When employees
              record journeys via the mobile app or terminal, they will be queued here.
            </p>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button
                type="button"
                className="btn-veh-primary"
                onClick={() => {
                  navigation.navigate(() => {
                    setState('');
                    setEmployeeId('');
                    setVehicleId('');
                    setPage(1);
                  });
                }}
              >
                Show All Trips
              </button>
            </div>
          </div>
        ) : (
          <div className="veh-items-list">
            {trips.data?.items.map((trip) => {
              const purposeLabel =
                journeyPurposeOptions[trip.purpose - 1]?.label ?? `Purpose #${trip.purpose}`;
              const isSelected = selectedTripId === trip.id;

              return (
                <article className="veh-item-card" key={trip.id}>
                  <div className="veh-item-top">
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '8px',
                        flexWrap: 'wrap',
                      }}
                    >
                      <span className="veh-reg-pill">
                        <Car size={13} />
                        <span>{trip.registrationNumber}</span>
                      </span>
                      <span className="veh-purpose-pill">{purposeLabel}</span>
                      <span
                        className={`veh-badge ${
                          trip.approvalState === 'Approved'
                            ? 'veh-badge-approved'
                            : trip.approvalState === 'Pending'
                              ? 'veh-badge-pending'
                              : 'veh-badge-active'
                        }`}
                      >
                        <FinanceStatus value={trip.approvalState} />
                      </span>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <button
                        type="button"
                        className={isSelected ? 'btn-veh-primary' : 'btn-veh-secondary'}
                        style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                        disabled={navigation.pending}
                        onClick={() => {
                          navigation.navigate(() => setSelectedTripId(isSelected ? '' : trip.id));
                        }}
                      >
                        {isSelected ? 'Close Details' : 'Attribution & History'}
                      </button>
                      {trip.approvalState === 'Pending' && (
                        <button
                          type="button"
                          className="btn-veh-success"
                          disabled={navigation.retained}
                          onClick={() => void approve(trip.id)}
                        >
                          <CheckCircle2 size={13} />
                          <span>Approve Trip</span>
                        </button>
                      )}
                    </div>
                  </div>

                  {/* Route & Driver Row */}
                  <div
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      flexWrap: 'wrap',
                      gap: '8px',
                    }}
                  >
                    <div className="veh-route-display">
                      <span>{trip.originLabel}</span>
                      <span className="veh-route-arrow">➔</span>
                      <span>{trip.destinationLabel}</span>
                    </div>
                    <div
                      style={{
                        fontSize: '12px',
                        color: '#475569',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '5px',
                      }}
                    >
                      <Users size={13} color="#64748b" />
                      <span style={{ fontWeight: 600 }}>{trip.employeeName}</span>
                    </div>
                  </div>

                  {/* Metrics Grid */}
                  <div className="veh-card-metrics-grid">
                    <div className="veh-metric-item">
                      <dt>Trip Distance</dt>
                      <dd style={{ color: '#2563eb', fontWeight: 700 }}>
                        {trip.tripDistanceKm ? `${trip.tripDistanceKm} km` : 'N/A'}
                      </dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>GPS Map Distance</dt>
                      <dd>{trip.mapDistanceKm ? `${trip.mapDistanceKm} km` : 'N/A'}</dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>Odometer Start</dt>
                      <dd>{trip.odometerStartKm ? `${trip.odometerStartKm} km` : 'N/A'}</dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>Odometer End</dt>
                      <dd>{trip.odometerEndKm ? `${trip.odometerEndKm} km` : 'N/A'}</dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>Started At</dt>
                      <dd>{labelDate(trip.startedAt)}</dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>Ended At</dt>
                      <dd>{labelDate(trip.endedAt)}</dd>
                    </div>
                  </div>

                  {/* Selected Trip Attribution Drawer */}
                  {isSelected && (
                    <div
                      style={{
                        marginTop: '8px',
                        borderTop: '1px solid #e2e8f0',
                        paddingTop: '12px',
                      }}
                    >
                      <VehicleTripAttribution trip={trip} onChanged={() => void trips.refetch()} />
                    </div>
                  )}
                </article>
              );
            })}

            {/* Pagination Controls */}
            {trips.data && (
              <div className="veh-pagination-bar">
                <span>Page {page} of results</span>
                <div style={{ display: 'flex', gap: '6px' }}>
                  <button
                    type="button"
                    className="btn-veh-secondary"
                    style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                    disabled={trips.isFetching || navigation.pending || page <= 1}
                    onClick={() => navigation.navigate(() => setPage((v) => Math.max(1, v - 1)))}
                  >
                    <ChevronLeft size={14} />
                    Previous
                  </button>
                  <button
                    type="button"
                    className="btn-veh-secondary"
                    style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                    disabled={trips.isFetching || navigation.pending || !trips.data.hasMore}
                    onClick={() => navigation.navigate(() => setPage((v) => v + 1))}
                  >
                    Next
                    <ChevronRight size={14} />
                  </button>
                </div>
              </div>
            )}
          </div>
        )}
      </QueryState>

      <ActionMessage action={action} />
    </div>
  );
}

function VehicleFuel({
  canManage,
  canCommute,
  canVehicles,
}: {
  canManage: boolean;
  canCommute: boolean;
  canVehicles: boolean;
}) {
  const vehicles = useFinanceQuery('/hr/vehicles', vehicleList, canVehicles);

  const [commuteForm, setCommuteForm] = useState({
    employeeId: '',
    effectiveFrom: '',
    effectiveTo: '',
    oneWayKm: '',
    dailyRoundTripKm: '',
    routeReference: '',
  });
  const [page, setPage] = useState(1);
  const [state, setState] = useState('0');
  const [employeeId, setEmployeeId] = useState('');
  const [isCommuteOpen, setIsCommuteOpen] = useState(false);
  const action = useFinanceAction();

  const claims = useFinanceQuery(
    '/hr/vehicles/fuel-claims' +
      queryString({
        employeeId: employeeId.trim() || undefined,
        state: state === '' ? undefined : Number(state),
        page,
        pageSize: 20,
      }),
    fuelClaimPage,
    canManage,
  );

  async function approveClaim(claimId: string) {
    if (!window.confirm('Approve this fuel reimbursement claim for payroll settlement?')) return;
    const result = await action.run(
      `/hr/vehicles/fuel-claims/${claimId}/approve`,
      null,
      'POST',
      fuelClaim,
    );
    if (result) void claims.refetch();
  }

  async function submitCommute(event: FormEvent) {
    event.preventDefault();
    const result = await action.run(
      '/hr/vehicles/commute',
      {
        employeeId: commuteForm.employeeId,
        effectiveFrom: commuteForm.effectiveFrom,
        effectiveTo: commuteForm.effectiveTo || null,
        oneWayKm: Number(commuteForm.oneWayKm),
        dailyRoundTripKm: Number(commuteForm.dailyRoundTripKm),
        routeReference: commuteForm.routeReference,
      },
      'POST',
      commuteDistance,
    );
    if (result) {
      setCommuteForm({
        employeeId: '',
        effectiveFrom: '',
        effectiveTo: '',
        oneWayKm: '',
        dailyRoundTripKm: '',
        routeReference: '',
      });
      setIsCommuteOpen(false);
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
      {/* Live OGRA Fuel Rates & Calculator */}
      <LiveFuelRatesWidget />

      {/* Collapsible Baseline Commute Distance Form */}
      {canCommute && (
        <div className="veh-collapsible-card">
          <div
            className="veh-collapsible-header"
            onClick={() => setIsCommuteOpen((o) => !o)}
            role="button"
            tabIndex={0}
            onKeyDown={(e) => e.key === 'Enter' && setIsCommuteOpen((o) => !o)}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Route size={16} color="#2563eb" />
              <span style={{ fontSize: '13px', fontWeight: 700, color: '#0f172a' }}>
                Baseline Commute Distance Configuration
              </span>
              <span style={{ fontSize: '11px', color: '#64748b' }}>
                (Baseline commute is deducted from eligible fuel reimbursement claims)
              </span>
            </div>
            <button
              type="button"
              className="btn-veh-secondary"
              style={{ height: '26px', padding: '0 8px', fontSize: '11px' }}
            >
              {isCommuteOpen ? <ChevronUp size={13} /> : <Plus size={13} />}
              <span>{isCommuteOpen ? 'Collapse' : '+ Set Commute Baseline'}</span>
            </button>
          </div>

          {isCommuteOpen && (
            <div className="veh-collapsible-body">
              <form onSubmit={(e) => void submitCommute(e)}>
                <div className="veh-grid-3">
                  <div className="veh-form-group">
                    <label className="veh-form-label">Employee ID (UUID) *</label>
                    <input
                      type="text"
                      className="veh-input"
                      required
                      placeholder="Employee UUID..."
                      value={commuteForm.employeeId}
                      onChange={(e) =>
                        setCommuteForm({ ...commuteForm, employeeId: e.target.value })
                      }
                    />
                  </div>

                  <div className="veh-form-group">
                    <label className="veh-form-label">Effective From *</label>
                    <input
                      type="date"
                      className="veh-input"
                      required
                      value={commuteForm.effectiveFrom}
                      onChange={(e) =>
                        setCommuteForm({ ...commuteForm, effectiveFrom: e.target.value })
                      }
                    />
                  </div>

                  <div className="veh-form-group">
                    <label className="veh-form-label">Effective To (Optional)</label>
                    <input
                      type="date"
                      className="veh-input"
                      value={commuteForm.effectiveTo}
                      onChange={(e) =>
                        setCommuteForm({ ...commuteForm, effectiveTo: e.target.value })
                      }
                    />
                  </div>
                </div>

                <div className="veh-grid-3" style={{ marginTop: '10px' }}>
                  <div className="veh-form-group">
                    <label className="veh-form-label">One Way Distance (km) *</label>
                    <input
                      type="number"
                      min={0}
                      max={500}
                      step="0.1"
                      className="veh-input"
                      placeholder="e.g. 15.5"
                      required
                      value={commuteForm.oneWayKm}
                      onChange={(e) => setCommuteForm({ ...commuteForm, oneWayKm: e.target.value })}
                    />
                  </div>

                  <div className="veh-form-group">
                    <label className="veh-form-label">Daily Round Trip (km) *</label>
                    <input
                      type="number"
                      min={0}
                      max={1000}
                      step="0.1"
                      className="veh-input"
                      placeholder="e.g. 31.0"
                      required
                      value={commuteForm.dailyRoundTripKm}
                      onChange={(e) =>
                        setCommuteForm({ ...commuteForm, dailyRoundTripKm: e.target.value })
                      }
                    />
                  </div>

                  <div className="veh-form-group">
                    <label className="veh-form-label">Route Reference / Address *</label>
                    <input
                      type="text"
                      className="veh-input"
                      placeholder="Home to main worksite route..."
                      required
                      value={commuteForm.routeReference}
                      onChange={(e) =>
                        setCommuteForm({ ...commuteForm, routeReference: e.target.value })
                      }
                    />
                  </div>
                </div>

                <div
                  style={{
                    display: 'flex',
                    justifyContent: 'flex-end',
                    gap: '8px',
                    marginTop: '12px',
                  }}
                >
                  <button
                    type="button"
                    className="btn-veh-secondary"
                    onClick={() => setIsCommuteOpen(false)}
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="btn-veh-primary"
                    disabled={
                      action.pending ||
                      !commuteForm.employeeId.trim() ||
                      !commuteForm.effectiveFrom ||
                      !commuteForm.oneWayKm ||
                      !commuteForm.dailyRoundTripKm ||
                      !commuteForm.routeReference.trim()
                    }
                  >
                    {action.pending ? 'Saving...' : 'Save Commute Distance'}
                  </button>
                </div>
              </form>
              <ActionMessage action={action} />
            </div>
          )}
        </div>
      )}

      {/* Fuel Claims Queue */}
      {canManage && (
        <div className="veh-card">
          <div className="veh-card-header">
            <div>
              <h2 className="veh-card-title">
                <Fuel size={16} color="#059669" />
                <span>Fuel Reimbursement Claims Queue</span>
              </h2>
              <p className="veh-card-desc">
                Review submitted fuel claims, audit eligible mileage against commute deductions, and
                approve for payroll settlement.
              </p>
            </div>
          </div>

          <div className="veh-filter-inputs-grid">
            <div className="veh-form-group">
              <label className="veh-form-label">Approval State</label>
              <select
                className="veh-select"
                value={state}
                onChange={(e) => {
                  setState(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">All States</option>
                {approvalStateOptions.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </div>

            <div className="veh-form-group">
              <label className="veh-form-label">Employee ID (UUID)</label>
              <div style={{ position: 'relative' }}>
                <input
                  type="text"
                  className="veh-input"
                  style={{ width: '100%', paddingRight: employeeId ? '28px' : '10px' }}
                  placeholder="Filter by employee UUID..."
                  value={employeeId}
                  onChange={(e) => {
                    setEmployeeId(e.target.value);
                    setPage(1);
                  }}
                />
                {employeeId && (
                  <button
                    type="button"
                    onClick={() => {
                      setEmployeeId('');
                      setPage(1);
                    }}
                    style={{
                      position: 'absolute',
                      right: '6px',
                      top: '50%',
                      transform: 'translateY(-50%)',
                      background: 'none',
                      border: 'none',
                      color: '#94a3b8',
                      cursor: 'pointer',
                      padding: 0,
                    }}
                  >
                    <X size={14} />
                  </button>
                )}
              </div>
            </div>
          </div>

          <QueryState query={claims}>
            {!claims.data?.items.length ? (
              <div className="veh-empty-state">
                <div className="veh-empty-icon">
                  <Fuel size={24} />
                </div>
                <h4 className="veh-empty-title">No fuel claims match this filter</h4>
                <p className="veh-empty-desc">
                  There are no submitted fuel reimbursement claims matching the selected filters.
                </p>
              </div>
            ) : (
              <div className="veh-items-list">
                {claims.data?.items.map((claim) => (
                  <article className="veh-item-card" key={claim.id}>
                    <div className="veh-item-top">
                      <div
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: '8px',
                          flexWrap: 'wrap',
                        }}
                      >
                        <span className="veh-reg-pill">
                          <Users size={12} />
                          <span>{claim.employeeName}</span>
                        </span>
                        <span
                          className={`veh-badge ${
                            claim.approvalState === 'Approved'
                              ? 'veh-badge-approved'
                              : 'veh-badge-pending'
                          }`}
                        >
                          <FinanceStatus value={claim.approvalState} />
                        </span>
                      </div>

                      <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                        <span style={{ fontSize: '14px', fontWeight: 750, color: '#0f172a' }}>
                          {claim.claimedAmount} {claim.currency}
                        </span>
                        {claim.approvalState === 'Pending' && (
                          <button
                            type="button"
                            className="btn-veh-success"
                            onClick={() => void approveClaim(claim.id)}
                          >
                            <CheckCircle2 size={13} />
                            <span>Approve Claim</span>
                          </button>
                        )}
                      </div>
                    </div>

                    <div className="veh-card-metrics-grid">
                      <div className="veh-metric-item">
                        <dt>Eligible Distance</dt>
                        <dd style={{ color: '#2563eb' }}>{claim.eligibleDistanceKm} km</dd>
                      </div>
                      <div className="veh-metric-item">
                        <dt>Commute Excluded</dt>
                        <dd>{claim.excludedCommuteKm} km</dd>
                      </div>
                      <div className="veh-metric-item">
                        <dt>Fuel Consumed</dt>
                        <dd>{claim.fuelLiters} L</dd>
                      </div>
                      <div className="veh-metric-item">
                        <dt>Receipt Ref</dt>
                        <dd>{claim.receiptReference || 'Digital Log'}</dd>
                      </div>
                      <div className="veh-metric-item">
                        <dt>Submitted Date</dt>
                        <dd>{labelDate(claim.createdAt)}</dd>
                      </div>
                    </div>
                  </article>
                ))}

                {claims.data && (
                  <div className="veh-pagination-bar">
                    <span>Page {page} of results</span>
                    <div style={{ display: 'flex', gap: '6px' }}>
                      <button
                        type="button"
                        className="btn-veh-secondary"
                        style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                        disabled={claims.isFetching || page <= 1}
                        onClick={() => setPage((v) => Math.max(1, v - 1))}
                      >
                        <ChevronLeft size={14} />
                        Previous
                      </button>
                      <button
                        type="button"
                        className="btn-veh-secondary"
                        style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                        disabled={claims.isFetching || !claims.data.hasMore}
                        onClick={() => setPage((v) => v + 1)}
                      >
                        Next
                        <ChevronRight size={14} />
                      </button>
                    </div>
                  </div>
                )}
              </div>
            )}
          </QueryState>
          <ActionMessage action={action} />
        </div>
      )}

      {vehicles.data && !vehicles.data.length && (
        <div
          style={{
            padding: '12px 14px',
            background: '#f8fafc',
            border: '1px solid #e2e8f0',
            borderRadius: '6px',
            fontSize: '12px',
            color: '#64748b',
          }}
        >
          No company vehicles are registered yet. Register your fleet under the Custody & Fleet tab.
        </div>
      )}
    </div>
  );
}

function VehicleMaintenance({
  canManage,
  canVehicles,
}: {
  canManage: boolean;
  canVehicles: boolean;
}) {
  const [page, setPage] = useState(1);
  const [state, setState] = useState('0');
  const [employeeId, setEmployeeId] = useState('');
  const [vehicleId, setVehicleId] = useState('');
  const action = useFinanceAction();

  const records = useFinanceQuery(
    '/hr/vehicles/maintenance' +
      queryString({
        vehicleId: vehicleId.trim() || undefined,
        employeeId: employeeId.trim() || undefined,
        state: state === '' ? undefined : Number(state),
        page,
        pageSize: 20,
      }),
    vehicleServicePage,
    canManage,
  );

  async function approve(recordId: string) {
    if (!window.confirm('Approve this vehicle maintenance reimbursement?')) return;
    const result = await action.run(
      `/hr/vehicles/maintenance/${recordId}/approve`,
      null,
      'POST',
      vehicleService,
    );
    if (result) void records.refetch();
  }

  return (
    <div className="veh-card">
      <div className="veh-card-header">
        <div>
          <h2 className="veh-card-title">
            <Wrench size={16} color="#2563eb" />
            <span>Vehicle Maintenance & Service Queue</span>
          </h2>
          <p className="veh-card-desc">
            Audit periodic servicing, oil change vouchers, workshop receipts, and emergency repair
            claims.
          </p>
          {canVehicles && (
            <p className="muted" style={{ fontSize: '11px', color: '#64748b', marginTop: 4 }}>
              Vehicle directory is managed in the custody tab.
            </p>
          )}
        </div>
      </div>

      <div className="veh-filter-inputs-grid">
        <div className="veh-form-group">
          <label className="veh-form-label">Approval State</label>
          <select
            className="veh-select"
            value={state}
            onChange={(e) => {
              setState(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All States</option>
            {approvalStateOptions.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        <div className="veh-form-group">
          <label className="veh-form-label">Employee ID (UUID)</label>
          <input
            type="text"
            className="veh-input"
            placeholder="Filter by employee..."
            value={employeeId}
            onChange={(e) => {
              setEmployeeId(e.target.value);
              setPage(1);
            }}
          />
        </div>

        <div className="veh-form-group">
          <label className="veh-form-label">Vehicle ID (UUID)</label>
          <input
            type="text"
            className="veh-input"
            placeholder="Filter by vehicle..."
            value={vehicleId}
            onChange={(e) => {
              setVehicleId(e.target.value);
              setPage(1);
            }}
          />
        </div>
      </div>

      <QueryState query={records}>
        {!records.data?.items.length ? (
          <div className="veh-empty-state">
            <div className="veh-empty-icon">
              <Wrench size={24} />
            </div>
            <h4 className="veh-empty-title">No maintenance records match this filter</h4>
            <p className="veh-empty-desc">
              There are no workshop service or repair records logged under the selected criteria.
            </p>
          </div>
        ) : (
          <div className="veh-items-list">
            {records.data?.items.map((record) => {
              const serviceLabel =
                serviceKindOptions[record.serviceKind - 1]?.label ??
                `Service #${record.serviceKind}`;

              return (
                <article className="veh-item-card" key={record.id}>
                  <div className="veh-item-top">
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '8px',
                        flexWrap: 'wrap',
                      }}
                    >
                      <span className="veh-reg-pill">
                        <Wrench size={13} />
                        <span>{record.registrationNumber}</span>
                      </span>
                      <span className="veh-purpose-pill">{serviceLabel}</span>
                      <span
                        className={`veh-badge ${
                          record.approvalState === 'Approved'
                            ? 'veh-badge-approved'
                            : 'veh-badge-pending'
                        }`}
                      >
                        <FinanceStatus value={record.approvalState} />
                      </span>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                      <span style={{ fontSize: '14px', fontWeight: 750, color: '#0f172a' }}>
                        {record.claimedAmount} {record.currency}
                      </span>
                      {record.approvalState === 'Pending' && (
                        <button
                          type="button"
                          className="btn-veh-success"
                          onClick={() => void approve(record.id)}
                        >
                          <CheckCircle2 size={13} />
                          <span>Approve Service</span>
                        </button>
                      )}
                    </div>
                  </div>

                  <div className="veh-card-metrics-grid">
                    <div className="veh-metric-item">
                      <dt>Driver / Employee</dt>
                      <dd>{record.employeeName}</dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>Service Date</dt>
                      <dd>{record.serviceDate}</dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>Odometer Reading</dt>
                      <dd>{record.odometerKm} km</dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>Authorized Workshop</dt>
                      <dd>{record.authorizedWorkshopReference || 'N/A'}</dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>Logged Date</dt>
                      <dd>{labelDate(record.createdAt)}</dd>
                    </div>
                  </div>
                </article>
              );
            })}

            {records.data && (
              <div className="veh-pagination-bar">
                <span>Page {page} of results</span>
                <div style={{ display: 'flex', gap: '6px' }}>
                  <button
                    type="button"
                    className="btn-veh-secondary"
                    style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                    disabled={records.isFetching || page <= 1}
                    onClick={() => setPage((v) => Math.max(1, v - 1))}
                  >
                    <ChevronLeft size={14} />
                    Previous
                  </button>
                  <button
                    type="button"
                    className="btn-veh-secondary"
                    style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                    disabled={records.isFetching || !records.data.hasMore}
                    onClick={() => setPage((v) => v + 1)}
                  >
                    Next
                    <ChevronRight size={14} />
                  </button>
                </div>
              </div>
            )}
          </div>
        )}
      </QueryState>
      <ActionMessage action={action} />
    </div>
  );
}

function VehicleCustodyPanel({
  canManage,
  canVehicles,
}: {
  canManage: boolean;
  canVehicles: boolean;
}) {
  const vehicles = useFinanceQuery('/hr/vehicles', vehicleList, canVehicles);
  const [vehicleId, setVehicleId] = useState('');
  const [employeeFilter, setEmployeeFilter] = useState('');
  const [isRegisterOpen, setIsRegisterOpen] = useState(false);
  const [isIssueOpen, setIsIssueOpen] = useState(false);

  const [createForm, setCreateForm] = useState({
    registrationNumber: '',
    vehicleType: '',
    makeModel: '',
    assetReference: '',
  });
  const [issueForm, setIssueForm] = useState({
    vehicleId: '',
    employeeId: '',
    issuedAt: '',
    issueOdometerKm: '',
    issueConditionReference: '',
  });
  const [returning, setReturning] = useState<VehicleCustody | null>(null);
  const [returnForm, setReturnForm] = useState({
    returnedAt: '',
    returnOdometerKm: '',
    returnConditionReference: '',
  });
  const action = useFinanceAction();
  const history = useFinanceQuery(
    '/hr/vehicles/custody/history' +
      queryString({
        vehicleId: vehicleId.trim() || undefined,
        employeeId: employeeFilter.trim() || undefined,
      }),
    custodyList,
    canManage,
  );

  async function createVehicle(event: FormEvent) {
    event.preventDefault();
    const result = await action.run(
      '/hr/vehicles',
      {
        registrationNumber: createForm.registrationNumber,
        vehicleType: createForm.vehicleType,
        makeModel: createForm.makeModel,
        assetReference: createForm.assetReference || null,
      },
      'POST',
      vehicle,
    );
    if (result) {
      setCreateForm({ registrationNumber: '', vehicleType: '', makeModel: '', assetReference: '' });
      setIsRegisterOpen(false);
      void vehicles.refetch();
    }
  }

  async function issue(event: FormEvent) {
    event.preventDefault();
    const result = await action.run(
      '/hr/vehicles/custody/issue',
      {
        vehicleId: issueForm.vehicleId,
        employeeId: issueForm.employeeId,
        issuedAt: new Date(issueForm.issuedAt).toISOString(),
        issueOdometerKm: Number(issueForm.issueOdometerKm),
        issueConditionReference: issueForm.issueConditionReference,
      },
      'POST',
      vehicleCustody,
    );
    if (result) {
      setIssueForm({
        vehicleId: '',
        employeeId: '',
        issuedAt: '',
        issueOdometerKm: '',
        issueConditionReference: '',
      });
      setIsIssueOpen(false);
      void history.refetch();
    }
  }

  async function recordReturn(event: FormEvent) {
    event.preventDefault();
    if (!returning) return;
    const result = await action.run(
      `/hr/vehicles/custody/${returning.id}/return`,
      {
        returnedAt: new Date(returnForm.returnedAt).toISOString(),
        returnOdometerKm: Number(returnForm.returnOdometerKm),
        returnConditionReference: returnForm.returnConditionReference,
      },
      'POST',
      vehicleCustody,
    );
    if (result) {
      setReturning(null);
      setReturnForm({ returnedAt: '', returnOdometerKm: '', returnConditionReference: '' });
      void history.refetch();
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
      {/* Action Bar: Register Vehicle & Issue Custody */}
      <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap' }}>
        {canVehicles && (
          <button
            type="button"
            className="btn-veh-primary"
            onClick={() => setIsRegisterOpen((o) => !o)}
          >
            <Plus size={14} />
            <span>{isRegisterOpen ? 'Cancel Registration' : '+ Register New Vehicle'}</span>
          </button>
        )}
        {canManage && (
          <button
            type="button"
            className="btn-veh-secondary"
            onClick={() => setIsIssueOpen((o) => !o)}
          >
            <KeyRound size={14} />
            <span>{isIssueOpen ? 'Cancel Assignment' : '+ Issue Vehicle Custody'}</span>
          </button>
        )}
      </div>

      {/* Collapsible Register Vehicle Form */}
      {isRegisterOpen && canVehicles && (
        <div className="veh-collapsible-card">
          <div className="veh-collapsible-body">
            <h3 style={{ margin: '0 0 10px', fontSize: '13px', fontWeight: 700, color: '#0f172a' }}>
              Register a New Company Vehicle
            </h3>
            <form onSubmit={(e) => void createVehicle(e)}>
              <div className="veh-grid-3">
                <div className="veh-form-group">
                  <label className="veh-form-label">Registration Number *</label>
                  <input
                    type="text"
                    className="veh-input"
                    placeholder="e.g. LEA-2026-901"
                    required
                    maxLength={40}
                    value={createForm.registrationNumber}
                    onChange={(e) =>
                      setCreateForm({ ...createForm, registrationNumber: e.target.value })
                    }
                  />
                </div>
                <div className="veh-form-group">
                  <label className="veh-form-label">Vehicle Type *</label>
                  <input
                    type="text"
                    className="veh-input"
                    placeholder="e.g. Sedan / Pickup / Motorcycle"
                    required
                    maxLength={60}
                    value={createForm.vehicleType}
                    onChange={(e) => setCreateForm({ ...createForm, vehicleType: e.target.value })}
                  />
                </div>
                <div className="veh-form-group">
                  <label className="veh-form-label">Make & Model *</label>
                  <input
                    type="text"
                    className="veh-input"
                    placeholder="e.g. Toyota Hilux 2024"
                    required
                    maxLength={150}
                    value={createForm.makeModel}
                    onChange={(e) => setCreateForm({ ...createForm, makeModel: e.target.value })}
                  />
                </div>
              </div>
              <div className="veh-form-group" style={{ marginTop: '10px' }}>
                <label className="veh-form-label">Asset Tag Reference (Optional)</label>
                <input
                  type="text"
                  className="veh-input"
                  placeholder="e.g. AST-FLEET-084"
                  maxLength={100}
                  value={createForm.assetReference}
                  onChange={(e) => setCreateForm({ ...createForm, assetReference: e.target.value })}
                />
              </div>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'flex-end',
                  gap: '8px',
                  marginTop: '12px',
                }}
              >
                <button
                  type="button"
                  className="btn-veh-secondary"
                  onClick={() => setIsRegisterOpen(false)}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="btn-veh-primary"
                  disabled={
                    action.pending ||
                    !createForm.registrationNumber.trim() ||
                    !createForm.vehicleType.trim() ||
                    !createForm.makeModel.trim()
                  }
                >
                  {action.pending ? 'Registering...' : 'Register Vehicle'}
                </button>
              </div>
            </form>
            <ActionMessage action={action} />
          </div>
        </div>
      )}

      {/* Collapsible Issue Custody Form */}
      {isIssueOpen && canManage && (
        <div className="veh-collapsible-card">
          <div className="veh-collapsible-body">
            <h3 style={{ margin: '0 0 10px', fontSize: '13px', fontWeight: 700, color: '#0f172a' }}>
              Issue Vehicle Custody to Employee
            </h3>
            <form onSubmit={(e) => void issue(e)}>
              <div className="veh-grid-3">
                <div className="veh-form-group">
                  <label className="veh-form-label">Select Vehicle *</label>
                  <select
                    className="veh-select"
                    required
                    value={issueForm.vehicleId}
                    onChange={(e) => setIssueForm({ ...issueForm, vehicleId: e.target.value })}
                  >
                    <option value="">Choose a vehicle</option>
                    {(vehicles.data ?? []).map((item: Vehicle) => (
                      <option key={item.id} value={item.id}>
                        {item.registrationNumber} ({item.makeModel})
                      </option>
                    ))}
                  </select>
                </div>
                <div className="veh-form-group">
                  <label className="veh-form-label">Employee ID (UUID) *</label>
                  <input
                    type="text"
                    className="veh-input"
                    placeholder="Employee UUID..."
                    required
                    value={issueForm.employeeId}
                    onChange={(e) => setIssueForm({ ...issueForm, employeeId: e.target.value })}
                  />
                </div>
                <div className="veh-form-group">
                  <label className="veh-form-label">Issued At Timestamp *</label>
                  <input
                    type="datetime-local"
                    className="veh-input"
                    required
                    value={issueForm.issuedAt}
                    onChange={(e) => setIssueForm({ ...issueForm, issuedAt: e.target.value })}
                  />
                </div>
              </div>
              <div className="veh-grid-3" style={{ marginTop: '10px' }}>
                <div className="veh-form-group">
                  <label className="veh-form-label">Issue Odometer Reading (km) *</label>
                  <input
                    type="number"
                    min={0}
                    step="0.1"
                    className="veh-input"
                    placeholder="e.g. 12450.0"
                    required
                    value={issueForm.issueOdometerKm}
                    onChange={(e) =>
                      setIssueForm({ ...issueForm, issueOdometerKm: e.target.value })
                    }
                  />
                </div>
                <div className="veh-form-group" style={{ gridColumn: 'span 2' }}>
                  <label className="veh-form-label">Condition & Handover Notes *</label>
                  <input
                    type="text"
                    className="veh-input"
                    placeholder="e.g. Clean interior, scratch on rear bumper, spare tire verified"
                    required
                    value={issueForm.issueConditionReference}
                    onChange={(e) =>
                      setIssueForm({ ...issueForm, issueConditionReference: e.target.value })
                    }
                  />
                </div>
              </div>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'flex-end',
                  gap: '8px',
                  marginTop: '12px',
                }}
              >
                <button
                  type="button"
                  className="btn-veh-secondary"
                  onClick={() => setIsIssueOpen(false)}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="btn-veh-primary"
                  disabled={
                    action.pending ||
                    !issueForm.vehicleId ||
                    !issueForm.employeeId.trim() ||
                    !issueForm.issuedAt
                  }
                >
                  {action.pending ? 'Issuing...' : 'Issue Custody Handover'}
                </button>
              </div>
            </form>
            <ActionMessage action={action} />
          </div>
        </div>
      )}

      {/* Custody History Card */}
      {canManage && (
        <div className="veh-card">
          <div className="veh-card-header">
            <div>
              <h2 className="veh-card-title">
                <KeyRound size={16} color="#7c3aed" />
                <span>Vehicle Custody & Assignment History</span>
              </h2>
              <p className="veh-card-desc">
                Track active employee vehicle custody, odometer handovers, and return condition
                records.
              </p>
            </div>
          </div>

          <div className="veh-filter-inputs-grid">
            <div className="veh-form-group">
              <label className="veh-form-label">Vehicle ID</label>
              <input
                type="text"
                className="veh-input"
                placeholder="Filter by vehicle..."
                value={vehicleId}
                onChange={(e) => setVehicleId(e.target.value)}
              />
            </div>
            <div className="veh-form-group">
              <label className="veh-form-label">Employee ID</label>
              <input
                type="text"
                className="veh-input"
                placeholder="Filter by employee..."
                value={employeeFilter}
                onChange={(e) => setEmployeeFilter(e.target.value)}
              />
            </div>
          </div>

          <QueryState query={history}>
            {!history.data?.length ? (
              <div className="veh-empty-state">
                <div className="veh-empty-icon">
                  <KeyRound size={24} />
                </div>
                <h4 className="veh-empty-title">No custody records found</h4>
                <p className="veh-empty-desc">
                  There are no active or past custody records matching your search.
                </p>
              </div>
            ) : (
              <div className="veh-items-list">
                {history.data?.map((item) => (
                  <article className="veh-item-card" key={item.id}>
                    <div className="veh-item-top">
                      <div
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          gap: '8px',
                          flexWrap: 'wrap',
                        }}
                      >
                        <span className="veh-reg-pill">
                          <Car size={13} />
                          <span>{item.registrationNumber}</span>
                        </span>
                        <span style={{ fontSize: '12px', fontWeight: 600, color: '#1e293b' }}>
                          {item.employeeName}
                        </span>
                        <span
                          className={`veh-badge ${
                            item.isActive ? 'veh-badge-approved' : 'veh-badge-pending'
                          }`}
                        >
                          <FinanceStatus value={item.isActive ? 'active' : 'returned'} />
                        </span>
                      </div>

                      <div>
                        {item.isActive && (
                          <button
                            type="button"
                            className="btn-veh-secondary"
                            style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                            onClick={() => setReturning(item)}
                          >
                            Record Return
                          </button>
                        )}
                      </div>
                    </div>

                    <div className="veh-card-metrics-grid">
                      <div className="veh-metric-item">
                        <dt>Issued Date</dt>
                        <dd>{labelDate(item.issuedAt)}</dd>
                      </div>
                      <div className="veh-metric-item">
                        <dt>Issue Odometer</dt>
                        <dd>{item.issueOdometerKm} km</dd>
                      </div>
                      <div className="veh-metric-item">
                        <dt>Issue Condition</dt>
                        <dd>{item.issueConditionReference || 'Normal'}</dd>
                      </div>
                      <div className="veh-metric-item">
                        <dt>Returned Date</dt>
                        <dd>
                          {item.returnedAt ? labelDate(item.returnedAt) : 'Currently In Custody'}
                        </dd>
                      </div>
                      <div className="veh-metric-item">
                        <dt>Return Odometer</dt>
                        <dd>{item.returnOdometerKm ? `${item.returnOdometerKm} km` : 'Pending'}</dd>
                      </div>
                    </div>

                    {/* Inline Record Return Form */}
                    {returning?.id === item.id && (
                      <form
                        style={{
                          marginTop: '10px',
                          background: '#f8fafc',
                          border: '1px solid #bfdbfe',
                          borderRadius: '6px',
                          padding: '12px',
                        }}
                        onSubmit={(e) => void recordReturn(e)}
                      >
                        <h4
                          style={{
                            margin: '0 0 8px',
                            fontSize: '12px',
                            fontWeight: 700,
                            color: '#1e40af',
                          }}
                        >
                          Record Vehicle Return for {item.registrationNumber}
                        </h4>
                        <div className="veh-grid-3">
                          <div className="veh-form-group">
                            <label className="veh-form-label">Returned At *</label>
                            <input
                              type="datetime-local"
                              className="veh-input"
                              required
                              value={returnForm.returnedAt}
                              onChange={(e) =>
                                setReturnForm({ ...returnForm, returnedAt: e.target.value })
                              }
                            />
                          </div>
                          <div className="veh-form-group">
                            <label className="veh-form-label">Return Odometer (km) *</label>
                            <input
                              type="number"
                              min={0}
                              step="0.1"
                              className="veh-input"
                              required
                              value={returnForm.returnOdometerKm}
                              onChange={(e) =>
                                setReturnForm({ ...returnForm, returnOdometerKm: e.target.value })
                              }
                            />
                          </div>
                          <div className="veh-form-group">
                            <label className="veh-form-label">Return Condition Reference *</label>
                            <input
                              type="text"
                              className="veh-input"
                              required
                              placeholder="Notes on vehicle condition upon return..."
                              value={returnForm.returnConditionReference}
                              onChange={(e) =>
                                setReturnForm({
                                  ...returnForm,
                                  returnConditionReference: e.target.value,
                                })
                              }
                            />
                          </div>
                        </div>
                        <div
                          style={{
                            display: 'flex',
                            justifyContent: 'flex-end',
                            gap: '6px',
                            marginTop: '10px',
                          }}
                        >
                          <button
                            type="button"
                            className="btn-veh-secondary"
                            onClick={() => setReturning(null)}
                          >
                            Cancel
                          </button>
                          <button
                            type="submit"
                            className="btn-veh-primary"
                            disabled={action.pending || !returnForm.returnedAt}
                          >
                            {action.pending ? 'Saving...' : 'Confirm Return'}
                          </button>
                        </div>
                      </form>
                    )}
                  </article>
                ))}
              </div>
            )}
          </QueryState>
          <ActionMessage action={action} />
        </div>
      )}
    </div>
  );
}

function VehicleIncidents({ canManage }: { canManage: boolean }) {
  const navigation = useVehicleNavigation();
  const [page, setPage] = useState(1);
  const [state, setState] = useState('');
  const [custodyId, setCustodyId] = useState('');
  const [selectedIncidentId, setSelectedIncidentId] = useState('');

  const incidents = useFinanceQuery(
    '/hr/vehicles/incidents' +
      queryString({
        custodyId: custodyId.trim() || undefined,
        state: state === '' ? undefined : Number(state),
        page,
        pageSize: 20,
      }),
    vehicleIncidentPage,
    canManage,
  );

  return (
    <div className="veh-card">
      <div className="veh-card-header">
        <div>
          <h2 className="veh-card-title">
            <ShieldAlert size={16} color="#dc2626" />
            <span>Vehicle Incidents & Accident Reports</span>
          </h2>
          <p className="veh-card-desc">
            Review accident reports, police reports, third-party liability assessments, and
            insurance claims.
          </p>
        </div>
      </div>

      <div className="veh-filter-inputs-grid">
        <div className="veh-form-group">
          <label className="veh-form-label">Approval State</label>
          <select
            className="veh-select"
            disabled={navigation.pending}
            value={state}
            onChange={(e) => {
              navigation.navigate(() => {
                setState(e.target.value);
                setPage(1);
              });
            }}
          >
            <option value="">All States</option>
            {approvalStateOptions.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        <div className="veh-form-group">
          <label className="veh-form-label">Custody ID (UUID)</label>
          <input
            type="text"
            className="veh-input"
            disabled={navigation.pending}
            value={custodyId}
            placeholder="All custody records"
            onChange={(e) => {
              navigation.navigate(() => {
                setCustodyId(e.target.value);
                setPage(1);
              });
            }}
          />
        </div>
      </div>

      <QueryState query={incidents}>
        {!incidents.data?.items.length ? (
          <div className="veh-empty-state">
            <div className="veh-empty-icon">
              <ShieldAlert size={24} />
            </div>
            <h4 className="veh-empty-title">No incident reports match this filter</h4>
            <p className="veh-empty-desc">
              There are no reported traffic accidents or damage claims logged under the selected
              criteria.
            </p>
          </div>
        ) : (
          <div className="veh-items-list">
            {incidents.data?.items.map((incident) => {
              const isSelected = selectedIncidentId === incident.id;

              return (
                <article className="veh-item-card" key={incident.id}>
                  <div className="veh-item-top">
                    <div
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '8px',
                        flexWrap: 'wrap',
                      }}
                    >
                      <span className="veh-reg-pill">
                        <ShieldAlert size={13} color="#dc2626" />
                        <span>Custody #{incident.vehicleCustodyId.slice(0, 8)}...</span>
                      </span>
                      <span className="veh-badge veh-badge-pending">
                        <FinanceStatus value={incident.approvalState} />
                      </span>
                    </div>

                    <button
                      type="button"
                      className={isSelected ? 'btn-veh-primary' : 'btn-veh-secondary'}
                      style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                      disabled={navigation.pending}
                      onClick={() => {
                        navigation.navigate(() =>
                          setSelectedIncidentId(isSelected ? '' : incident.id),
                        );
                      }}
                    >
                      {isSelected ? 'Close Details' : 'Review & Amendment History'}
                    </button>
                  </div>

                  <div className="veh-card-metrics-grid">
                    <div className="veh-metric-item">
                      <dt>Occurred At</dt>
                      <dd>{labelDate(incident.occurredAt)}</dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>Evidence Ref</dt>
                      <dd>{incident.incidentEvidenceReference || 'N/A'}</dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>Third Party Details</dt>
                      <dd>{incident.thirdPartyDetailsReference || 'None'}</dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>Insurance Claim</dt>
                      <dd>{incident.insuranceClaimReference || 'Pending'}</dd>
                    </div>
                    <div className="veh-metric-item">
                      <dt>Proposed Liability</dt>
                      <dd>
                        {incident.proposedEmployeeLiability} {incident.currency}
                      </dd>
                    </div>
                  </div>

                  {isSelected && (
                    <div
                      style={{
                        marginTop: '8px',
                        borderTop: '1px solid #e2e8f0',
                        paddingTop: '10px',
                      }}
                    >
                      <VehicleIncidentManagement
                        incident={incident}
                        onChanged={() => void incidents.refetch()}
                      />
                    </div>
                  )}
                </article>
              );
            })}

            {incidents.data && (
              <div className="veh-pagination-bar">
                <span>Page {page} of results</span>
                <div style={{ display: 'flex', gap: '6px' }}>
                  <button
                    type="button"
                    className="btn-veh-secondary"
                    style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                    disabled={incidents.isFetching || navigation.pending || page <= 1}
                    onClick={() => navigation.navigate(() => setPage((v) => Math.max(1, v - 1)))}
                  >
                    <ChevronLeft size={14} />
                    Previous
                  </button>
                  <button
                    type="button"
                    className="btn-veh-secondary"
                    style={{ height: '28px', padding: '0 10px', fontSize: '11.5px' }}
                    disabled={incidents.isFetching || navigation.pending || !incidents.data.hasMore}
                    onClick={() => navigation.navigate(() => setPage((v) => v + 1))}
                  >
                    Next
                    <ChevronRight size={14} />
                  </button>
                </div>
              </div>
            )}
          </div>
        )}
      </QueryState>
    </div>
  );
}
