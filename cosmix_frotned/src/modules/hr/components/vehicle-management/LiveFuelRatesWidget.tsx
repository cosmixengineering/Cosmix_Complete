import { useState, useEffect } from 'react';
import {
  Fuel,
  RefreshCw,
  Calculator,
  History,
  CheckCircle2,
  Info,
} from 'lucide-react';
import {
  fetchCurrentFuelPrices,
  fetchFuelPriceHistory,
  calculateFuelReimbursement,
  type FuelPriceItem,
  type FuelPriceHistoryRow,
} from '@/services/fuel-prices';

export function LiveFuelRatesWidget() {
  const [prices, setPrices] = useState<FuelPriceItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [source, setSource] = useState('OGRA notified prices, Government of Pakistan');
  const [lastRefreshed, setLastRefreshed] = useState<string>('');

  // History state
  const [showHistory, setShowHistory] = useState(false);
  const [historyFuel, setHistoryFuel] = useState<'petrol' | 'diesel'>('petrol');
  const [historyRows, setHistoryRows] = useState<FuelPriceHistoryRow[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  // Calculator state
  const [calcTripKm, setCalcTripKm] = useState('60');
  const [calcCommuteKm, setCalcCommuteKm] = useState('10');
  const [calcVehicleKpl, setCalcVehicleKpl] = useState('28'); // Standard 28 km/L motorcycle
  const [calcFuelType, setCalcFuelType] = useState<'petrol' | 'diesel'>('petrol');

  async function loadPrices() {
    setLoading(true);
    try {
      const data = await fetchCurrentFuelPrices();
      setPrices(data.prices);
      setSource(data.source);
      setLastRefreshed(new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' }));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadPrices();
  }, []);

  async function loadHistory(fuel: 'petrol' | 'diesel') {
    setHistoryLoading(true);
    try {
      const rows = await fetchFuelPriceHistory(fuel, 15);
      setHistoryRows(rows);
    } finally {
      setHistoryLoading(false);
    }
  }

  const petrolPrice = prices.find((p) => p.slug === 'petrol')?.price_pkr ?? 382.02;
  const dieselPrice = prices.find((p) => p.slug === 'diesel')?.price_pkr ?? 389.8;
  const activeRate = calcFuelType === 'petrol' ? petrolPrice : dieselPrice;

  const calculation = calculateFuelReimbursement(
    Number(calcTripKm) || 0,
    Number(calcCommuteKm) || 0,
    Number(calcVehicleKpl) || 28,
    activeRate,
    calcFuelType,
  );

  return (
    <div
      style={{
        background: '#ffffff',
        border: '1px solid #e2e8f0',
        borderRadius: '10px',
        padding: '18px 20px',
        marginBottom: '20px',
        boxShadow: '0 1px 3px rgba(0,0,0,0.02)',
      }}
    >
      {/* Header Row */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '10px',
          marginBottom: '16px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <div
            style={{
              width: '32px',
              height: '32px',
              borderRadius: '8px',
              background: '#eff6ff',
              color: '#2563eb',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Fuel size={17} />
          </div>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <h2 style={{ fontSize: '15px', fontWeight: 750, color: '#0f172a', margin: 0 }}>
                Live OGRA Fuel Rates &amp; Claim Calculator
              </h2>
              <span
                style={{
                  fontSize: '10.5px',
                  fontWeight: 600,
                  background: '#dcfce7',
                  color: '#15803d',
                  padding: '2px 8px',
                  borderRadius: '9999px',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '4px',
                }}
              >
                <span style={{ width: '6px', height: '6px', borderRadius: '50%', background: '#16a34a' }} />
                AutoOnes Synced
              </span>
            </div>
            <span style={{ fontSize: '11.5px', color: '#64748b' }}>
              Official notified prices for fuel reimbursement claims across project sites &bull; Source: {source}
            </span>
          </div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          {lastRefreshed && (
            <span style={{ fontSize: '11px', color: '#94a3b8' }}>Updated: {lastRefreshed}</span>
          )}
          <button
            type="button"
            onClick={() => void loadPrices()}
            disabled={loading}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '5px',
              padding: '4px 10px',
              borderRadius: '6px',
              border: '1px solid #cbd5e1',
              background: '#ffffff',
              fontSize: '11.5px',
              fontWeight: 600,
              color: '#334155',
              cursor: 'pointer',
            }}
          >
            <RefreshCw size={12} className={loading ? 'animate-spin' : ''} />
            <span>{loading ? 'Refreshing…' : 'Refresh Rates'}</span>
          </button>
          <button
            type="button"
            onClick={() => {
              const next = !showHistory;
              setShowHistory(next);
              if (next) void loadHistory(historyFuel);
            }}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '5px',
              padding: '4px 10px',
              borderRadius: '6px',
              border: '1px solid #cbd5e1',
              background: showHistory ? '#f1f5f9' : '#ffffff',
              fontSize: '11.5px',
              fontWeight: 600,
              color: '#334155',
              cursor: 'pointer',
            }}
          >
            <History size={12} />
            <span>{showHistory ? 'Hide History' : 'Rate History'}</span>
          </button>
        </div>
      </div>

      {/* Live Rate Cards Grid */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
          gap: '12px',
          marginBottom: '16px',
        }}
      >
        {/* Petrol Card */}
        <div
          style={{
            background: calcFuelType === 'petrol' ? '#f0fdf4' : '#f8fafc',
            border: calcFuelType === 'petrol' ? '1.5px solid #16a34a' : '1px solid #e2e8f0',
            borderRadius: '8px',
            padding: '14px 16px',
            position: 'relative',
            cursor: 'pointer',
            transition: 'all 0.15s ease',
          }}
          onClick={() => setCalcFuelType('petrol')}
        >
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: '11.5px', fontWeight: 700, textTransform: 'uppercase', color: '#15803d' }}>
              Motor Gasoline (Petrol)
            </span>
            <span
              style={{
                fontSize: '10px',
                fontWeight: 600,
                background: '#dcfce7',
                color: '#15803d',
                padding: '2px 6px',
                borderRadius: '4px',
              }}
            >
              Motorcycles / Site Staff
            </span>
          </div>

          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px', margin: '8px 0 4px' }}>
            <span style={{ fontSize: '24px', fontWeight: 750, color: '#0f172a' }}>
              PKR {petrolPrice.toFixed(2)}
            </span>
            <span style={{ fontSize: '12px', color: '#64748b' }}>/ litre</span>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '11.5px' }}>
            <span style={{ color: '#64748b' }}>
              Effective: <strong>{prices.find((p) => p.slug === 'petrol')?.effective_date || 'Current'}</strong>
            </span>
            {calcFuelType === 'petrol' && (
              <span style={{ color: '#16a34a', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '4px' }}>
                <CheckCircle2 size={12} /> Active in Calculator
              </span>
            )}
          </div>
        </div>

        {/* Diesel Card */}
        <div
          style={{
            background: calcFuelType === 'diesel' ? '#eff6ff' : '#f8fafc',
            border: calcFuelType === 'diesel' ? '1.5px solid #2563eb' : '1px solid #e2e8f0',
            borderRadius: '8px',
            padding: '14px 16px',
            position: 'relative',
            cursor: 'pointer',
            transition: 'all 0.15s ease',
          }}
          onClick={() => setCalcFuelType('diesel')}
        >
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: '11.5px', fontWeight: 700, textTransform: 'uppercase', color: '#1d4ed8' }}>
              High Speed Diesel (HSD)
            </span>
            <span
              style={{
                fontSize: '10px',
                fontWeight: 600,
                background: '#dbeafe',
                color: '#1e40af',
                padding: '2px 6px',
                borderRadius: '4px',
              }}
            >
              Heavy Vehicles &amp; Generators
            </span>
          </div>

          <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px', margin: '8px 0 4px' }}>
            <span style={{ fontSize: '24px', fontWeight: 750, color: '#0f172a' }}>
              PKR {dieselPrice.toFixed(2)}
            </span>
            <span style={{ fontSize: '12px', color: '#64748b' }}>/ litre</span>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '11.5px' }}>
            <span style={{ color: '#64748b' }}>
              Effective: <strong>{prices.find((p) => p.slug === 'diesel')?.effective_date || 'Current'}</strong>
            </span>
            {calcFuelType === 'diesel' && (
              <span style={{ color: '#2563eb', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '4px' }}>
                <CheckCircle2 size={12} /> Active in Calculator
              </span>
            )}
          </div>
        </div>
      </div>

      {/* History Table Dropdown */}
      {showHistory && (
        <div
          style={{
            background: '#f8fafc',
            border: '1px solid #e2e8f0',
            borderRadius: '8px',
            padding: '14px',
            marginBottom: '16px',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '10px' }}>
            <span style={{ fontSize: '12px', fontWeight: 700, color: '#0f172a' }}>
              Official OGRA Historical Rates (Last 15 Revisions)
            </span>
            <div style={{ display: 'flex', gap: '6px' }}>
              <button
                type="button"
                onClick={() => {
                  setHistoryFuel('petrol');
                  void loadHistory('petrol');
                }}
                style={{
                  padding: '3px 8px',
                  borderRadius: '4px',
                  border: historyFuel === 'petrol' ? '1px solid #16a34a' : '1px solid #cbd5e1',
                  background: historyFuel === 'petrol' ? '#dcfce7' : '#ffffff',
                  color: historyFuel === 'petrol' ? '#15803d' : '#334155',
                  fontSize: '11px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                Petrol History
              </button>
              <button
                type="button"
                onClick={() => {
                  setHistoryFuel('diesel');
                  void loadHistory('diesel');
                }}
                style={{
                  padding: '3px 8px',
                  borderRadius: '4px',
                  border: historyFuel === 'diesel' ? '1px solid #2563eb' : '1px solid #cbd5e1',
                  background: historyFuel === 'diesel' ? '#dbeafe' : '#ffffff',
                  color: historyFuel === 'diesel' ? '#1e40af' : '#334155',
                  fontSize: '11px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                Diesel History
              </button>
            </div>
          </div>

          {historyLoading ? (
            <p style={{ fontSize: '12px', color: '#64748b' }}>Loading historical rates from AutoOnes API…</p>
          ) : (
            <div style={{ maxHeight: '180px', overflowY: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px', textAlign: 'left' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid #cbd5e1', color: '#64748b' }}>
                    <th style={{ padding: '6px 8px' }}>Effective Date</th>
                    <th style={{ padding: '6px 8px' }}>Price (PKR / Litre)</th>
                    <th style={{ padding: '6px 8px' }}>Rate Change</th>
                    <th style={{ padding: '6px 8px' }}>Official Source</th>
                  </tr>
                </thead>
                <tbody>
                  {historyRows.map((row, idx) => (
                    <tr key={idx} style={{ borderBottom: '1px solid #f1f5f9' }}>
                      <td style={{ padding: '6px 8px', fontWeight: 600, color: '#0f172a' }}>
                        {row.effective_date}
                      </td>
                      <td style={{ padding: '6px 8px', fontWeight: 700, color: '#0f172a' }}>
                        PKR {Number(row.price_pkr).toFixed(2)}
                      </td>
                      <td style={{ padding: '6px 8px' }}>
                        {Number(row.change_pkr) > 0 ? (
                          <span style={{ color: '#dc2626', fontWeight: 600 }}>+{row.change_pkr}</span>
                        ) : Number(row.change_pkr) < 0 ? (
                          <span style={{ color: '#16a34a', fontWeight: 600 }}>{row.change_pkr}</span>
                        ) : (
                          <span style={{ color: '#94a3b8' }}>0.00</span>
                        )}
                      </td>
                      <td style={{ padding: '6px 8px', textTransform: 'uppercase', color: '#64748b', fontSize: '11px' }}>
                        {row.source}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* Live Fuel Claim Calculator Section */}
      <div
        style={{
          background: '#f8fafc',
          border: '1px solid #e2e8f0',
          borderRadius: '8px',
          padding: '14px 16px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px', marginBottom: '12px' }}>
          <Calculator size={15} color="#2563eb" />
          <span style={{ fontSize: '13px', fontWeight: 700, color: '#0f172a' }}>
            Real-time Fuel Reimbursement Counter
          </span>
          <span style={{ fontSize: '11.5px', color: '#64748b' }}>
            &bull; Counts liters and amount dynamically using live OGRA rate (PKR {activeRate.toFixed(2)}/L)
          </span>
        </div>

        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
            gap: '12px',
            alignItems: 'flex-end',
            marginBottom: '14px',
          }}
        >
          <div>
            <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: '#475569', marginBottom: '4px' }}>
              Total Trip Distance (km)
            </label>
            <input
              type="number"
              min={0}
              step="0.5"
              value={calcTripKm}
              onChange={(e) => setCalcTripKm(e.target.value)}
              style={{
                width: '100%',
                height: '34px',
                padding: '0 10px',
                borderRadius: '6px',
                border: '1px solid #cbd5e1',
                fontSize: '12.5px',
                background: '#ffffff',
                color: '#0f172a',
              }}
            />
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: '#475569', marginBottom: '4px' }}>
              Baseline Commute to Deduct (km)
            </label>
            <input
              type="number"
              min={0}
              step="0.5"
              value={calcCommuteKm}
              onChange={(e) => setCalcCommuteKm(e.target.value)}
              style={{
                width: '100%',
                height: '34px',
                padding: '0 10px',
                borderRadius: '6px',
                border: '1px solid #cbd5e1',
                fontSize: '12.5px',
                background: '#ffffff',
                color: '#0f172a',
              }}
            />
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: '#475569', marginBottom: '4px' }}>
              Fuel Economy / Standard
            </label>
            <select
              value={calcVehicleKpl}
              onChange={(e) => setCalcVehicleKpl(e.target.value)}
              style={{
                width: '100%',
                height: '34px',
                padding: '0 8px',
                borderRadius: '6px',
                border: '1px solid #cbd5e1',
                fontSize: '12px',
                background: '#ffffff',
                color: '#0f172a',
              }}
            >
              <option value="28">Motorcycle (28.0 km/L) - Standard</option>
              <option value="16">Small Car / 1000cc (16.0 km/L)</option>
              <option value="12">Sedan / 1300-1800cc (12.0 km/L)</option>
              <option value="8">SUV / Van (8.0 km/L)</option>
            </select>
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: '#475569', marginBottom: '4px' }}>
              Fuel Product Type
            </label>
            <div style={{ display: 'flex', gap: '6px', height: '34px' }}>
              <button
                type="button"
                onClick={() => setCalcFuelType('petrol')}
                style={{
                  flex: 1,
                  borderRadius: '6px',
                  border: calcFuelType === 'petrol' ? '1.5px solid #16a34a' : '1px solid #cbd5e1',
                  background: calcFuelType === 'petrol' ? '#dcfce7' : '#ffffff',
                  color: calcFuelType === 'petrol' ? '#15803d' : '#334155',
                  fontSize: '11.5px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                Petrol
              </button>
              <button
                type="button"
                onClick={() => setCalcFuelType('diesel')}
                style={{
                  flex: 1,
                  borderRadius: '6px',
                  border: calcFuelType === 'diesel' ? '1.5px solid #2563eb' : '1px solid #cbd5e1',
                  background: calcFuelType === 'diesel' ? '#dbeafe' : '#ffffff',
                  color: calcFuelType === 'diesel' ? '#1e40af' : '#334155',
                  fontSize: '11.5px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                Diesel
              </button>
            </div>
          </div>
        </div>

        {/* Calculated Result Ribbon */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: '12px',
            background: '#ffffff',
            border: '1px solid #bfdbfe',
            borderRadius: '8px',
            padding: '12px 18px',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '18px', flexWrap: 'wrap' }}>
            <div>
              <span style={{ fontSize: '11px', color: '#64748b', display: 'block' }}>Eligible Distance</span>
              <strong style={{ fontSize: '15px', color: '#0f172a' }}>{calculation.eligibleKm.toFixed(1)} km</strong>
            </div>

            <div>
              <span style={{ fontSize: '11px', color: '#64748b', display: 'block' }}>Fuel Consumed</span>
              <strong style={{ fontSize: '15px', color: '#2563eb' }}>{calculation.fuelLiters.toFixed(2)} Litres</strong>
            </div>

            <div>
              <span style={{ fontSize: '11px', color: '#64748b', display: 'block' }}>Applied OGRA Rate</span>
              <strong style={{ fontSize: '15px', color: '#16a34a' }}>PKR {calculation.fuelPricePerLiter.toFixed(2)} / L</strong>
            </div>
          </div>

          <div style={{ textAlign: 'right' }}>
            <span style={{ fontSize: '11px', color: '#64748b', display: 'block' }}>Calculated Total Claim</span>
            <div style={{ fontSize: '22px', fontWeight: 800, color: '#0f172a' }}>
              PKR {calculation.claimedAmount.toLocaleString('en-PK', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '6px', marginTop: '8px', fontSize: '11px', color: '#64748b' }}>
          <Info size={12} color="#3b82f6" />
          <span>
            Calculation breakdown: ({calcTripKm} km &minus; {calcCommuteKm} km commute) &divide; {calcVehicleKpl} km/L &times; PKR {activeRate.toFixed(2)} = <strong>PKR {calculation.claimedAmount.toFixed(2)}</strong>.
          </span>
        </div>
      </div>
    </div>
  );
}
