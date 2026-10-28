import { useState, useMemo } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  ShieldCheck,
  ShieldAlert,
  KeyRound,
  Plus,
  Search,
  RefreshCw,
  Lock,
  CheckCircle2,
  Users,
  Shield,
  Layers,
  Sparkles,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';
import { fetchAllRoles, fetchCatalog, createRole, grantPermissionsToRole } from '../api/access-api';
import type { AccessRole } from '@/modules/hr/schemas/access-setup';
import { EmployeeAccessSetup } from '@/modules/hr/components/approvals/EmployeeAccessSetup';
import '@/styles/roles-permissions.css';

// Permission category groupings for fast filtering
const CATEGORY_MAP: Record<string, { label: string; prefix: string[] }> = {
  all: { label: 'All Domains', prefix: [''] },
  hr: {
    label: 'HR & Employees',
    prefix: ['hr.employees', 'hr.bank', 'hr.documents', 'hr.lifecycle'],
  },
  attendance: { label: 'Attendance & Rosters', prefix: ['attendance', 'scheduling'] },
  leaves: { label: 'Leave Management', prefix: ['hr.leaves', 'leaves'] },
  org: {
    label: 'Organization & Taxonomy',
    prefix: ['hr.organization', 'hr.staff-taxonomy', 'hr.foundation-policies'],
  },
  access: { label: 'Auth & Access Control', prefix: ['access', 'auth'] },
  approvals: { label: 'Approvals & Routing', prefix: ['approvals', 'hr.approvals'] },
  finance: {
    label: 'Compensation & Payroll',
    prefix: ['hr.compensation', 'hr.payroll', 'hr.advances', 'hr.allowances'],
  },
  sites: { label: 'Sites & Assets', prefix: ['hr.sites', 'assets', 'sims', 'certifications'] },
  reports: { label: 'Audit & Reports', prefix: ['reports', 'hr.reports'] },
};

