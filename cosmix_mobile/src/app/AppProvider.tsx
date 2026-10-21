import React, {
  createContext,
  useContext,
  useEffect,
  useState,
  useSyncExternalStore,
} from 'react';
import { AppState, Platform } from 'react-native';
import { SessionManager } from '../auth/SessionManager';
import { environment } from '../config/environment';
import { ApiClient } from '../services/api/ApiClient';
import { nativeTransport } from '../services/api/nativeTransport';
import { AttendanceWorkflow } from '../services/attendance/AttendanceWorkflow';
import { CameraCapture } from '../services/camera/CameraCapture';
import { CurrentLocation } from '../services/location/CurrentLocation';
import { SecureVault } from '../services/storage/SecureVault';
import { SecureRequestStore } from '../services/storage/SecureRequestStore';
import { RequestCoordinator } from '../services/selfService/RequestCoordinator';
import { SelfServiceClient } from '../services/selfService/SelfServiceClient';
import { EvidencePicker } from '../services/documents/EvidencePicker';
import { AppError } from '../utils/errors';
import { randomSecret, requestKey } from '../utils/random';

function createServices() {
  const requestStore = new SecureRequestStore();
  const vault = new SecureVault(async () => {
    await requestStore.clear();
    await EvidencePicker.clearTemporaryCopies();
  });
  const api = new ApiClient(environment.apiBaseUrl, nativeTransport);
  const session = new SessionManager(api, vault, randomSecret, {
    platform: Platform.OS === 'ios' ? 'ios' : 'android',
    deviceName: `${environment.companyName} ${Platform.OS} app`,
  });
  const attendance = new AttendanceWorkflow(session, {
    camera: new CameraCapture(),
    location: new CurrentLocation(),
    pending: vault,
    createKey: requestKey,
    now: Date.now,
    identity: () => {
      const state = session.getSnapshot();
      if (
        state.status !== 'authenticated' ||
        !state.account ||
        !state.sessionId
      ) {
        throw new AppError('authentication_required', 401);
      }
      return { accountId: state.account.accountId, sessionId: state.sessionId };
    },
  });
  const requests = new RequestCoordinator(session, requestStore, () => {
    const state = session.getSnapshot();
    if (
      state.status !== 'authenticated' ||
      !state.account ||
      !state.sessionId
    ) {
      throw new AppError('authentication_required', 401);
    }
    return { accountId: state.account.accountId, sessionId: state.sessionId };
  });
  return {
    session,
    attendance,
    requests,
    selfService: new SelfServiceClient(session),
  };
}

const ServicesContext = createContext<ReturnType<typeof createServices> | null>(
  null,
);

export function AppProvider({
  children,
}: React.PropsWithChildren): React.JSX.Element {
  const [services] = useState(createServices);
  useEffect(() => {
    EvidencePicker.clearTemporaryCopies()
      .catch(() => undefined)
      .then(() => services.session.initialize());
    const subscription = AppState.addEventListener('change', next => {
      if (next === 'active') {
        services.session.revalidate().catch(() => undefined);
      }
    });
    return () => subscription.remove();
  }, [services]);
  return (
    <ServicesContext.Provider value={services}>
      {children}
    </ServicesContext.Provider>
  );
}

export function useServices() {
  const value = useContext(ServicesContext);
  if (!value) {
    throw new Error('AppProvider is required');
  }
  return value;
}

export function useSession() {
  const { session } = useServices();
  return useSyncExternalStore(session.subscribe, session.getSnapshot);
}
