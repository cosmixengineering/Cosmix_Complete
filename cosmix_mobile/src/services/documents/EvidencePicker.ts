import { Platform } from 'react-native';
import {
  pick,
  keepLocalCopy,
  releaseSecureAccess,
  isErrorWithCode,
  errorCodes,
  types,
} from '@react-native-documents/picker';
import ReactNativeBlobUtil from 'react-native-blob-util';
import type { SelectedEvidence } from '../../types/selfService';
import { AppError } from '../../utils/errors';
import { randomSecret } from '../../utils/random';

export class EvidencePicker {
  /** Startup/sign-out only, before a form can select documents. Deletes our own
   * bounded temporary copies; never traverses document-provider locations. */
  static async clearTemporaryCopies(): Promise<void> {
    const picker = new EvidencePicker();
    const root = ReactNativeBlobUtil.fs.dirs.CacheDir.replace(/\/$/, '');
    let inspected = 0;
    const visit = async (folder: string, depth: number): Promise<void> => {
      for (const name of await ReactNativeBlobUtil.fs.ls(folder)) {
        if (++inspected > 1000) {
          return;
        }
        const path = folder + '/' + name;
        if (/^ess_evidence_[a-f0-9]{64}\.(?:pdf|png|jpg)$/.test(name)) {
          await picker.removePath(path);
          continue;
        }
        if (depth < 2 && /^[A-Za-z0-9_-]{1,100}$/.test(name)) {
          const info = await ReactNativeBlobUtil.fs.stat(path);
          if (info.type === 'directory') {
            await visit(path, depth + 1);
          }
        }
      }
    };
    await visit(root, 0);
  }
  async select(): Promise<SelectedEvidence[]> {
    const paths: string[] = [];
    let sourceUris: string[] = [];
    try {
      const selected = await pick({
        type: [types.pdf, types.images],
        allowMultiSelection: true,
        mode: 'open',
        requestLongTermAccess: false,
      });
      sourceUris = selected.map(file => file.uri);
      if (selected.length > 4) {
        throw new AppError('evidence_too_large');
      }
      const result: SelectedEvidence[] = [];
      let total = 0;
      for (const file of selected) {
        const extension =
          file.type === 'application/pdf'
            ? 'pdf'
            : file.type === 'image/png'
            ? 'png'
            : file.type === 'image/jpeg'
            ? 'jpg'
            : null;
        if (!extension || file.isVirtual) {
          throw new AppError('invalid_evidence');
        }
        const id = randomSecret();
        const name = `ess_evidence_${id}.${extension}`;
        const copies = await keepLocalCopy({
          destination: 'cachesDirectory',
          files: [{ uri: file.uri, fileName: name }],
        });
        const copy = copies[0];
        if (copy.status !== 'success') {
          throw new AppError('invalid_evidence');
        }
        const path = decodeURIComponent(
          copy.localUri.replace(/^file:\/\//, ''),
        );
        paths.push(path);
        this.requirePrivate(path);
        const info = await ReactNativeBlobUtil.fs.stat(path);
        const size = Number(info.size);
        total += size;
        if (
          !Number.isFinite(size) ||
          size < 1 ||
          size > 10 * 1024 * 1024 ||
          total > 20 * 1024 * 1024
        ) {
          throw new AppError('evidence_too_large');
        }
        result.push({
          id,
          path,
          name: file.name ?? `Evidence.${extension}`,
          size,
          contentType: file.type!,
          kind: 6,
        });
      }
      return result;
    } catch (error) {
      await Promise.allSettled(paths.map(path => this.removePath(path)));
      if (error instanceof AppError) {
        throw error;
      }
      if (
        isErrorWithCode(error) &&
        error.code === errorCodes.OPERATION_CANCELED
      ) {
        throw new AppError('selection_cancelled');
      }
      throw new AppError('invalid_evidence');
    } finally {
      if (Platform.OS === 'ios' && sourceUris.length) {
        await releaseSecureAccess(sourceUris).catch(() => undefined);
      }
    }
  }
  async attachments(
    selected: readonly SelectedEvidence[],
  ): Promise<
    { kind: number; fileName: string; contentType: string; content: string }[]
  > {
    if (
      selected.length > 4 ||
      selected.reduce((sum, file) => sum + file.size, 0) > 20 * 1024 * 1024
    ) {
      throw new AppError('evidence_too_large');
    }
    const result = [];
    for (const file of selected) {
      this.requirePrivate(file.path);
      const info = await ReactNativeBlobUtil.fs.stat(file.path);
      if (Number(info.size) !== file.size || file.size > 10 * 1024 * 1024) {
        throw new AppError('invalid_evidence');
      }
      const content = await ReactNativeBlobUtil.fs.readFile(
        file.path,
        'base64',
      );
      const valid =
        file.contentType === 'application/pdf'
          ? content.startsWith('JVBERi0')
          : file.contentType === 'image/png'
          ? content.startsWith('iVBORw0KGgo')
          : file.contentType === 'image/jpeg' && content.startsWith('/9j/');
      if (!valid || content.length > Math.ceil(file.size / 3) * 4 + 4) {
        throw new AppError('invalid_evidence');
      }
      const extension =
        file.contentType === 'application/pdf'
          ? 'pdf'
          : file.contentType === 'image/png'
          ? 'png'
          : 'jpg';
      result.push({
        kind: file.kind,
        fileName: `evidence-${result.length + 1}.${extension}`,
        contentType: file.contentType,
        content,
      });
    }
    return result;
  }
  async remove(files: readonly SelectedEvidence[]): Promise<void> {
    await Promise.allSettled(files.map(file => this.removePath(file.path)));
  }
  private requirePrivate(path: string): void {
    const root = ReactNativeBlobUtil.fs.dirs.CacheDir.replace(/\/$/, '');
    const relative = path.slice(root.length + 1);
    const parts = relative.split('/');
    const name = parts[parts.length - 1];
    if (
      !path.startsWith(root + '/') ||
      parts.some(part => !part || part === '.' || part === '..') ||
      !/^ess_evidence_[a-f0-9]{64}\.(?:pdf|png|jpg)$/.test(name)
    ) {
      throw new AppError('invalid_evidence');
    }
  }
  private async removePath(path: string): Promise<void> {
    this.requirePrivate(path);
    if (await ReactNativeBlobUtil.fs.exists(path)) {
      await ReactNativeBlobUtil.fs.unlink(path);
    }
  }
}
