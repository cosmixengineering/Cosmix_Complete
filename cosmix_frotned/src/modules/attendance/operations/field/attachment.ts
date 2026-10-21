import { attachmentInput, type Attachment } from './contracts';

export async function readAttachment(file: File, signal: AbortSignal): Promise<Attachment> {
  signal.throwIfAborted();
  if (
    file.size <= 0 ||
    file.size > 10 * 1024 * 1024 ||
    !file.name.trim() ||
    file.name.trim() !== file.name ||
    file.name.length > 180 ||
    /[/\\:]/.test(file.name) ||
    [...file.name].some(
      (c) => c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159),
    )
  )
    throw new Error('Choose a PNG, JPEG or PDF up to 10 MiB with a plain filename.');
  const bytes = new Uint8Array(await file.arrayBuffer());
  signal.throwIfAborted();
  const ext = file.name.split('.').at(-1)?.toLowerCase();
  const starts = (signature: number[]) => signature.every((v, i) => bytes[i] === v);
  const png =
    ext === 'png' && file.type === 'image/png' && starts([137, 80, 78, 71, 13, 10, 26, 10]);
  const jpeg =
    ['jpg', 'jpeg'].includes(ext ?? '') &&
    file.type === 'image/jpeg' &&
    starts([255, 216, 255]) &&
    bytes.at(-2) === 255 &&
    bytes.at(-1) === 217;
  const pdf = ext === 'pdf' && file.type === 'application/pdf' && starts([37, 80, 68, 70, 45]);
  if (!png && !jpeg && !pdf)
    throw new Error('The filename, media type and file signature must match.');
  let binary = '';
  for (let i = 0; i < bytes.length; i += 8192)
    binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
  return attachmentInput.parse({
    fileName: file.name,
    contentType: file.type,
    content: btoa(binary),
  });
}