export function RolesPermissionsPage() {
  const queryClient = useQueryClient();

  const [activeTab, setActiveTab] = useState<'roles' | 'permissions' | 'assignments'>('roles');
  const [roleSearch, setRoleSearch] = useState('');
  const [permSearch, setPermSearch] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('all');

  // Modals state
  const [createRoleOpen, setCreateRoleOpen] = useState(false);
  const [newRoleCode, setNewRoleCode] = useState('');
  const [newRoleName, setNewRoleName] = useState('');
  const [createRoleError, setCreateRoleError] = useState('');
  const [createRoleBusy, setCreateRoleBusy] = useState(false);

  // Grant Permissions state
  const [grantModalRole, setGrantModalRole] = useState<AccessRole | null>(null);
  const [selectedPermissions, setSelectedPermissions] = useState<string[]>([]);
  const [grantReason, setGrantReason] = useState('');
  const [grantFilter, setGrantFilter] = useState('');
  const [grantCategory, setGrantCategory] = useState('all');
  const [grantProgress, setGrantProgress] = useState<string>('');
  const [grantError, setGrantError] = useState('');
  const [grantBusy, setGrantBusy] = useState(false);

  // User assignment quick view state
  const [assignEmployeeId, setAssignEmployeeId] = useState<string>('');

  // Notifications
  const [feedbackMessage, setFeedbackMessage] = useState<string>('');

  // 1. Fetch All Roles
  const rolesQuery = useQuery({
    queryKey: ['admin-access-roles'],
    queryFn: fetchAllRoles,
    staleTime: 30000,
  });

  // 2. Fetch Catalog (193 permissions)
  const catalogQuery = useQuery({
    queryKey: ['admin-access-catalog'],
    queryFn: fetchCatalog,
    staleTime: 60000,
  });

  const roles = rolesQuery.data ?? [];
  const catalog = catalogQuery.data ?? [];
  const isRefreshing = rolesQuery.isFetching || catalogQuery.isFetching;

  // Filtered Roles
  const filteredRoles = useMemo(() => {
    if (!roleSearch.trim()) return roles;
    const query = roleSearch.toLowerCase();
    return roles.filter(
      (r) =>
        r.code.toLowerCase().includes(query) ||
        r.name.toLowerCase().includes(query) ||
        r.permissions.some((p) => p.toLowerCase().includes(query)),
    );
  }, [roles, roleSearch]);

  // Filtered Catalog
  const filteredCatalog = useMemo(() => {
    return catalog.filter((perm) => {
      // Category filter
      if (selectedCategory !== 'all') {
        const allowedPrefixes = CATEGORY_MAP[selectedCategory]?.prefix ?? [];
        const matchesCategory = allowedPrefixes.some((pref) => perm.code.startsWith(pref));
        if (!matchesCategory) return false;
      }

      // Search query
      if (permSearch.trim()) {
        const query = permSearch.toLowerCase();
        const matchesCode = perm.code.toLowerCase().includes(query);
        const matchesDesc = perm.description.toLowerCase().includes(query);
        if (!matchesCode && !matchesDesc) return false;
      }

      return true;
    });
  }, [catalog, selectedCategory, permSearch]);

  // Mapping from Permission Code -> Array of Roles having it
  const permissionToRolesMap = useMemo(() => {
    const map = new Map<string, AccessRole[]>();
    for (const r of roles) {
      for (const p of r.permissions) {
        const existing = map.get(p) ?? [];
        existing.push(r);
        map.set(p, existing);
      }
    }
    return map;
  }, [roles]);

  // Create Role Handler
  const handleCreateRoleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreateRoleError('');

    const code = newRoleCode.trim().toUpperCase();
    const name = newRoleName.trim();

    if (!code || !name) {
      setCreateRoleError('Both Role Code and Role Name are required.');
      return;
    }

    if (!/^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$/.test(code)) {
      setCreateRoleError(
        'Role Code must start with alphanumeric and contain only letters, numbers, underscores, dots, or dashes.',
      );
      return;
    }

    setCreateRoleBusy(true);
    try {
      const created = await createRole({ code, name });
      setFeedbackMessage(`Role "${created.name}" (${created.code}) was created successfully!`);
      setNewRoleCode('');
      setNewRoleName('');
      setCreateRoleOpen(false);
      await queryClient.invalidateQueries({ queryKey: ['admin-access-roles'] });
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to create role';
      setCreateRoleError(msg);
    } finally {
      setCreateRoleBusy(false);
    }
  };

  // Open Grant Permissions Modal
  const openGrantModal = (role: AccessRole) => {
    setGrantModalRole(role);
    setSelectedPermissions([...role.permissions]);
    setGrantReason('');
    setGrantFilter('');
    setGrantCategory('all');
    setGrantProgress('');
    setGrantError('');
  };

  // Submit Grant Permissions
  const handleGrantPermissionsSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!grantModalRole) return;

    setGrantError('');
    if (!grantReason.trim()) {
      setGrantError(
        'An explicit audit justification reason (1-1000 characters) is required by server policy.',
      );
      return;
    }

    // Only grant new permissions that are not already present
    const existing = new Set(grantModalRole.permissions);
    const toGrant = selectedPermissions.filter((p) => !existing.has(p));

    if (toGrant.length === 0) {
      setGrantError('Select at least one new permission to grant.');
      return;
    }

    setGrantBusy(true);
    setGrantProgress(`Initiating grant of ${toGrant.length} permissions...`);

    try {
      await grantPermissionsToRole(
        grantModalRole.id,
        toGrant,
        grantModalRole.version,
        grantReason.trim(),
        (granted, total) => {
          setGrantProgress(`Granted ${granted} of ${total} permissions...`);
        },
      );

      setFeedbackMessage(
        `Successfully granted ${toGrant.length} permissions to role "${grantModalRole.name}"!`,
      );
      setGrantModalRole(null);
      await queryClient.invalidateQueries({ queryKey: ['admin-access-roles'] });
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to grant permissions';
      setGrantError(msg);
    } finally {
      setGrantBusy(false);
      setGrantProgress('');
    }
  };

  // Quick stats
  const totalRoles = roles.length;
  const systemRoles = roles.filter((r) => r.isSystemRole).length;
  const customRoles = totalRoles - systemRoles;
  const totalCatalogPerms = catalog.length;

  return (
    <div className="rp-container">
      {/* 1. Header & Breadcrumb */}
      <header className="rp-header">
        <div className="rp-breadcrumb">
          <Link to="/admin/dashboard">Administration</Link>
          <span>/</span>
          <span>Access Management</span>
          <span>/</span>
          <span style={{ color: '#0f172a', fontWeight: 600 }}>Roles &amp; Permissions</span>
        </div>

        <div className="rp-header-main">
          <div className="rp-title-group">
            <div className="rp-icon-badge">
              <ShieldCheck size={24} />
            </div>
            <div>
              <span className="rp-tagline">Security &amp; Authorization Hub</span>
              <h1 className="rp-title">Roles &amp; Permissions Management</h1>
              <p className="rp-subtitle">
                Configure role definitions, assign system permissions across 193 enterprise
                capabilities, and manage role delegations for authorized employees.
              </p>
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Button
              variant="secondary"
              style={{
                height: '32px',
                padding: '0 12px',
                fontSize: '12px',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
              }}
              disabled={isRefreshing}
              onClick={() => {
                void rolesQuery.refetch();
                void catalogQuery.refetch();
              }}
            >
              <RefreshCw size={13} className={isRefreshing ? 'animate-spin' : ''} />
              <span>{isRefreshing ? 'Refreshing…' : 'Refresh Data'}</span>
            </Button>

            <Button
              variant="primary"
              style={{
                height: '32px',
                padding: '0 14px',
                fontSize: '12px',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '6px',
              }}
              onClick={() => setCreateRoleOpen(true)}
            >
              <Plus size={15} />
              <span>Create New Role</span>
            </Button>
          </div>
        </div>
      </header>

      {/* Global Feedback Banner */}
      {feedbackMessage && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            background: '#f0fdf4',
            border: '1px solid #bbf7d0',
            borderRadius: '8px',
            padding: '10px 16px',
            fontSize: '13px',
            color: '#166534',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <CheckCircle2 size={16} />
            <span>{feedbackMessage}</span>
          </div>
          <button
            type="button"
            onClick={() => setFeedbackMessage('')}
            style={{
              background: 'transparent',
              border: 'none',
              cursor: 'pointer',
              color: '#166534',
            }}
          >
            <X size={15} />
          </button>
        </div>
      )}

      {/* 2. KPI Stats Cards */}
      <div className="rp-stats-grid">
        <div className="rp-stat-card">
          <div className="rp-stat-icon" style={{ background: '#eff6ff', color: '#1d4ed8' }}>
            <Shield size={20} />
          </div>
          <div className="rp-stat-info">
            <span className="rp-stat-label">Total System Roles</span>
            <span className="rp-stat-val">{totalRoles}</span>
            <span className="rp-stat-sub">
              {customRoles} custom &bull; {systemRoles} protected
            </span>
          </div>
        </div>

        <div className="rp-stat-card">
          <div className="rp-stat-icon" style={{ background: '#fdf4ff', color: '#a21caf' }}>
            <KeyRound size={20} />
          </div>
          <div className="rp-stat-info">
            <span className="rp-stat-label">Permissions Catalog</span>
            <span className="rp-stat-val">{totalCatalogPerms}</span>
            <span className="rp-stat-sub">10 enterprise functional domains</span>
          </div>
        </div>

        <div className="rp-stat-card">
          <div className="rp-stat-icon" style={{ background: '#f0fdf4', color: '#15803d' }}>
            <Sparkles size={20} />
          </div>
          <div className="rp-stat-info">
            <span className="rp-stat-label">Custom Extensibility</span>
            <span className="rp-stat-val">{customRoles}</span>
            <span className="rp-stat-sub">Configured custom access roles</span>
          </div>
        </div>

        <div className="rp-stat-card">
          <div className="rp-stat-icon" style={{ background: '#fffbeb', color: '#b45309' }}>
            <Lock size={20} />
          </div>
          <div className="rp-stat-info">
            <span className="rp-stat-label">Protection Level</span>
            <span className="rp-stat-val">Hardware MFA</span>
            <span className="rp-stat-sub">Strict cryptographic rowversions</span>
          </div>
        </div>
      </div>

      {/* 3. Navigation Tabs */}
      <div className="rp-tabs-container">
        <div className="rp-tabs-list">
          <button
            type="button"
            className={`rp-tab-btn ${activeTab === 'roles' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('roles')}
          >
            <ShieldCheck size={16} />
            <span>Roles Directory</span>
            <span className="rp-tab-counter">{roles.length}</span>
          </button>

          <button
            type="button"
            className={`rp-tab-btn ${activeTab === 'permissions' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('permissions')}
          >
            <Layers size={16} />
            <span>Permissions Catalog</span>
            <span className="rp-tab-counter">{catalog.length}</span>
          </button>

          <button
            type="button"
            className={`rp-tab-btn ${activeTab === 'assignments' ? 'is-active' : ''}`}
            onClick={() => setActiveTab('assignments')}
          >
            <Users size={16} />
            <span>Employee Access Setup</span>
          </button>
        </div>
      </div>

      {/* =========================================================================
          TAB 1: ROLES DIRECTORY
          ========================================================================= */}
      {activeTab === 'roles' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          {/* Controls Bar */}
          <div className="rp-controls-bar">
            <div className="rp-search-box">
              <Search size={15} color="#64748b" />
              <input
                type="text"
                className="rp-search-input"
                placeholder="Search roles by code, name, or granted permission..."
                value={roleSearch}
                onChange={(e) => setRoleSearch(e.target.value)}
              />
              {roleSearch && (
                <button
                  type="button"
                  onClick={() => setRoleSearch('')}
                  style={{
                    background: 'transparent',
                    border: 'none',
                    cursor: 'pointer',
                    color: '#94a3b8',
                  }}
                >
                  <X size={14} />
                </button>
              )}
            </div>

            <div style={{ fontSize: '12.5px', color: '#64748b' }}>
              Showing {filteredRoles.length} of {roles.length} roles
            </div>
          </div>

          {/* Roles Cards Grid */}
          {filteredRoles.length === 0 ? (
            <div
              style={{
                textAlign: 'center',
                padding: '48px 20px',
                background: '#ffffff',
                border: '1px solid #e2e8f0',
                borderRadius: '8px',
                color: '#64748b',
              }}
            >
              <ShieldAlert size={36} color="#94a3b8" style={{ margin: '0 auto 12px' }} />
              <h3 style={{ fontSize: '15px', color: '#0f172a', margin: '0 0 6px' }}>
                No roles match your search
              </h3>
              <p style={{ fontSize: '13px', margin: 0 }}>
                Try clearing the search query or create a new role.
              </p>
            </div>
          ) : (
            <div className="rp-roles-grid">
              {filteredRoles.map((role) => {
                const isSystem = role.isSystemRole;
                const permCount = role.permissions.length;

                return (
                  <article key={role.id} className="rp-role-card">
                    {/* Top Row: Title & Badges */}
                    <div className="rp-role-card-header">
                      <div className="rp-role-title-wrap">
                        <div
                          className="rp-role-icon"
                          style={{
                            background: isSystem ? '#fef3c7' : '#eff6ff',
                            color: isSystem ? '#b45309' : '#1d4ed8',
                          }}
                        >
                          {isSystem ? <Lock size={18} /> : <ShieldCheck size={18} />}
                        </div>
                        <div>
                          <span className="rp-role-code">{role.code}</span>
                          <h3 className="rp-role-name">{role.name}</h3>
                        </div>
                      </div>

                      <div className="rp-badges-wrap">
                        {isSystem ? (
                          <span
                            className="rp-badge rp-badge--system"
                            title="System immutable core role"
                          >
                            <Lock size={10} />
                            System
                          </span>
                        ) : (
                          <span className="rp-badge rp-badge--custom">Custom</span>
                        )}
                        <span
                          className={`rp-badge ${role.isActive ? 'rp-badge--active' : 'rp-badge--inactive'}`}
                        >
                          {role.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </div>
                    </div>

                    {/* Middle Section: Granted Permissions */}
                    <div className="rp-role-permissions-section">
                      <div className="rp-role-perm-header">
                        <span>Granted Permissions ({permCount})</span>
                        {!isSystem && (
                          <button
                            type="button"
                            onClick={() => openGrantModal(role)}
                            style={{
                              background: 'transparent',
                              border: 'none',
                              color: '#2563eb',
                              fontWeight: 600,
                              fontSize: '11px',
                              cursor: 'pointer',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: '4px',
                            }}
                          >
                            <Plus size={11} />
                            Grant Permissions
                          </button>
                        )}
                      </div>

                      {permCount === 0 ? (
                        <div
                          style={{
                            fontSize: '11.5px',
                            color: '#94a3b8',
                            fontStyle: 'italic',
                            padding: '6px 0',
                          }}
                        >
                          No permissions assigned yet. Click "Grant Permissions" to assign access.
                        </div>
                      ) : (
                        <div className="rp-perm-chips">
                          {role.permissions.map((p) => (
                            <span key={p} className="rp-perm-chip" title={p}>
                              {p}
                            </span>
                          ))}
                        </div>
                      )}
                    </div>

                    {/* Footer Info & Action */}
                    <div className="rp-role-card-footer">
                      <span title="Concurrency RowVersion hash">
                        Version:{' '}
                        <code style={{ fontSize: '10px' }}>{role.version.slice(0, 8)}…</code>
                      </span>

                      {!isSystem ? (
                        <Button
                          variant="secondary"
                          style={{ height: '24px', padding: '0 8px', fontSize: '11px' }}
                          onClick={() => openGrantModal(role)}
                        >
                          <KeyRound size={11} style={{ marginRight: '4px' }} />
                          Manage Access
                        </Button>
                      ) : (
                        <span
                          style={{
                            fontSize: '11px',
                            color: '#94a3b8',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '3px',
                          }}
                        >
                          <Lock size={11} /> Immutable
                        </span>
                      )}
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* =========================================================================
          TAB 2: PERMISSIONS MATRIX (CATALOG OF 193 PERMISSIONS)
          ========================================================================= */}
      {activeTab === 'permissions' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          {/* Controls Bar */}
          <div className="rp-controls-bar">
            <div className="rp-search-box">
              <Search size={15} color="#64748b" />
              <input
                type="text"
                className="rp-search-input"
                placeholder="Search catalog by permission code or description..."
                value={permSearch}
                onChange={(e) => setPermSearch(e.target.value)}
              />
              {permSearch && (
                <button
                  type="button"
                  onClick={() => setPermSearch('')}
                  style={{
                    background: 'transparent',
                    border: 'none',
                    cursor: 'pointer',
                    color: '#94a3b8',
                  }}
                >
                  <X size={14} />
                </button>
              )}
            </div>

            <div style={{ fontSize: '12.5px', color: '#64748b' }}>
              Showing {filteredCatalog.length} of {catalog.length} catalog permissions
            </div>
          </div>

          {/* Matrix Container with Category Pills */}
          <div className="rp-matrix-container">
            <div className="rp-matrix-filters">
              {Object.entries(CATEGORY_MAP).map(([key, item]) => (
                <button
                  key={key}
                  type="button"
                  className={`rp-filter-pill ${selectedCategory === key ? 'is-active' : ''}`}
                  onClick={() => setSelectedCategory(key)}
                >
                  {item.label}
                </button>
              ))}
            </div>

            <div className="rp-table-scroll">
              <table className="rp-table">
                <thead>
                  <tr>
                    <th style={{ width: '280px' }}>Permission Code</th>
                    <th style={{ width: '380px' }}>Description &amp; Capability Scope</th>
                    <th style={{ width: '200px' }}>Security Attributes</th>
                    <th>Granted Roles</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredCatalog.length === 0 ? (
                    <tr>
                      <td
                        colSpan={4}
                        style={{ textAlign: 'center', padding: '36px', color: '#94a3b8' }}
                      >
                        No permissions found matching current filters.
                      </td>
                    </tr>
                  ) : (
                    filteredCatalog.map((perm) => {
                      const grantedRoles = permissionToRolesMap.get(perm.code) ?? [];

                      return (
                        <tr key={perm.code}>
                          <td className="rp-perm-code-col">{perm.code}</td>
                          <td style={{ color: '#475569', fontSize: '12px' }}>{perm.description}</td>
                          <td>
                            <div style={{ display: 'flex', gap: '4px', flexWrap: 'wrap' }}>
                              {perm.sensitive && (
                                <span
                                  className="rp-badge rp-badge--sensitive"
                                  title="Requires sensitive authorization"
                                >
                                  Sensitive
                                </span>
                              )}
                              {perm.management && (
                                <span
                                  className="rp-badge rp-badge--mgmt"
                                  title="Administrative / management capability"
                                >
                                  Management
                                </span>
                              )}
                              {perm.selfService && (
                                <span
                                  className="rp-badge rp-badge--self"
                                  title="Delegated self-service scope permitted"
                                >
                                  Self-Service
                                </span>
                              )}
                              {!perm.sensitive && !perm.management && !perm.selfService && (
                                <span className="rp-badge rp-badge--inactive">Standard</span>
                              )}
                            </div>
                          </td>
                          <td>
                            {grantedRoles.length === 0 ? (
                              <span
                                style={{ color: '#94a3b8', fontSize: '11px', fontStyle: 'italic' }}
                              >
                                Unassigned
                              </span>
                            ) : (
                              <div>
                                {grantedRoles.map((r) => (
                                  <span key={r.id} className="rp-role-assignment-pill">
                                    {r.name}
                                  </span>
                                ))}
                              </div>
                            )}
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* =========================================================================
          TAB 3: EMPLOYEE ACCESS SETUP & ROLE ASSIGNMENT
          ========================================================================= */}
      {activeTab === 'assignments' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div
            style={{
              background: '#ffffff',
              border: '1px solid #e2e8f0',
              borderRadius: '10px',
              padding: '20px',
            }}
          >
            <div
              style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '14px' }}
            >
              <KeyRound size={20} color="#2563eb" />
              <div>
                <h2 style={{ fontSize: '16px', fontWeight: 700, margin: 0, color: '#0f172a' }}>
                  Direct Employee Role Setup &amp; Provisioning
                </h2>
                <p style={{ fontSize: '12.5px', color: '#64748b', margin: '2px 0 0' }}>
                  Assign explicit roles, configure web login credentials, enable mobile access, or
                  revoke permissions for an approved employee.
                </p>
              </div>
            </div>

            {/* Input to specify employee ID */}
            <div style={{ display: 'flex', gap: '10px', alignItems: 'center', maxWidth: '600px' }}>
              <div style={{ flex: 1 }}>
                <input
                  type="text"
                  className="rp-form-input"
                  placeholder="Paste Employee UUID (e.g. 6ba7b810-9dad-11d1-80b4-00c04fd430c8)..."
                  value={assignEmployeeId}
                  onChange={(e) => setAssignEmployeeId(e.target.value.trim())}
                />
              </div>
              <Link
                to="/admin/employees"
                className="button button--secondary"
                style={{ height: '36px', fontSize: '12px', textDecoration: 'none' }}
              >
                Browse Employees
              </Link>
            </div>
          </div>

          {/* Render EmployeeAccessSetup if UUID is supplied */}
          {assignEmployeeId ? (
            <div
              style={{
                background: '#ffffff',
                border: '1px solid #e2e8f0',
                borderRadius: '10px',
                padding: '24px',
              }}
            >
              <EmployeeAccessSetup employeeId={assignEmployeeId} approved />
            </div>
          ) : (
            <div
              style={{
                padding: '40px 20px',
                textAlign: 'center',
                background: '#f8fafc',
                border: '1px dashed #cbd5e1',
                borderRadius: '8px',
                color: '#64748b',
              }}
            >
              <Users size={36} color="#94a3b8" style={{ margin: '0 auto 12px' }} />
              <h3 style={{ fontSize: '15px', color: '#0f172a', margin: '0 0 6px' }}>
                Select an Employee to Manage Access
              </h3>
              <p style={{ fontSize: '13px', maxWidth: '480px', margin: '0 auto 16px' }}>
                Enter an employee UUID above, or go to the{' '}
                <Link to="/admin/approvals" style={{ color: '#2563eb', fontWeight: 600 }}>
                  Hiring Approvals Queue
                </Link>{' '}
                or{' '}
                <Link to="/admin/employees" style={{ color: '#2563eb', fontWeight: 600 }}>
                  Employees Directory
                </Link>{' '}
                to open the interactive Role &amp; Access Assignment dialog.
              </p>
            </div>
          )}
        </div>
      )}

      {/* =========================================================================
          MODAL: CREATE NEW ROLE
          ========================================================================= */}
      <Dialog
        open={createRoleOpen}
        onOpenChange={() => {
          if (!createRoleBusy) setCreateRoleOpen(false);
        }}
        title="Create New Custom Access Role"
        description="Define an enterprise role with unique system code and descriptive name."
      >
        <form onSubmit={handleCreateRoleSubmit} className="rp-modal-form">
          {createRoleError && (
            <div
              style={{
                padding: '8px 12px',
                background: '#fef2f2',
                border: '1px solid #fecaca',
                borderRadius: '6px',
                fontSize: '12.5px',
                color: '#991b1b',
              }}
            >
              {createRoleError}
            </div>
          )}

          <div className="rp-form-group">
            <label className="rp-form-label" htmlFor="role-code-input">
              Role Code <span style={{ color: '#ef4444' }}>*</span>
            </label>
            <input
              id="role-code-input"
              type="text"
              className="rp-form-input"
              placeholder="e.g. INVENTORY_MANAGER, SITE_SUPERVISOR"
              value={newRoleCode}
              onChange={(e) => setNewRoleCode(e.target.value.toUpperCase())}
              required
              disabled={createRoleBusy}
            />
            <span className="rp-form-hint">
              Alphanumeric characters, underscores, hyphens, and dots only. Automatically
              capitalized.
            </span>
          </div>

          <div className="rp-form-group">
            <label className="rp-form-label" htmlFor="role-name-input">
              Role Display Name <span style={{ color: '#ef4444' }}>*</span>
            </label>
            <input
              id="role-name-input"
              type="text"
              className="rp-form-input"
              placeholder="e.g. Senior Inventory &amp; Warehouse Manager"
              value={newRoleName}
              onChange={(e) => setNewRoleName(e.target.value)}
              required
              disabled={createRoleBusy}
            />
          </div>

          <div className="rp-modal-actions">
            <Button
              variant="secondary"
              type="button"
              disabled={createRoleBusy}
              onClick={() => setCreateRoleOpen(false)}
            >
              Cancel
            </Button>
            <Button variant="primary" type="submit" disabled={createRoleBusy}>
              {createRoleBusy ? 'Creating Role…' : 'Create Role'}
            </Button>
          </div>
        </form>
      </Dialog>

      {/* =========================================================================
          MODAL: GRANT ROLE PERMISSIONS
          ========================================================================= */}
      {grantModalRole && (
        <Dialog
          open={!!grantModalRole}
          onOpenChange={() => {
            if (!grantBusy) setGrantModalRole(null);
          }}
          title={`Grant Permissions: ${grantModalRole.name}`}
          description={`Select permissions from the catalog for role ${grantModalRole.code}. Server requires an explicit audit reason.`}
        >
          <form onSubmit={handleGrantPermissionsSubmit} className="rp-modal-form">
            {grantError && (
              <div
                style={{
                  padding: '8px 12px',
                  background: '#fef2f2',
                  border: '1px solid #fecaca',
                  borderRadius: '6px',
                  fontSize: '12.5px',
                  color: '#991b1b',
                }}
              >
                {grantError}
              </div>
            )}

            {grantProgress && (
              <div
                style={{
                  padding: '8px 12px',
                  background: '#eff6ff',
                  border: '1px solid #bfdbfe',
                  borderRadius: '6px',
                  fontSize: '12.5px',
                  color: '#1d4ed8',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                }}
              >
                <RefreshCw size={14} className="animate-spin" />
                <span>{grantProgress}</span>
              </div>
            )}

            {/* Audit Justification Reason */}
            <div className="rp-form-group">
              <label className="rp-form-label" htmlFor="grant-reason-input">
                Audit Justification Reason <span style={{ color: '#ef4444' }}>*</span>
              </label>
              <textarea
                id="grant-reason-input"
                className="rp-form-textarea"
                rows={2}
                placeholder="State the business rationale for granting these permissions (retained permanently in audit log)..."
                value={grantReason}
                onChange={(e) => setGrantReason(e.target.value)}
                required
                disabled={grantBusy}
              />
              <span className="rp-form-hint">
                Mandatory server requirement for security compliance.
              </span>
            </div>

            {/* Permissions Filter & Category Filter inside modal */}
            <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
              <input
                type="text"
                className="rp-form-input"
                style={{ flex: 1, minWidth: '180px' }}
                placeholder="Filter permissions..."
                value={grantFilter}
                onChange={(e) => setGrantFilter(e.target.value)}
                disabled={grantBusy}
              />
              <select
                className="rp-form-input"
                style={{ width: '160px' }}
                value={grantCategory}
                onChange={(e) => setGrantCategory(e.target.value)}
                disabled={grantBusy}
              >
                {Object.entries(CATEGORY_MAP).map(([key, item]) => (
                  <option key={key} value={key}>
                    {item.label}
                  </option>
                ))}
              </select>
            </div>

            {/* Permissions Selection List */}
            <div className="rp-perm-selector">
              {catalog
                .filter((p) => {
                  if (grantCategory !== 'all') {
                    const prefixes = CATEGORY_MAP[grantCategory]?.prefix ?? [];
                    if (!prefixes.some((pre) => p.code.startsWith(pre))) return false;
                  }
                  if (grantFilter.trim()) {
                    const q = grantFilter.toLowerCase();
                    if (
                      !p.code.toLowerCase().includes(q) &&
                      !p.description.toLowerCase().includes(q)
                    )
                      return false;
                  }
                  return true;
                })
                .map((perm) => {
                  const isChecked = selectedPermissions.includes(perm.code);
                  const isAlreadyGranted = grantModalRole.permissions.includes(perm.code);

                  return (
                    <label
                      key={perm.code}
                      className={`rp-perm-item ${isChecked ? 'is-selected' : ''}`}
                      style={{ opacity: isAlreadyGranted ? 0.75 : 1 }}
                    >
                      <input
                        type="checkbox"
                        checked={isChecked}
                        disabled={grantBusy || isAlreadyGranted}
                        onChange={(e) => {
                          if (e.target.checked) {
                            setSelectedPermissions((prev) => [...prev, perm.code]);
                          } else {
                            setSelectedPermissions((prev) => prev.filter((p) => p !== perm.code));
                          }
                        }}
                      />
                      <div style={{ flex: 1 }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                          <span className="rp-perm-code-col" style={{ fontSize: '11px' }}>
                            {perm.code}
                          </span>
                          {isAlreadyGranted && (
                            <span
                              className="rp-badge rp-badge--active"
                              style={{ fontSize: '9.5px', padding: '0 4px' }}
                            >
                              Already Granted
                            </span>
                          )}
                          {perm.sensitive && (
                            <span
                              className="rp-badge rp-badge--sensitive"
                              style={{ fontSize: '9px', padding: '0 4px' }}
                            >
                              Sensitive
                            </span>
                          )}
                        </div>
                        <div style={{ fontSize: '11px', color: '#64748b', marginTop: '2px' }}>
                          {perm.description}
                        </div>
                      </div>
                    </label>
                  );
                })}
            </div>

            {/* Selection Summary */}
            <div
              style={{
                fontSize: '11.5px',
                color: '#475569',
                display: 'flex',
                justifyContent: 'space-between',
              }}
            >
              <span>
                Total selected: <strong>{selectedPermissions.length}</strong> permissions
              </span>
              <span>
                New to grant:{' '}
                <strong>
                  {
                    selectedPermissions.filter((p) => !grantModalRole.permissions.includes(p))
                      .length
                  }
                </strong>
              </span>
            </div>

            <div className="rp-modal-actions">
              <Button
                variant="secondary"
                type="button"
                disabled={grantBusy}
                onClick={() => setGrantModalRole(null)}
              >
                Cancel
              </Button>
              <Button
                variant="primary"
                type="submit"
                disabled={
                  grantBusy ||
                  selectedPermissions.filter((p) => !grantModalRole.permissions.includes(p))
                    .length === 0
                }
              >
                {grantBusy ? 'Granting…' : 'Confirm & Grant Permissions'}
              </Button>
            </div>
          </form>
        </Dialog>
      )}
    </div>
  );
}
