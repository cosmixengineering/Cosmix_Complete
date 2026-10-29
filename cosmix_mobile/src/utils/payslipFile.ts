export type PayslipFormat = 'pdf' | 'xlsx';
export type PayslipFile = {
  fileName: string;
  contentType: string;
  base64: string;
  sha256: string;
};

export function validatePayslipFile(
  value: unknown,
  format: PayslipFormat,
): PayslipFile {
  if (!value || typeof value !== 'object')
    throw new Error('Invalid payslip file');
  const file = value as Record<string, unknown>;
  const mime =
    format === 'pdf'
      ? 'application/pdf'
      : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
  if (
    typeof file.fileName !== 'string' ||
    file.fileName.length > 180 ||
    /[\\/]/.test(file.fileName) ||
    [...file.fileName].some(char => char.charCodeAt(0) < 32) ||
    !file.fileName.toLowerCase().endsWith('.' + format) ||
    file.contentType !== mime ||
    typeof file.base64 !== 'string' ||
    file.base64.length > 1400000 ||
    !/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(
      file.base64,
    ) ||
    !file.base64.startsWith(format === 'pdf' ? 'JVBERi0' : 'UEsDB') ||
    typeof file.sha256 !== 'string' ||
    !/^[a-fA-F0-9]{64}$/.test(file.sha256)
  )
    throw new Error('Invalid payslip file');
  return file as PayslipFile;
}
