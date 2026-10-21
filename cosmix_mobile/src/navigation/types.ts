import type { OtpChallenge } from '../types/contracts';
import type { ServiceKey, RequestKind } from '../types/selfService';
import type {
  CompositeScreenProps,
  NavigatorScreenParams,
} from '@react-navigation/native';
import type { BottomTabScreenProps } from '@react-navigation/bottom-tabs';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

export type WorkspaceTabParams = {
  Dashboard: undefined;
  Services: undefined;
  Attendance: undefined;
  History: { category?: ServiceKey } | undefined;
  Profile: undefined;
};

export type WorkspaceScreenProps<T extends keyof WorkspaceTabParams> =
  CompositeScreenProps<
    BottomTabScreenProps<WorkspaceTabParams, T>,
    NativeStackScreenProps<RootStackParams>
  >;

export type RootStackParams = {
  Login: undefined;
  VerifyCode: { phoneNumber: string; challenge: OtpChallenge };
  Workspace: NavigatorScreenParams<WorkspaceTabParams>;
  ServiceDetail: { service: ServiceKey };
  RequestForm: { kind: RequestKind; previousRequestId?: string };
};
