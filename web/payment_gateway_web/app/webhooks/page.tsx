'use client';

import { AxiosError } from 'axios';
import { useState } from 'react';
import { AlertCircle, Plus, RefreshCw, Settings, ToggleLeft, ToggleRight, Trash2, Webhook } from 'lucide-react';
import { useCreateWebhook, useDeleteWebhook, useWebhooks } from '@/lib/queries';
import { useAutoApiAccess } from '@/lib/useAutoApiAccess';
import Link from 'next/link';

interface ApiError {
  error?: string;
  message?: string;
}

const EVENTS = [
  'payment.created',
  'payment.approved',
  'payment.rejected',
  'payment.refunded',
  'payment.captured',
];

export default function WebhooksPage() {
  const { merchantId } = useAutoApiAccess();

  const { data, isLoading, error, refetch } = useWebhooks(merchantId ?? '');
  const createWebhook = useCreateWebhook();
  const deleteWebhook = useDeleteWebhook();

  const [showForm, setShowForm] = useState(false);
  const [formData, setFormData] = useState({
    url: '',
    events: [] as string[],
    secret: '',
  });
  const [formLoading, setFormLoading] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);

  const webhooks = data?.webhooks ?? [];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!merchantId) return;

    setFormError(null);
    setFormLoading(true);

    try {
      await createWebhook.mutateAsync({
        merchantId,
        url: formData.url,
        events: formData.events,
        secret: formData.secret || undefined,
        active: true,
      });
      setShowForm(false);
      setFormData({ url: '', events: [], secret: '' });
      refetch();
    } catch (err) {
      const axiosError = err as AxiosError<ApiError>;
      setFormError(
        axiosError.response?.data?.error ??
          axiosError.response?.data?.message ??
          'Failed to create webhook'
      );
    } finally {
      setFormLoading(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteWebhook.mutateAsync(id);
      setDeleteConfirm(null);
      refetch();
    } catch (err) {
      console.error('Failed to delete webhook:', err);
    }
  };

  const toggleEvent = (event: string) => {
    setFormData((prev) => ({
      ...prev,
      events: prev.events.includes(event)
        ? prev.events.filter((item) => item !== event)
        : [...prev.events, event],
    }));
  };

  if (!merchantId) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-6 text-center">
          <Settings className="w-12 h-12 text-yellow-500 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-yellow-900 mb-2">Configure Merchant Access</h3>
          <p className="text-yellow-800 mb-4">
            Configure your Merchant ID and API Key in Settings to manage webhooks.
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

  if (error) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-red-50 border border-red-200 rounded-lg p-6 text-center">
          <AlertCircle className="w-12 h-12 text-red-500 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-red-900 mb-2">Failed to load webhooks</h3>
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
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-2xl font-bold text-gray-900">Webhooks</h1>
        <div className="flex gap-2">
          <button
            onClick={() => refetch()}
            className="inline-flex items-center gap-2 px-3 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
          <button
            onClick={() => setShowForm(!showForm)}
            className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700"
          >
            <Plus className="w-4 h-4" />
            Add Webhook
          </button>
        </div>
      </div>

      {showForm && (
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6 mb-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4">New Webhook</h2>
          {formError && (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4">
              {formError}
            </div>
          )}
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Merchant ID</label>
              <input
                type="text"
                value={merchantId}
                readOnly
                className="w-full px-4 py-2 border border-gray-300 bg-gray-50 rounded-lg text-gray-700"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">URL *</label>
              <input
                type="url"
                required
                value={formData.url}
                onChange={(e) => setFormData({ ...formData, url: e.target.value })}
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="https://example.com/webhook"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">Events</label>
              <div className="flex flex-wrap gap-2">
                {EVENTS.map((event) => (
                  <button
                    key={event}
                    type="button"
                    onClick={() => toggleEvent(event)}
                    className={`px-3 py-1.5 text-sm rounded-lg border transition-colors ${
                      formData.events.includes(event)
                        ? 'bg-blue-100 border-blue-500 text-blue-700'
                        : 'bg-white border-gray-300 text-gray-700 hover:bg-gray-50'
                    }`}
                  >
                    {event}
                  </button>
                ))}
              </div>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Secret (optional)</label>
              <input
                type="password"
                value={formData.secret}
                onChange={(e) => setFormData({ ...formData, secret: e.target.value })}
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="Webhook secret for HMAC"
              />
            </div>
            <div className="flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setShowForm(false)}
                className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={formLoading || formData.events.length === 0}
                className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 disabled:opacity-50"
              >
                {formLoading ? 'Creating...' : 'Create Webhook'}
              </button>
            </div>
          </form>
        </div>
      )}

      <div className="space-y-4">
        {webhooks.length > 0 ? (
          webhooks.map((webhook) => (
            <div key={webhook.id} className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <div className="flex items-start justify-between">
                <div className="flex items-start gap-4">
                  <div
                    className={`w-10 h-10 rounded-full flex items-center justify-center ${
                      webhook.active ? 'bg-green-100' : 'bg-gray-100'
                    }`}
                  >
                    <Webhook
                      className={`w-5 h-5 ${webhook.active ? 'text-green-600' : 'text-gray-500'}`}
                    />
                  </div>
                  <div>
                    <h3 className="font-semibold text-gray-900">{webhook.url}</h3>
                    <p className="text-sm text-gray-500 mt-1">{webhook.events.join(', ')}</p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {webhook.active ? (
                    <ToggleRight className="w-6 h-6 text-green-600" />
                  ) : (
                    <ToggleLeft className="w-6 h-6 text-gray-400" />
                  )}
                  {deleteConfirm === webhook.id ? (
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => handleDelete(webhook.id)}
                        className="px-3 py-1 text-sm text-red-600 hover:bg-red-50 rounded"
                      >
                        Confirm
                      </button>
                      <button
                        onClick={() => setDeleteConfirm(null)}
                        className="px-3 py-1 text-sm text-gray-600 hover:bg-gray-50 rounded"
                      >
                        Cancel
                      </button>
                    </div>
                  ) : (
                    <button
                      onClick={() => setDeleteConfirm(webhook.id)}
                      className="p-2 text-gray-400 hover:text-red-600 transition-colors"
                    >
                      <Trash2 className="w-5 h-5" />
                    </button>
                  )}
                </div>
              </div>
            </div>
          ))
        ) : (
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-12 text-center">
            <Webhook className="w-12 h-12 text-gray-400 mx-auto mb-4" />
            <p className="text-gray-500">No webhooks configured</p>
            <button onClick={() => setShowForm(true)} className="mt-4 text-blue-600 hover:text-blue-800 font-medium">
              Add your first webhook
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
