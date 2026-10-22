import { useState, useEffect } from 'react';
import {
  Plus,
  Download,
  ShieldCheck,
  RefreshCw,
  Filter,
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
  notes?: string;
  createdAt: string;
  createdByUserId: string;
  approvedAt?: string;
  approvedByUserId?: string;
  postedAt?: string;
  postedByUserId?: string;
}

export function StatutoryContributionsPage() {
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
  const [payableAccount, setPayableAccount] = useState('2030-EOBI-PAYABLE');
  const [notes, setNotes] = useState('');

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
    fetchContributions();
  }, [schemeFilter, yearFilter]);

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
          <button className="payroll-btn payroll-btn-secondary" onClick={fetchContributions} disabled={loading}>
            <RefreshCw className={loading ? 'animate-spin' : ''} size={16} /> Refresh
          </button>
          <button className="payroll-btn payroll-btn-primary" onClick={() => setModalOpen(true)}>
            <Plus size={16} /> New Contribution
          </button>
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
                  <option value={1}>EOBI (Employees Old-Age Benefits Institution)</option>
                  <option value={2}>PESSI (Punjab Employees Social Security)</option>
                  <option value={3}>SESSI (Sindh Employees Social Security)</option>
                </select>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Contribution Month</label>
                <input
                  type="month"
                  value={month}
                  onChange={(e) => setMonth(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Employee Amount (PKR)</label>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={employeeAmount}
                    onChange={(e) => setEmployeeAmount(Number(e.target.value))}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Employer Amount (PKR)</label>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    value={employerAmount}
                    onChange={(e) => setEmployerAmount(Number(e.target.value))}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Payable Account (GL Code)</label>
                <input
                  type="text"
                  value={payableAccount}
                  onChange={(e) => setPayableAccount(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Notes / Reference</label>
                <input
                  type="text"
                  placeholder="e.g. October 2026 challan submission"
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button className="payroll-btn payroll-btn-secondary" onClick={() => setModalOpen(false)}>Cancel</button>
              <button className="payroll-btn payroll-btn-primary" onClick={handleCreate} disabled={actionLoading}>
                Save as Draft
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
