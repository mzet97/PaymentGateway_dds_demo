'use client';

import { useEffect } from 'react';
import { useSession } from 'next-auth/react';
import { useApiAccess } from './useApiAccess';

const DEMO_MERCHANT_ID = '11111111-1111-1111-1111-111111111111';
const DEMO_API_KEY = 'sk_test_smoke_merchant';

/**
 * Provides merchant credentials for API calls.
 *
 * - merchantId: from Authentik JWT session claim (primary),
 *   or from manually configured localStorage (fallback).
 * - apiKey: from localStorage. Auto-configured for demo merchant.
 *
 * The Bearer token from the session is sent automatically by apiClient.
 * The API Key is sent as X-API-Key header for backend auth fallback.
 */
export function useAutoApiAccess() {
  const { data: session } = useSession();
  const { credentials, saveCredentials, removeCredentials } = useApiAccess();

  // Primary: merchantId from Authentik JWT claim
  // Fallback: merchantId from manual localStorage config
  const merchantId = session?.merchantId || credentials?.merchantId || '';
  const apiKey = credentials?.apiKey || '';

  // Auto-configure API key for demo merchant when JWT provides merchantId
  // but no API key is stored locally
  useEffect(() => {
    if (session?.merchantId && !credentials?.apiKey) {
      if (session.merchantId === DEMO_MERCHANT_ID) {
        saveCredentials({ merchantId: DEMO_MERCHANT_ID, apiKey: DEMO_API_KEY });
      }
    }
  }, [session?.merchantId, credentials?.apiKey, saveCredentials]);

  return {
    credentials: merchantId ? { merchantId, apiKey } : null,
    merchantId: merchantId || undefined,
    apiKey: apiKey || undefined,
    hasCredentials: !!merchantId,
    saveCredentials,
    removeCredentials,
    session,
  };
}
