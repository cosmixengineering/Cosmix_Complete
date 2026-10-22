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
      return await this.session.request<LocationConsentStatus>('/api/v1/me/location-consent', {
        method: 'GET',
      });
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
