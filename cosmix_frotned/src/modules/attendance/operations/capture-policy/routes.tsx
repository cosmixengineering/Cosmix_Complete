import type { RouteObject } from 'react-router-dom';
import { CaptureValidationPolicyPage } from './CapturePolicyPage';

export const captureValidationPolicyRoutes: RouteObject[] = [
  { path: '/hr/attendance/capture-policy', element: <CaptureValidationPolicyPage /> },
];
