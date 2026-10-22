import { Platform } from 'react-native';
import type { SessionManager } from '../../auth/SessionManager';

export interface LocationConsentStatus {
  hasActivePolicy: boolean;
  activePolicyVersion?: number;
  noticeText?: string;
  status: 'NotRequired' | 'PendingConsent' | 'Consented';
  consentedAt?: string;
}

export interface ConsentAcceptedResponse {
  consentId: string;
  acceptedAtUtc: string;
}

export class LocationConsentService {
  constructor(private readonly session: SessionManager) {}

  async getConsentStatus(): Promise<LocationConsentStatus> {
    try {
      const res = await this.session.request<any>('/api/v1/me/location-consent', {
        method: 'GET',
      });
      const statusMap: Record<number | string, 'NotRequired' | 'PendingConsent' | 'Consented'> = {
        0: 'NotRequired',
        1: 'PendingConsent',
        2: 'Consented',
        NotRequired: 'NotRequired',
        PendingConsent: 'PendingConsent',
        Consented: 'Consented',
      };
      return {
        ...res,
        status: statusMap[res?.status] ?? 'NotRequired',
      };
    } catch {
      return {
        hasActivePolicy: false,
        status: 'NotRequired',
      };
    }
  }

  async acceptConsent(): Promise<ConsentAcceptedResponse> {
    const metadata = `Platform=${Platform.OS}, OSVersion=${Platform.Version}`;
    return await this.session.request<ConsentAcceptedResponse>('/api/v1/me/location-consent/accept', {
      method: 'POST',
      body: {
        deviceMetadata: metadata,
        ipAddress: null,
      },
    });
  }
}
