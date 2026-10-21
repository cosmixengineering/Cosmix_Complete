const maximumDecimalCoefficient = 79228162514264337593543950335n;

function decimalParts(value: string) {
  const [mantissa = '', exponent = '0'] = value.toLowerCase().split('e');
  const [whole = '', fraction = ''] = mantissa.split('.');
  const digits = whole + fraction;
  const point = whole.length + Number(exponent);
  const expanded =
    point <= 0
      ? '0.' + '0'.repeat(-point) + digits
      : point >= digits.length
        ? digits + '0'.repeat(point - digits.length)
        : digits.slice(0, point) + '.' + digits.slice(point);
  const [integer = '', decimals = ''] = expanded.split('.');
  return {
    integer: integer.replace(/^0+(?=\d)/, ''),
    decimals: decimals.replace(/0+$/, ''),
  };
}

function canonical(value: string) {
  const { integer, decimals } = decimalParts(value);
  return integer + (decimals ? '.' + decimals : '');
}

export function decimalFits(value: number) {
  if (!Number.isFinite(value) || value <= 0) return false;
  const { integer, decimals } = decimalParts(String(value));
  return decimals.length <= 28 && BigInt(integer + decimals) <= maximumDecimalCoefficient;
}

export function formatLimit(value: number) {
  return canonical(String(value));
}

export function intervalFits(value: number) {
  if (!decimalFits(value)) return false;
  const { integer, decimals } = decimalParts(String(value));
  return (
    BigInt(integer + decimals) * 10000000n <= 9223372036854775807n * 10n ** BigInt(decimals.length)
  );
}

export function parseLimit(value: string) {
  const text = value.trim();
  const number = Number(text);
  if (!/^\d+(?:\.\d+)?$/.test(text) || !decimalFits(number))
    throw new Error('Enter a positive decimal within the supported server decimal range.');
  if (canonical(text) !== canonical(String(number)))
    throw new Error(
      'This decimal would lose precision in web submission. Enter an exactly serializable limit.',
    );
  return number;
}
