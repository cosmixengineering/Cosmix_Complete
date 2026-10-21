import ReactNativeBlobUtil from 'react-native-blob-util';
import type { Transport } from './ApiClient';
import { AppError } from '../../utils/errors';

export const nativeTransport: Transport = async request => {
  try {
    const response = await ReactNativeBlobUtil.config({
      timeout: request.timeoutMs,
      followRedirect: false,
      trusty: false,
      fileCache: false,
    }).fetch(request.method, request.url, request.headers, request.body);
    const info = response.info();
    if (info.timeout) {
      throw new AppError('request_timeout');
    }
    const headers = Object.entries(info.headers);
    const contentType = String(
      headers.find(([name]) => name.toLowerCase() === 'content-type')?.[1] ??
        '',
    );
    return { status: info.status, contentType, body: await response.text() };
  } catch (error) {
    if (error instanceof AppError) {
      throw error;
    }
    throw new AppError('network_error');
  }
};
