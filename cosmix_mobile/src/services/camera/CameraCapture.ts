import { PermissionsAndroid, Platform } from 'react-native';
import { launchCamera } from 'react-native-image-picker';
import ImageResizer from '@bam.tech/react-native-image-resizer';
import ReactNativeBlobUtil from 'react-native-blob-util';
import { AppError } from '../../utils/errors';
import { validateCaptureBase64 } from '../../utils/validation';

export class CameraCapture {
  async capture(): Promise<string> {
    if (
      Platform.OS === 'android' &&
      (await PermissionsAndroid.request(
        PermissionsAndroid.PERMISSIONS.CAMERA,
      )) !== PermissionsAndroid.RESULTS.GRANTED
    ) {
      throw new AppError('camera_permission');
    }
    const temporary = new Set<string>();
    try {
      const result = await launchCamera({
        mediaType: 'photo',
        cameraType: 'front',
        saveToPhotos: false,
        includeBase64: false,
        includeExtra: false,
        quality: 0.9,
        maxWidth: 1600,
        maxHeight: 1600,
      });
      if (result.didCancel) {
        throw new AppError('camera_cancelled');
      }
      if (result.errorCode) {
        throw new AppError(
          result.errorCode === 'permission'
            ? 'camera_permission'
            : 'camera_unavailable',
        );
      }
      const asset = result.assets?.[0];
      if (!asset?.uri || result.assets?.length !== 1) {
        throw new AppError('invalid_face_image');
      }
      temporary.add(asset.uri);
      // Rotation is normalized and metadata is deliberately not copied. The
      // backend rejects EXIF-bearing or oversized images independently.
      let prepared = await ImageResizer.createResizedImage(
        asset.uri,
        720,
        720,
        'JPEG',
        85,
        0,
        undefined,
        false,
        { mode: 'contain', onlyScaleDown: true },
      );
      temporary.add(prepared.uri);
      if (prepared.size > 512 * 1024) {
        prepared = await ImageResizer.createResizedImage(
          prepared.uri,
          640,
          640,
          'JPEG',
          65,
          0,
          undefined,
          false,
          { mode: 'contain', onlyScaleDown: true },
        );
        temporary.add(prepared.uri);
      }
      if (prepared.size > 512 * 1024) {
        throw new AppError('invalid_face_image');
      }
      const image = await ReactNativeBlobUtil.fs.readFile(
        this.privatePath(prepared.uri),
        'base64',
      );
      validateCaptureBase64(image);
      return image;
    } catch (error) {
      if (error instanceof AppError) {
        throw error;
      }
      throw new AppError('invalid_face_image');
    } finally {
      await Promise.allSettled(
        Array.from(temporary, async uri => {
          const path = this.privatePath(uri);
          if (await ReactNativeBlobUtil.fs.exists(path)) {
            await ReactNativeBlobUtil.fs.unlink(path);
          }
        }),
      );
    }
  }

  private privatePath(uri: string): string {
    const path = decodeURI(uri.replace(/^file:\/\//, ''));
    const cache = ReactNativeBlobUtil.fs.dirs.CacheDir.replace(/\/$/, '');
    // Only remove app-owned cache files; never follow an arbitrary returned URI.
    const iosTmp =
      Platform.OS === 'ios' ? cache.replace(/\/Library\/Caches$/, '/tmp') : '';
    if (
      path.includes('/../') ||
      path.includes('/./') ||
      ![cache, iosTmp].filter(Boolean).some(root => path.startsWith(root + '/'))
    ) {
      throw new AppError('invalid_face_image');
    }
    return path;
  }
}
