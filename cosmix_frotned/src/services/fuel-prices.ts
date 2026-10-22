/**
 * Service for fetching live and historical fuel prices in Pakistan via AutoOnes API.
 * Free API providing official OGRA notified prices for Petrol and Diesel.
 */

export interface FuelPriceItem {
  fuel: string;
  slug: 'petrol' | 'diesel';
  price_pkr: number;
  change_pkr: number;
  effective_date: string;
  price_updated_at: string;
}

export interface FuelPricesApiResponse {
  source: string;
  unit: string;
  provider: string;
  docs: string;
  prices: FuelPriceItem[];
}

export interface FuelPriceHistoryRow {
  effective_date: string;
  price_pkr: string | number;
  change_pkr: string | number;
  source: string;
}

export interface FuelPriceHistoryResponse {
  fuel: string;
  slug: string;
  view: string;
  unit: string;
  provider: string;
  rows: FuelPriceHistoryRow[];
}

export interface FuelCalculationResult {
  totalKm: number;
  excludedCommuteKm: number;
  eligibleKm: number;
  kmPerLiter: number;
  fuelPricePerLiter: number;
  fuelType: 'petrol' | 'diesel';
  fuelLiters: number;
  claimedAmount: number;
  currency: string;
  effectiveDate: string;
}

// Fallback rates if API is momentarily unreachable
export const FALLBACK_FUEL_PRICES: FuelPriceItem[] = [
  {
    fuel: 'Petrol',
    slug: 'petrol',
    price_pkr: 382.02,
    change_pkr: 0,
    effective_date: new Date().toISOString().slice(0, 10),
    price_updated_at: new Date().toISOString(),
  },
  {
    fuel: 'Diesel',
    slug: 'diesel',
    price_pkr: 389.8,
    change_pkr: 0,
    effective_date: new Date().toISOString().slice(0, 10),
    price_updated_at: new Date().toISOString(),
  },
];

let cachedCurrentPrices: { data: FuelPricesApiResponse; timestamp: number } | null = null;
const CACHE_DURATION_MS = 5 * 60 * 1000; // 5 minutes

export async function fetchCurrentFuelPrices(): Promise<FuelPricesApiResponse> {
  const now = Date.now();
  if (cachedCurrentPrices && now - cachedCurrentPrices.timestamp < CACHE_DURATION_MS) {
    return cachedCurrentPrices.data;
  }

  try {
    const res = await fetch('https://api2.autoones.com/api/fuel-prices', {
      headers: { Accept: 'application/json' },
    });
    if (!res.ok) throw new Error(`Fuel API returned HTTP ${res.status}`);
    const data: FuelPricesApiResponse = await res.json();
    cachedCurrentPrices = { data, timestamp: now };
    return data;
  } catch (err) {
    console.warn('AutoOnes fuel price API unreachable, using cached/fallback:', err);
    if (cachedCurrentPrices) return cachedCurrentPrices.data;
    return {
      source: 'OGRA notified prices, Government of Pakistan (Offline fallback)',
      unit: 'PKR per litre',
      provider: 'autoones.com',
      docs: 'https://autoones.com/fuel-prices-api',
      prices: FALLBACK_FUEL_PRICES,
    };
  }
}

export async function fetchFuelPriceHistory(
  fuel: 'petrol' | 'diesel' = 'petrol',
  limit = 30,
): Promise<FuelPriceHistoryRow[]> {
  try {
    const res = await fetch(
      `https://api2.autoones.com/api/fuel-prices/history?fuel=${fuel}&view=daily&limit=${limit}`,
      { headers: { Accept: 'application/json' } },
    );
    if (!res.ok) throw new Error(`History API returned HTTP ${res.status}`);
    const data: FuelPriceHistoryResponse = await res.json();
    return data.rows || [];
  } catch (err) {
    console.warn('AutoOnes fuel history API unreachable:', err);
    return [];
  }
}

/**
 * Calculates fuel claim reimbursement according to Cosmix ERP policy rules.
 * Standard Motorcycle: 28 km/L
 */
export function calculateFuelReimbursement(
  totalKm: number,
  excludedCommuteKm = 0,
  kmPerLiter = 28.0,
  fuelPricePerLiter: number,
  fuelType: 'petrol' | 'diesel' = 'petrol',
  effectiveDate = new Date().toISOString().slice(0, 10),
): FuelCalculationResult {
  const safeTotal = Math.max(0, isNaN(totalKm) ? 0 : totalKm);
  const safeCommute = Math.max(0, isNaN(excludedCommuteKm) ? 0 : excludedCommuteKm);
  const eligibleKm = Math.max(0, safeTotal - safeCommute);
  const safeKpl = kmPerLiter > 0 ? kmPerLiter : 28.0;
  const safePrice = fuelPricePerLiter > 0 ? fuelPricePerLiter : 382.02;

  // Round to 2 decimal places per segment policy
  const fuelLiters = Math.round((eligibleKm / safeKpl) * 100) / 100;
  const claimedAmount = Math.round(fuelLiters * safePrice * 100) / 100;

  return {
    totalKm: safeTotal,
    excludedCommuteKm: safeCommute,
    eligibleKm,
    kmPerLiter: safeKpl,
    fuelPricePerLiter: safePrice,
    fuelType,
    fuelLiters,
    claimedAmount,
    currency: 'PKR',
    effectiveDate,
  };
}
