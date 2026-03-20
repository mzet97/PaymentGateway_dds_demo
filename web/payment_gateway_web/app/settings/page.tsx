'use client';

import { AxiosError } from 'axios';
import { useState } from 'react';
import { signOut, useSession } from 'next-auth/react';
import { AlertCircle, CheckCircle, Key, LogOut, Save, Shield, User } from 'lucide-react';
import apiClient from '@/lib/apiClient';
import { useApiAccess } from '@/lib/useApiAccess';

interface ApiError {
  error?: string;
  message?: string;
}

export default function SettingsPage() {
  const { data: session } = useSession();
  const { credentials, saveCredentials, removeCredentials } = useApiAccess();

  const [activeTab, setActiveTab] = useState<'profile' | 'apiaccess'>('profile');
  const [merchantIdInput, setMerchantIdInput] = useState<string | null>(null);
  const [apiKeyInput, setApiKeyInput] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [validationState, setValidationState] = useState<{
    status: 'idle' | 'loading' | 'success' | 'error';
    message?: string;
  }>({ status: 'idle' });

  const merchantId = merchantIdInput ?? credentials?.merchantId ?? '';
  const apiKey = apiKeyInput ?? credentials?.apiKey ?? '';

  const handleSaveAccess = () => {
    if (!merchantId.trim() || !apiKey.trim()) {
      setValidationState({
        status: 'error',
        message: 'Merchant ID and API key are required.',
      });
      return;
    }

    saveCredentials({
      merchantId: merchantId.trim(),
      apiKey: apiKey.trim(),
    });

    setSaved(true);
    setTimeout(() => setSaved(false), 3000);
  };

  const handleClearAccess = () => {
    removeCredentials();
    setMerchantIdInput('');
    setApiKeyInput('');
    setValidationState({ status: 'idle' });
  };

  const handleValidateAccess = async () => {
    if (!merchantId.trim() || !apiKey.trim()) {
      setValidationState({
        status: 'error',
        message: 'Merchant ID and API key are required for validation.',
      });
      return;
    }

    setValidationState({ status: 'loading' });
    saveCredentials({
      merchantId: merchantId.trim(),
      apiKey: apiKey.trim(),
    });

    try {
      const merchant = await apiClient.getMerchant(merchantId.trim());
      const name = (merchant as { name?: string }).name ?? merchantId.trim();
      setValidationState({
        status: 'success',
        message: `Access validated for merchant: ${name}`,
      });
    } catch (err) {
      const axiosError = err as AxiosError<ApiError>;
      setValidationState({
        status: 'error',
        message:
          axiosError.response?.data?.error ??
          axiosError.response?.data?.message ??
          'Failed to validate access with backend.',
      });
    }
  };

  const tabs = [
    { id: 'profile', label: 'Profile', icon: User },
    { id: 'apiaccess', label: 'API Access', icon: Key },
  ] as const;

  return (
    <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-2xl font-bold text-gray-900 mb-8">Settings</h1>

      <div className="flex border-b border-gray-200 mb-6">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`flex items-center gap-2 px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === tab.id
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            <tab.icon className="w-4 h-4" />
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === 'profile' && (
        <div className="space-y-6">
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <h2 className="text-lg font-semibold text-gray-900 mb-6">Profile</h2>
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Name</label>
                <input
                  type="text"
                  value={session?.user?.name ?? ''}
                  readOnly
                  className="w-full px-4 py-2 border border-gray-200 rounded-lg bg-gray-50 text-gray-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
                <input
                  type="email"
                  value={session?.user?.email ?? ''}
                  readOnly
                  className="w-full px-4 py-2 border border-gray-200 rounded-lg bg-gray-50 text-gray-500"
                />
              </div>
              <div className="flex justify-end pt-4 border-t border-gray-200">
                <button
                  onClick={() => signOut({ callbackUrl: '/auth/signin' })}
                  className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-red-600 hover:bg-red-50 rounded-lg transition-colors"
                >
                  <LogOut className="w-4 h-4" />
                  Sign Out
                </button>
              </div>
            </div>
          </div>

          {/* Session Info */}
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <h2 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
              <Shield className="w-5 h-5" />
              Session Info
            </h2>
            <div className="space-y-3 text-sm">
              <div className="flex justify-between">
                <span className="text-gray-500">Authentication</span>
                <span className="text-green-600 font-medium">Authentik OIDC</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500">Merchant ID (JWT)</span>
                <span className="font-mono text-gray-700">
                  {session?.merchantId || <span className="text-yellow-600">Not in token</span>}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500">Roles</span>
                <span className="text-gray-700">
                  {session?.roles?.length ? session.roles.join(', ') : 'None'}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500">Token Expires</span>
                <span className="text-gray-700">
                  {session?.expiresAt
                    ? new Date(session.expiresAt * 1000).toLocaleString('pt-BR')
                    : '-'}
                </span>
              </div>
            </div>
          </div>
        </div>
      )}

      {activeTab === 'apiaccess' && (
        <div className="space-y-6">
          {session?.merchantId && (
            <div className="bg-green-50 border border-green-200 rounded-lg p-4">
              <div className="flex items-start gap-3">
                <CheckCircle className="w-5 h-5 text-green-600 flex-shrink-0 mt-0.5" />
                <div>
                  <p className="text-sm font-medium text-green-800">JWT Authentication Active</p>
                  <p className="text-sm text-green-700 mt-1">
                    Your Authentik session includes merchant ID <code className="font-mono bg-green-100 px-1 rounded">{session.merchantId}</code>.
                    API calls use your Bearer token automatically. The API Key below is optional for additional authorization.
                  </p>
                </div>
              </div>
            </div>
          )}

          {!session?.merchantId && (
            <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
              <div className="flex items-start gap-3">
                <AlertCircle className="w-5 h-5 text-yellow-600 flex-shrink-0 mt-0.5" />
                <div>
                  <p className="text-sm font-medium text-yellow-800">Manual Configuration Required</p>
                  <p className="text-sm text-yellow-700 mt-1">
                    Your Authentik token does not include a <code>merchant_id</code> claim.
                    Configure your Merchant ID and API Key below to access merchant-specific data.
                  </p>
                </div>
              </div>
            </div>
          )}

          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">Merchant API Access</h2>
            <p className="text-sm text-gray-600 mb-6">
              Configure the merchant credentials used by this frontend to call backend endpoints.
              {session?.merchantId && ' These override the JWT-derived merchant ID.'}
            </p>

            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Merchant ID</label>
                <input
                  type="text"
                  value={merchantId}
                  onChange={(e) => setMerchantIdInput(e.target.value)}
                  placeholder="00000000-0000-0000-0000-000000000000"
                  className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">API Key</label>
                <input
                  type="password"
                  value={apiKey}
                  onChange={(e) => setApiKeyInput(e.target.value)}
                  placeholder="Paste merchant API key"
                  className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                />
              </div>

              <div className="flex flex-wrap gap-3 pt-2">
                <button
                  onClick={handleSaveAccess}
                  className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors"
                >
                  <Save className="w-4 h-4" />
                  Save Access
                </button>
                <button
                  onClick={handleValidateAccess}
                  className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
                >
                  Validate with Backend
                </button>
                <button
                  onClick={handleClearAccess}
                  className="px-4 py-2 text-sm font-medium text-red-600 bg-white border border-red-200 rounded-lg hover:bg-red-50"
                >
                  Clear Access
                </button>
              </div>

              {saved && (
                <div className="flex items-center gap-2 text-green-600 text-sm">
                  <CheckCircle className="w-4 h-4" />
                  API access saved locally.
                </div>
              )}

              {validationState.status === 'loading' && (
                <p className="text-sm text-gray-500">Validating access...</p>
              )}

              {validationState.status === 'success' && (
                <div className="flex items-center gap-2 text-green-600 text-sm">
                  <CheckCircle className="w-4 h-4" />
                  {validationState.message}
                </div>
              )}

              {validationState.status === 'error' && (
                <div className="flex items-center gap-2 text-red-600 text-sm">
                  <AlertCircle className="w-4 h-4" />
                  {validationState.message}
                </div>
              )}
            </div>
          </div>

          <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
            <div className="flex items-start gap-3">
              <AlertCircle className="w-5 h-5 text-yellow-600 flex-shrink-0 mt-0.5" />
              <div>
                <p className="text-sm font-medium text-yellow-800">Security Notice</p>
                <p className="text-sm text-yellow-700 mt-1">
                  This key grants access to merchant resources. Keep it private and rotate it if exposed.
                  API keys are stored in your browser&apos;s localStorage.
                </p>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
