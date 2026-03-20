'use client';

import { AlertCircle, Building, Globe, Mail, RefreshCw, Settings } from 'lucide-react';
import { useMerchant } from '@/lib/queries';
import { useAutoApiAccess } from '@/lib/useAutoApiAccess';
import Link from 'next/link';

export default function MerchantsPage() {
  const { merchantId } = useAutoApiAccess();
  const { data: merchant, isLoading, error, refetch } = useMerchant(merchantId ?? '');

  if (!merchantId) {
    return (
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-6 text-center">
          <Settings className="w-12 h-12 text-yellow-500 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-yellow-900 mb-2">Configure Merchant Access</h3>
          <p className="text-yellow-800 mb-4">
            Configure your Merchant ID and API Key in Settings to view merchant data.
          </p>
          <Link href="/settings" className="inline-flex items-center px-4 py-2 bg-yellow-600 text-white rounded-lg hover:bg-yellow-700 transition-colors">
            Go to Settings
          </Link>
        </div>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  if (error || !merchant) {
    return (
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-red-50 border border-red-200 rounded-lg p-6 text-center">
          <AlertCircle className="w-12 h-12 text-red-500 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-red-900 mb-2">Failed to load merchant</h3>
          <button
            onClick={() => refetch()}
            className="inline-flex items-center px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700"
          >
            <RefreshCw className="w-4 h-4 mr-2" />
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-2xl font-bold text-gray-900">Merchant</h1>
        <button
          onClick={() => refetch()}
          className="inline-flex items-center gap-2 px-3 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
        >
          <RefreshCw className="w-4 h-4" />
          Refresh
        </button>
      </div>

      <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
        <div className="flex items-start justify-between mb-6">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-blue-100 rounded-full flex items-center justify-center">
              <Building className="w-5 h-5 text-blue-600" />
            </div>
            <div>
              <h2 className="text-xl font-semibold text-gray-900">{merchant.name}</h2>
              <p className="text-sm text-gray-500">ID: {merchant.id}</p>
            </div>
          </div>
          <Link
            href={`/merchants/${merchant.id}`}
            className="text-sm font-medium text-blue-600 hover:text-blue-700"
          >
            View details
          </Link>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
          <div className="flex items-center gap-2 text-gray-700">
            <Mail className="w-4 h-4" />
            {merchant.email}
          </div>
          <div className="text-gray-700">
            <span className="font-medium">Document:</span> {merchant.document}
          </div>
          <div className="text-gray-700">
            <span className="font-medium">Category:</span> {merchant.category}
          </div>
          <div className="text-gray-700">
            <span className="font-medium">Status:</span> {merchant.status ?? 'unknown'}
          </div>
          {merchant.callbackUrl && (
            <div className="md:col-span-2 flex items-center gap-2 text-gray-700">
              <Globe className="w-4 h-4" />
              <span className="truncate">{merchant.callbackUrl}</span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
