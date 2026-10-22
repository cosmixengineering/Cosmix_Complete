import { useState, useEffect } from 'react';
import {
  Coins,
  RefreshCw,
  FileText,
  DollarSign,
  ShieldCheck,
} from 'lucide-react';
import { request } from '@/services/api/http-client';
import '@/styles/payroll-handoff.css';

interface Obligation {
  id: string;
  batchId: string;
  totalGrossAmount: number;
  totalNetAmount: number;
  currency: string;
  state: 'Draft' | 'Finalized' | 'Voided';
  finalizedAt?: string;
  finalizedByUserId?: string;
  createdAt: string;
}

interface Payment {
  id: string;
  obligationId: string;
  state: 'Pending' | 'Posted' | 'Confirmed' | 'Failed';
  paymentReference: string;
  paymentJournalEntryId?: string;
  confirmedAt?: string;
  confirmedByUserId?: string;
  createdAt: string;
}

export function PayrollAccountingPage() {
  const [obligations, setObligations] = useState<Obligation[]>([]);
  const [selectedObligation, setSelectedObligation] = useState<Obligation | null>(null);
  const [payments, setPayments] = useState<Payment[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // Modal / Action states
  const [accrualModal, setAccrualModal] = useState(false);
  const [paymentModal, setPaymentModal] = useState(false);
  const [debitAccount, setDebitAccount] = useState('5010-SALARY-EXPENSE');
  const [creditAccount, setCreditAccount] = useState('2010-SALARY-PAYABLE');
  const [bankAccount, setBankAccount] = useState('1010-HBL-OPERATIONAL');
  const [paymentRef, setPaymentRef] = useState('');
  const [accrualJournalId, setAccrualJournalId] = useState('');

  const fetchObligations = async () => {
    setLoading(true);
    try {
      const res = (await request('/api/v1/payroll-accounting/obligations')) as Obligation[];
      setObligations(Array.isArray(res) ? res : []);
      if (res && res.length > 0 && !selectedObligation && res[0]) {
        setSelectedObligation(res[0]);
      }
    } catch {
      // Fallback empty list if no obligations yet
      setObligations([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchObligations();
  }, []);

  useEffect(() => {
    if (selectedObligation) {
      request(`/api/v1/payroll-accounting/obligations/${selectedObligation.id}/payments`)
        .then((res) => setPayments(Array.isArray(res) ? (res as Payment[]) : []))
        .catch(() => setPayments([]));
    }
  }, [selectedObligation]);

  const handlePostAccrual = async () => {
    if (!selectedObligation) return;
    setActionLoading(true);
    setMessage(null);
    try {
      const res = (await request('/api/v1/payroll-accounting/accrual-journal', {
        method: 'POST',
        body: {
          obligationId: selectedObligation.id,
          debitAccountCode: debitAccount,
          creditAccountCode: creditAccount,
          description: `Salary accrual for batch ${selectedObligation.batchId.slice(0, 8)}`,
        },
      })) as { id: string };
      setAccrualJournalId(res.id);
      setMessage({ type: 'success', text: 'Accrual journal posted successfully.' });
      setAccrualModal(false);
    } catch {
      setMessage({ type: 'error', text: 'Failed to post accrual journal. Check permissions or accounts.' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleRecordPayment = async () => {
    if (!selectedObligation) return;
    if (!paymentRef.trim()) {
      setMessage({ type: 'error', text: 'Payment reference is required.' });
      return;
    }
    setActionLoading(true);
    setMessage(null);
    try {
      await request('/api/v1/payroll-accounting/payments', {
        method: 'POST',
        body: {
          obligationId: selectedObligation.id,
          accrualJournalEntryId: accrualJournalId || selectedObligation.id,
          paymentReference: paymentRef.trim(),
          debitAccountCode: creditAccount, // Debit Salary Payable
          creditAccountCode: bankAccount, // Credit Bank Account
        },
      });
      setMessage({ type: 'success', text: 'Salary disbursement payment recorded.' });
      setPaymentModal(false);
      setPaymentRef('');
      if (selectedObligation) {
        const pRes = (await request(
          `/api/v1/payroll-accounting/obligations/${selectedObligation.id}/payments`,
        )) as Payment[];
        setPayments(Array.isArray(pRes) ? pRes : []);
      }
    } catch {
      setMessage({ type: 'error', text: 'Failed to record salary payment.' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleConfirmDisbursement = async (paymentId: string) => {
    setActionLoading(true);
    setMessage(null);
    try {
      await request(`/api/v1/payroll-accounting/payments/${paymentId}/confirm`, {
        method: 'POST',
      });
      setMessage({ type: 'success', text: 'Salary disbursement confirmed by Finance.' });
      if (selectedObligation) {
        const pRes = (await request(
          `/api/v1/payroll-accounting/obligations/${selectedObligation.id}/payments`,
        )) as Payment[];
        setPayments(Array.isArray(pRes) ? pRes : []);
      }
    } catch {
      setMessage({ type: 'error', text: 'Failed to confirm disbursement.' });
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <div className="payroll-page">
      <div className="payroll-header">
        <div>
          <h1 className="payroll-title">Payroll Accounting & Salary Payment</h1>
          <p className="payroll-subtitle">
            Manage finalized payroll obligations, double-entry accrual journals, and verified salary disbursements.
          </p>
        </div>
        <button className="payroll-btn payroll-btn-secondary" onClick={fetchObligations} disabled={loading}>
          <RefreshCw className={loading ? 'animate-spin' : ''} size={16} /> Refresh
        </button>
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

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', marginTop: '16px' }}>
        {/* Left: Obligations List */}
        <div className="payroll-card">
          <h2 style={{ fontSize: '16px', fontWeight: 600, marginBottom: '12px', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <FileText size={18} /> Finalized Payroll Obligations
          </h2>

          {loading ? (
            <div style={{ padding: '24px', textAlign: 'center', color: '#666' }}>Loading obligations...</div>
          ) : obligations.length === 0 ? (
            <div style={{ padding: '24px', textAlign: 'center', color: '#666' }}>
              No payroll obligations found. Lock a payroll handoff batch first to generate an obligation.
            </div>
          ) : (
            <table className="payroll-table">
              <thead>
                <tr>
                  <th>Batch</th>
                  <th>Net Amount</th>
                  <th>State</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {obligations.map((ob) => (
                  <tr
                    key={ob.id}
                    style={{
                      background: selectedObligation?.id === ob.id ? '#f0f4ff' : undefined,
                      cursor: 'pointer',
                    }}
                    onClick={() => setSelectedObligation(ob)}
                  >
                    <td>
                      <div style={{ fontWeight: 500 }}>Batch #{ob.batchId.slice(0, 8)}</div>
                      <div style={{ fontSize: '12px', color: '#666' }}>{new Date(ob.createdAt).toLocaleDateString()}</div>
                    </td>
                    <td>
                      <div style={{ fontWeight: 600 }}>
                        {ob.currency} {ob.totalNetAmount.toLocaleString()}
                      </div>
                      <div style={{ fontSize: '11px', color: '#777' }}>Gross: {ob.totalGrossAmount.toLocaleString()}</div>
                    </td>
                    <td>
                      <span className={`payroll-badge ${ob.state === 'Finalized' ? 'badge-success' : 'badge-warning'}`}>
                        {ob.state}
                      </span>
                    </td>
                    <td>
                      <button
                        className="payroll-btn payroll-btn-primary"
                        style={{ padding: '4px 10px', fontSize: '12px' }}
                        onClick={(e) => {
                          e.stopPropagation();
                          setSelectedObligation(ob);
                          setAccrualModal(true);
                        }}
                      >
                        Post Accrual
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Right: Payment Management & Disbursal */}
        <div className="payroll-card">
          <h2 style={{ fontSize: '16px', fontWeight: 600, marginBottom: '12px', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Coins size={18} /> Salary Payments & Finance Confirmation
          </h2>

          {!selectedObligation ? (
            <div style={{ padding: '24px', textAlign: 'center', color: '#666' }}>
              Select a payroll obligation from the left to view payments.
            </div>
          ) : (
            <div>
              <div
                style={{
                  background: '#f8f9fa',
                  padding: '12px',
                  borderRadius: '6px',
                  marginBottom: '16px',
                  display: 'flex',
                  justifyContent: 'space-between',
                }}
              >
                <div>
                  <div style={{ fontSize: '12px', color: '#666' }}>Selected Obligation</div>
                  <div style={{ fontWeight: 600 }}>Batch #{selectedObligation.batchId.slice(0, 8)}</div>
                </div>
                <div style={{ textAlign: 'right' }}>
                  <div style={{ fontSize: '12px', color: '#666' }}>Total Net Payable</div>
                  <div style={{ fontWeight: 700, color: '#1a73e8' }}>
                    {selectedObligation.currency} {selectedObligation.totalNetAmount.toLocaleString()}
                  </div>
                </div>
              </div>

              <div style={{ display: 'flex', gap: '8px', marginBottom: '16px' }}>
                <button
                  className="payroll-btn payroll-btn-secondary"
                  onClick={() => setAccrualModal(true)}
                  disabled={actionLoading}
                >
                  <FileText size={16} /> 1. Post Accrual
                </button>
                <button
                  className="payroll-btn payroll-btn-primary"
                  onClick={() => setPaymentModal(true)}
                  disabled={actionLoading}
                >
                  <DollarSign size={16} /> 2. Record Payment
                </button>
              </div>

              <h3 style={{ fontSize: '14px', fontWeight: 600, marginBottom: '8px' }}>Recorded Disbursals</h3>
              {payments.length === 0 ? (
                <div style={{ padding: '16px', textAlign: 'center', color: '#777', background: '#fafafa', borderRadius: '4px' }}>
                  No payment recorded yet for this obligation.
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  {payments.map((p) => (
                    <div
                      key={p.id}
                      style={{
                        padding: '12px',
                        border: '1px solid #e0e0e0',
                        borderRadius: '6px',
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                      }}
                    >
                      <div>
                        <div style={{ fontWeight: 500 }}>Ref: {p.paymentReference}</div>
                        <div style={{ fontSize: '12px', color: '#666' }}>
                          Recorded: {new Date(p.createdAt).toLocaleDateString()}
                          {p.confirmedAt && ` | Confirmed: ${new Date(p.confirmedAt).toLocaleDateString()}`}
                        </div>
                      </div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span className={`payroll-badge ${p.state === 'Confirmed' ? 'badge-success' : 'badge-warning'}`}>
                          {p.state}
                        </span>
                        {p.state === 'Posted' && (
                          <button
                            className="payroll-btn payroll-btn-primary"
                            style={{ padding: '4px 8px', fontSize: '12px' }}
                            onClick={() => handleConfirmDisbursement(p.id)}
                            disabled={actionLoading}
                          >
                            <ShieldCheck size={14} /> Confirm
                          </button>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Accrual Modal */}
      {accrualModal && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', padding: '24px', width: '450px', maxWidth: '90%' }}>
            <h2 style={{ fontSize: '18px', fontWeight: 600, marginBottom: '16px' }}>Post Salary Accrual Journal</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '20px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Debit Account (Salary Expense)</label>
                <input
                  type="text"
                  value={debitAccount}
                  onChange={(e) => setDebitAccount(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Credit Account (Salary Payable)</label>
                <input
                  type="text"
                  value={creditAccount}
                  onChange={(e) => setCreditAccount(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button className="payroll-btn payroll-btn-secondary" onClick={() => setAccrualModal(false)}>Cancel</button>
              <button className="payroll-btn payroll-btn-primary" onClick={handlePostAccrual} disabled={actionLoading}>
                Confirm & Post Journal
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Payment Modal */}
      {paymentModal && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', padding: '24px', width: '450px', maxWidth: '90%' }}>
            <h2 style={{ fontSize: '18px', fontWeight: 600, marginBottom: '16px' }}>Record Salary Payment</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '20px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Payment / Bank Advice Reference</label>
                <input
                  type="text"
                  placeholder="e.g. HBL-FT-202610-001"
                  value={paymentRef}
                  onChange={(e) => setPaymentRef(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Debit Account (Salary Payable)</label>
                <input
                  type="text"
                  value={creditAccount}
                  readOnly
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #eee', background: '#f5f5f5' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Credit Account (Disbursing Bank Account)</label>
                <input
                  type="text"
                  value={bankAccount}
                  onChange={(e) => setBankAccount(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button className="payroll-btn payroll-btn-secondary" onClick={() => setPaymentModal(false)}>Cancel</button>
              <button className="payroll-btn payroll-btn-primary" onClick={handleRecordPayment} disabled={actionLoading}>
                Record Payment
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
