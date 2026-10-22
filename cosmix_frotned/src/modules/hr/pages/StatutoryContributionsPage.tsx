import { useState, useEffect } from 'react';
import {
  Plus,
  Download,
  ShieldCheck,
  RefreshCw,
  Filter,
  UserCheck,
  Building,
} from 'lucide-react';
import { request } from '@/services/api/http-client';
import '@/styles/payroll-handoff.css';

interface StatutoryContribution {
  id: string;
  scheme: 'EOBI' | 'PESSI' | 'SESSI';
  contributionMonth: string;
  employeeContributionAmount: number;
  employerContributionAmount: number;
  totalContributionAmount: number;
  currency: string;
  state: 'Draft' | 'Approved' | 'Posted';
  payableAccountCode?: string;
  employerRegistrationNumber?: string;
  notes?: string;
  createdAt: string;
  createdByUserId: string;
  approvedAt?: string;
  approvedByUserId?: string;
  postedAt?: string;
  postedByUserId?: string;
}

interface StatutoryRegistration {
  id: string;
  employeeId: string;
  scheme: string;
  registrationNumber: string;
  registeredAt: string;
  recordedAt: string;
}

export function StatutoryContributionsPage() {
  const [activeTab, setActiveTab] = useState<'contributions' | 'registrations'>('contributions');
  const [contributions, setContributions] = useState<StatutoryContribution[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [schemeFilter, setSchemeFilter] = useState<string>('all');
  const [yearFilter, setYearFilter] = useState<number>(new Date().getFullYear());
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // New Contribution Modal
  const [modalOpen, setModalOpen] = useState(false);
  const [scheme, setScheme] = useState<number>(1); // EOBI = 1
  const [month, setMonth] = useState(`${new Date().getFullYear()}-${String(new Date().getMonth() + 1).padStart(2, '0')}`);
  const [employeeAmount, setEmployeeAmount] = useState<number>(0);
  const [employerAmount, setEmployerAmount] = useState<number>(0);
  const currency = 'PKR';
  const [employerRegNum, setEmployerRegNum] = useState('');
  const [payableAccount, setPayableAccount] = useState('2030-EOBI-PAYABLE');
  const [notes, setNotes] = useState('');

  // Employee Registrations tab state
  const [lookupEmpId, setLookupEmpId] = useState('');
  const [registrations, setRegistrations] = useState<StatutoryRegistration[]>([]);
  const [regModalOpen, setRegModalOpen] = useState(false);
  const [regTargetEmpId, setRegTargetEmpId] = useState('');
  const [regScheme, setRegScheme] = useState<number>(1);
  const [regNumber, setRegNumber] = useState('');
  const [regDate, setRegDate] = useState(new Date().toISOString().split('T')[0]);

  const fetchContributions = async () => {
    setLoading(true);
    try {
      const queryParams = new URLSearchParams();
      if (schemeFilter !== 'all') queryParams.set('scheme', schemeFilter);
      if (yearFilter) queryParams.set('year', yearFilter.toString());
      const query = queryParams.toString() ? `?${queryParams.toString()}` : '';

      const res = (await request(`/api/v1/statutory-contributions${query}`)) as {
        items: StatutoryContribution[];
      };
      setContributions(res?.items || []);
    } catch {
      setContributions([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (activeTab === 'contributions') {
      fetchContributions();
    }
  }, [activeTab, schemeFilter, yearFilter]);

  const handleLookupRegistrations = async () => {
    if (!lookupEmpId.trim()) return;
    setActionLoading(true);
    try {
      const res = (await request(
        `/api/v1/employees/${lookupEmpId.trim()}/statutory-registrations`,
      )) as StatutoryRegistration[];
      setRegistrations(Array.isArray(res) ? res : []);
    } catch {
      setRegistrations([]);
      setMessage({ type: 'error', text: 'No registrations found for this employee ID.' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleAddRegistration = async () => {
    if (!regTargetEmpId.trim() || !regNumber.trim()) {
      setMessage({ type: 'error', text: 'Employee ID and registration number are required.' });
      return;
    }
    setActionLoading(true);
    setMessage(null);
    try {
      await request(`/api/v1/employees/${regTargetEmpId.trim()}/statutory-registrations`, {
        method: 'POST',
        body: {
          scheme: regScheme,
          registrationNumber: regNumber.trim(),
          registeredAt: regDate,
        },
      });
      setMessage({ type: 'success', text: 'Employee statutory registration recorded successfully.' });
      setRegModalOpen(false);
      setLookupEmpId(regTargetEmpId.trim());
      setRegNumber('');
      const res = (await request(
        `/api/v1/employees/${regTargetEmpId.trim()}/statutory-registrations`,
      )) as StatutoryRegistration[];
      setRegistrations(Array.isArray(res) ? res : []);
    } catch {
      setMessage({ type: 'error', text: 'Failed to record registration or employee already registered.' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleCreate = async () => {
    if (employeeAmount <= 0 && employerAmount <= 0) {
      setMessage({ type: 'error', text: 'At least one contribution amount must be greater than zero.' });
      return;
    }
    setActionLoading(true);
    setMessage(null);
    try {
      await request('/api/v1/statutory-contributions', {
        method: 'POST',
        body: {
          scheme,
          contributionMonth: `${month}-01`,
          employeeContributionAmount: employeeAmount,
          employerContributionAmount: employerAmount,
          currency,
          payableAccountCode: payableAccount,
          notes,
          employerRegistrationNumber: employerRegNum.trim() || undefined,
        },
      });
      setMessage({ type: 'success', text: 'Statutory contribution record created in Draft.' });
      setModalOpen(false);
      fetchContributions();
    } catch {
      setMessage({ type: 'error', text: 'Failed to create contribution record.' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleApprove = async (id: string) => {
    setActionLoading(true);
    setMessage(null);
    try {
      await request(`/api/v1/statutory-contributions/${id}/approve`, {
        method: 'POST',
      });
      setMessage({ type: 'success', text: 'Contribution approved (second-person approval verified).' });
      fetchContributions();
    } catch {
      setMessage({
        type: 'error',
        text: 'Approval failed. The same person who created the record cannot approve it (separation of duties).',
      });
    } finally {
      setActionLoading(false);
    }
  };

  const handlePost = async (id: string) => {
    setActionLoading(true);
    setMessage(null);
    try {
      await request(`/api/v1/statutory-contributions/${id}/post`, {
        method: 'POST',
      });
      setMessage({ type: 'success', text: 'Contribution posted to accounting payable.' });
      fetchContributions();
    } catch {
      setMessage({ type: 'error', text: 'Posting failed.' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleExportCsv = async (id: string) => {
    try {
      const blob = (await request(`/api/v1/statutory-contributions/${id}/export`, {
        responseType: 'blob',
      })) as Blob;
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `Statutory_Schedule_${id.slice(0, 8)}.csv`;
      a.click();
      window.URL.revokeObjectURL(url);
    } catch {
      setMessage({ type: 'error', text: 'Failed to export CSV schedule.' });
    }
  };

  return (
    <div className="payroll-page">
      <div className="payroll-header">
        <div>
          <h1 className="payroll-title">Statutory Contributions (EOBI & Social Security)</h1>
          <p className="payroll-subtitle">
            Manage employer & employee statutory contributions for EOBI (National), PESSI (Punjab), and SESSI (Sindh).
          </p>
        </div>
        <div style={{ display: 'flex', gap: '8px' }}>
          {activeTab === 'contributions' && (
            <>
              <button className="payroll-btn payroll-btn-secondary" onClick={fetchContributions} disabled={loading}>
                <RefreshCw className={loading ? 'animate-spin' : ''} size={16} /> Refresh
              </button>
              <button className="payroll-btn payroll-btn-primary" onClick={() => setModalOpen(true)}>
                <Plus size={16} /> New Contribution
              </button>
            </>
          )}
          {activeTab === 'registrations' && (
            <button className="payroll-btn payroll-btn-primary" onClick={() => setRegModalOpen(true)}>
              <Plus size={16} /> Register Employee
            </button>
          )}
        </div>
      </div>

      {message && (
        <div
          style={{
            padding: '12px 16px',
            borderRadius: '6px',
            marginBottom: '16px',
            background: message.type === 'success' ? '#e6f4ea' : '#fce8e6',
            color: message.type === 'success' ? '#137333' : '#c5221f',
            border: `1px solid ${message.type === 'success' ? '#ceead6' : '#fad2cf'}`,
          }}
        >
          {message.text}
        </div>
      )}

      {/* Navigation Tabs */}
      <div style={{ display: 'flex', gap: '12px', borderBottom: '1px solid #e0e0e0', marginBottom: '16px' }}>
        <button
          onClick={() => setActiveTab('contributions')}
          style={{
            padding: '8px 16px',
            border: 'none',
            background: 'none',
            borderBottom: activeTab === 'contributions' ? '2px solid #1a73e8' : 'none',
            fontWeight: activeTab === 'contributions' ? 600 : 400,
            color: activeTab === 'contributions' ? '#1a73e8' : '#666',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '6px',
          }}
        >
          <Building size={16} /> Monthly Filings & Returns
        </button>
        <button
          onClick={() => setActiveTab('registrations')}
          style={{
            padding: '8px 16px',
            border: 'none',
            background: 'none',
            borderBottom: activeTab === 'registrations' ? '2px solid #1a73e8' : 'none',
            fontWeight: activeTab === 'registrations' ? 600 : 400,
            color: activeTab === 'registrations' ? '#1a73e8' : '#666',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: '6px',
          }}
        >
          <UserCheck size={16} /> Employee Scheme Registrations
        </button>
      </div>

      {activeTab === 'contributions' ? (
        <>
          {/* Filter Bar */}
          <div className="payroll-card" style={{ padding: '12px 16px', marginBottom: '16px', display: 'flex', gap: '16px', alignItems: 'center' }}>
            <Filter size={16} color="#666" />
            <div>
              <label style={{ fontSize: '12px', color: '#666', marginRight: '6px' }}>Scheme:</label>
              <select
                value={schemeFilter}
                onChange={(e) => setSchemeFilter(e.target.value)}
                style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid #ccc' }}
              >
                <option value="all">All Schemes</option>
                <option value="1">EOBI</option>
                <option value="2">PESSI (Punjab)</option>
                <option value="3">SESSI (Sindh)</option>
              </select>
            </div>
            <div>
              <label style={{ fontSize: '12px', color: '#666', marginRight: '6px' }}>Year:</label>
              <select
                value={yearFilter}
                onChange={(e) => setYearFilter(Number(e.target.value))}
                style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid #ccc' }}
              >
                <option value={2026}>2026</option>
                <option value={2025}>2025</option>
              </select>
            </div>
          </div>

          {/* Main Table */}
          <div className="payroll-card">
            {loading ? (
              <div style={{ padding: '32px', textAlign: 'center', color: '#666' }}>Loading statutory records...</div>
            ) : contributions.length === 0 ? (
              <div style={{ padding: '32px', textAlign: 'center', color: '#666' }}>
                No statutory contributions recorded for the selected filter. Click "New Contribution" to record.
              </div>
            ) : (
              <table className="payroll-table">
                <thead>
                  <tr>
                    <th>Scheme</th>
                    <th>Month</th>
                    <th>Employee Share</th>
                    <th>Employer Share</th>
                    <th>Total Contribution</th>
                    <th>State</th>
                    <th>Payable Account</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {contributions.map((c) => (
                    <tr key={c.id}>
                      <td>
                        <span style={{ fontWeight: 600 }}>{c.scheme}</span>
                        {c.employerRegistrationNumber && (
                          <div style={{ fontSize: '11px', color: '#666' }}>Reg: {c.employerRegistrationNumber}</div>
                        )}
                      </td>
                      <td>{c.contributionMonth.slice(0, 7)}</td>
                      <td>
                        {c.currency} {c.employeeContributionAmount.toLocaleString()}
                      </td>
                      <td>
                        {c.currency} {c.employerContributionAmount.toLocaleString()}
                      </td>
                      <td>
                        <span style={{ fontWeight: 700, color: '#1a73e8' }}>
                          {c.currency} {c.totalContributionAmount.toLocaleString()}
                        </span>
                      </td>
                      <td>
                        <span
                          className={`payroll-badge ${
                            c.state === 'Posted' ? 'badge-success' : c.state === 'Approved' ? 'badge-info' : 'badge-warning'
                          }`}
                        >
                          {c.state}
                        </span>
                      </td>
                      <td>
                        <span style={{ fontSize: '12px', color: '#666' }}>{c.payableAccountCode || '—'}</span>
                      </td>
                      <td>
                        <div style={{ display: 'flex', gap: '6px' }}>
                          {c.state === 'Draft' && (
                            <button
                              className="payroll-btn payroll-btn-primary"
                              style={{ padding: '4px 8px', fontSize: '12px' }}
                              onClick={() => handleApprove(c.id)}
                              disabled={actionLoading}
                              title="Second-person approval required"
                            >
                              <ShieldCheck size={14} /> Approve
                            </button>
                          )}
                          {c.state === 'Approved' && (
                            <button
                              className="payroll-btn payroll-btn-primary"
                              style={{ padding: '4px 8px', fontSize: '12px' }}
                              onClick={() => handlePost(c.id)}
                              disabled={actionLoading}
                            >
                              Post GL
                            </button>
                          )}
                          <button
                            className="payroll-btn payroll-btn-secondary"
                            style={{ padding: '4px 8px', fontSize: '12px' }}
                            onClick={() => handleExportCsv(c.id)}
                            title="Download CSV schedule"
                          >
                            <Download size={14} /> CSV
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      ) : (
        /* Employee Registrations Tab */
        <div className="payroll-card">
          <h2 style={{ fontSize: '16px', fontWeight: 600, marginBottom: '16px' }}>Employee Scheme Registrations</h2>
          <div style={{ display: 'flex', gap: '8px', marginBottom: '20px', maxWidth: '500px' }}>
            <input
              type="text"
              placeholder="Enter Employee ID (GUID)"
              value={lookupEmpId}
              onChange={(e) => setLookupEmpId(e.target.value)}
              style={{ flex: 1, padding: '8px 12px', borderRadius: '4px', border: '1px solid #ccc' }}
            />
            <button className="payroll-btn payroll-btn-primary" onClick={handleLookupRegistrations} disabled={actionLoading}>
              Search
            </button>
          </div>

          {registrations.length === 0 ? (
            <div style={{ padding: '24px', textAlign: 'center', color: '#777', background: '#fafafa', borderRadius: '4px' }}>
              No statutory registrations loaded. Enter an employee GUID to view registered schemes, or click "Register Employee".
            </div>
          ) : (
            <table className="payroll-table">
              <thead>
                <tr>
                  <th>Scheme</th>
                  <th>Registration / Certificate #</th>
                  <th>Registered Date</th>
                  <th>Recorded At</th>
                </tr>
              </thead>
              <tbody>
                {registrations.map((r) => (
                  <tr key={r.id}>
                    <td><strong>{r.scheme}</strong></td>
                    <td>{r.registrationNumber}</td>
                    <td>{r.registeredAt}</td>
                    <td>{new Date(r.recordedAt).toLocaleDateString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Create Modal */}
      {modalOpen && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', padding: '24px', width: '480px', maxWidth: '90%' }}>
            <h2 style={{ fontSize: '18px', fontWeight: 600, marginBottom: '16px' }}>Record Monthly Statutory Contribution</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '20px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Statutory Scheme</label>
                <select
                  value={scheme}
                  onChange={(e) => {
                    const val = Number(e.target.value);
                    setScheme(val);
                    if (val === 1) setPayableAccount('2030-EOBI-PAYABLE');
                    else if (val === 2) setPayableAccount('2031-PESSI-PAYABLE');
                    else setPayableAccount('2032-SESSI-PAYABLE');
                  }}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value={1}>EOBI (National)</option>
                  <option value={2}>PESSI (Punjab Social Security)</option>
                  <option value={3}>SESSI (Sindh Social Security)</option>
                </select>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Contribution Month</label>
                  <input
                    type="month"
                    value={month}
                    onChange={(e) => setMonth(e.target.value)}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Employer Reg Number</label>
                  <input
                    type="text"
                    placeholder="e.g. BE-01928"
                    value={employerRegNum}
                    onChange={(e) => setEmployerRegNum(e.target.value)}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Employee Share (PKR)</label>
                  <input
                    type="number"
                    value={employeeAmount}
                    onChange={(e) => setEmployeeAmount(Number(e.target.value))}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Employer Share (PKR)</label>
                  <input
                    type="number"
                    value={employerAmount}
                    onChange={(e) => setEmployerAmount(Number(e.target.value))}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>GL Payable Account</label>
                <input
                  type="text"
                  value={payableAccount}
                  onChange={(e) => setPayableAccount(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Challan / Filing Notes</label>
                <input
                  type="text"
                  placeholder="e.g. October monthly challan submission"
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button className="payroll-btn payroll-btn-secondary" onClick={() => setModalOpen(false)}>Cancel</button>
              <button className="payroll-btn payroll-btn-primary" onClick={handleCreate} disabled={actionLoading}>
                Save Draft
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Add Employee Registration Modal */}
      {regModalOpen && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', padding: '24px', width: '450px', maxWidth: '90%' }}>
            <h2 style={{ fontSize: '18px', fontWeight: 600, marginBottom: '16px' }}>Register Employee with Scheme</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '20px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Employee ID (GUID)</label>
                <input
                  type="text"
                  placeholder="e.g. 550e8400-e29b-41d4-a716-446655440000"
                  value={regTargetEmpId}
                  onChange={(e) => setRegTargetEmpId(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Statutory Scheme</label>
                <select
                  value={regScheme}
                  onChange={(e) => setRegScheme(Number(e.target.value))}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value={1}>EOBI</option>
                  <option value={2}>PESSI (Punjab)</option>
                  <option value={3}>SESSI (Sindh)</option>
                </select>
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Registration / Card Number</label>
                <input
                  type="text"
                  placeholder="e.g. EOBI-PK-982716"
                  value={regNumber}
                  onChange={(e) => setRegNumber(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Registration Date</label>
                <input
                  type="date"
                  value={regDate}
                  onChange={(e) => setRegDate(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button className="payroll-btn payroll-btn-secondary" onClick={() => setRegModalOpen(false)}>Cancel</button>
              <button className="payroll-btn payroll-btn-primary" onClick={handleAddRegistration} disabled={actionLoading}>
                Save Registration
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
