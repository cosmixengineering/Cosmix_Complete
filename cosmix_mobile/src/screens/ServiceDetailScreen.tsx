import React from 'react';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { ServiceRecords } from '../components/ServiceRecords';
import type { RootStackParams } from '../navigation/types';

export function ServiceDetailScreen({
  navigation,
  route,
}: NativeStackScreenProps<
  RootStackParams,
  'ServiceDetail'
>): React.JSX.Element {
  return (
    <ServiceRecords
      key={route.params.service}
      service={route.params.service}
      onRequest={(kind, previousRequestId) =>
        navigation.navigate('RequestForm', { kind, previousRequestId })
      }
      onHistory={category =>
        navigation.popTo('Workspace', {
          screen: 'History',
          params: { category },
        })
      }
    />
  );
}
