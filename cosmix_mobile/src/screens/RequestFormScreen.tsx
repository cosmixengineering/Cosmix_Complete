import React from 'react';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useSession } from '../app/AppProvider';
import { Notice, Screen } from '../components/Controls';
import { AllowanceForm } from '../components/forms/AllowanceForm';
import { DutyRequestForm } from '../components/forms/DutyRequestForm';
import { EncashmentForm } from '../components/forms/EncashmentForm';
import { FinanceRequestForm } from '../components/forms/FinanceRequestForm';
import { LeaveRequestForm } from '../components/forms/LeaveRequestForm';
import { ResignationForm } from '../components/forms/ResignationForm';
import { VehicleRequestForm } from '../components/forms/VehicleRequestForm';
import { canOpenService } from '../constants/services';
import type { RootStackParams } from '../navigation/types';
import type { RequestKind, ServiceKey } from '../types/selfService';

const serviceFor: Record<RequestKind, ServiceKey> = {
  leave: 'leaves',
  advance: 'loans',
  loan: 'loans',
  resignation: 'resignation',
  allowance: 'allowances',
  fuel: 'vehicles',
  trip: 'vehicles',
  maintenance: 'vehicles',
  incident: 'vehicles',
  correction: 'corrections',
  encashment: 'encashment',
  overtime: 'overtime',
};
export function RequestFormScreen({
  route,
}: NativeStackScreenProps<RootStackParams, 'RequestForm'>): React.JSX.Element {
  const auth = useSession();
  const { kind, previousRequestId } = route.params;
  if (!canOpenService(serviceFor[kind], auth)) {
    return (
      <Screen>
        <Notice message="Your company has not enabled this service for your current account." />
      </Screen>
    );
  }
  if (
    kind === 'leave' &&
    !auth.permissions.some(
      grant => grant.permission === 'attendance.rosters.self',
    )
  ) {
    return (
      <Screen>
        <Notice
          tone="info"
          message="Leave applications need access to your dated shift. Ask HR to enable your own roster access. Your existing leave history is still available."
        />
      </Screen>
    );
  }
  switch (kind) {
    case 'advance':
    case 'loan':
      return <FinanceRequestForm kind={kind} />;
    case 'leave':
      return <LeaveRequestForm previousRequestId={previousRequestId} />;
    case 'resignation':
      return <ResignationForm />;
    case 'allowance':
      return <AllowanceForm />;
    case 'overtime':
    case 'correction':
      return <DutyRequestForm kind={kind} />;
    case 'encashment':
      return <EncashmentForm />;
    case 'trip':
    case 'fuel':
    case 'maintenance':
    case 'incident':
      return <VehicleRequestForm kind={kind} />;
  }
}
