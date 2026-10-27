import { Platform } from 'react-native';
import ReactNativeBlobUtil from 'react-native-blob-util';
import type { SessionManager } from '../../auth/SessionManager';
import { validatePayslipFile, type PayslipFormat } from '../../utils/payslipFile';
import { AppError } from '../../utils/errors';
import { isGuid } from '../../utils/validation';
import { requestKey } from '../../utils/random';

const prefix = 'cosmix-payslip-';
export class PayslipFiles {
  static async clearTemporaryCopies(): Promise<void> {
    const directory = ReactNativeBlobUtil.fs.dirs.CacheDir;
    const files = await ReactNativeBlobUtil.fs.ls(directory);
    await Promise.all(files.filter(name => /^cosmix-payslip-[A-Za-z0-9_-]+\.(pdf|xlsx)$/.test(name)).map(name => ReactNativeBlobUtil.fs.unlink(`${directory}/${name}`).catch(() => undefined)));
  }

  static async open(session: SessionManager, batchId: string, format: PayslipFormat): Promise<void> {
    if (!isGuid(batchId)) throw new AppError('invalid_server_response');
    await session.revalidate();
    const identity = session.getSnapshot();
    const current = () => {
      const state = session.getSnapshot();
      if (state.status !== 'authenticated' || state.sessionId !== identity.sessionId || state.account?.accountId !== identity.account?.accountId) throw new AppError('session_expired', 401);
    };
    current();
    let file;
    try {
      file = validatePayslipFile(await session.request<unknown>(`/api/v1/self-service/finances/payslips/${batchId}?format=${format}`), format);
    } catch (error) {
      if (error instanceof AppError) throw error;
      throw new AppError('invalid_server_response');
    }
    current();
    const path = `${ReactNativeBlobUtil.fs.dirs.CacheDir}/${prefix}${requestKey('payslip').replace(/[^A-Za-z0-9_-]/g, '')}.${format}`;
    try {
      await ReactNativeBlobUtil.fs.writeFile(path, file.base64, 'base64');
      if ((await ReactNativeBlobUtil.fs.hash(path, 'sha256')).toLowerCase() !== file.sha256.toLowerCase()) throw new AppError('invalid_server_response');
      current();
      if (Platform.OS === 'android') await ReactNativeBlobUtil.android.actionViewIntent(path, file.contentType);
      else ReactNativeBlobUtil.ios.previewDocument(path);
    } catch (error) {
      await ReactNativeBlobUtil.fs.unlink(path).catch(() => undefined);
      throw error;
    }
  }
}
