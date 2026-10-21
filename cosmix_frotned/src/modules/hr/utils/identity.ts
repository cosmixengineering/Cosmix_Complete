/**
 * Pakistani CNIC & Identity Document Utilities
 * Standard CNIC format: XXXXX-XXXXXXX-X (13 digits, 15 characters with hyphens)
 */

export function formatCnic(raw: string): string {
  // Strip all non-digits and cap at 13 digits
  const digits = raw.replace(/\D/g, '').slice(0, 13);
  if (digits.length <= 5) {
    return digits;
  }
  if (digits.length <= 12) {
    return `${digits.slice(0, 5)}-${digits.slice(5)}`;
  }
  return `${digits.slice(0, 5)}-${digits.slice(5, 12)}-${digits.slice(12, 13)}`;
}

export function validateCnic(raw: string | null | undefined): string | null {
  if (!raw || !raw.trim()) {
    return 'Identity document number is required.';
  }
  const digits = raw.replace(/\D/g, '');
  if (digits.length === 0) {
    return 'CNIC must contain numbers.';
  }
  if (digits.length !== 13) {
    return `CNIC must be exactly 13 digits (${digits.length}/13 entered). Format: 42101-1234567-1`;
  }
  return null;
}
