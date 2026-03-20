'use client';

import { AxiosError } from 'axios';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { AlertCircle, ArrowLeft, CheckCircle, CreditCard, Loader2, Settings, User } from 'lucide-react';
import { useCreatePayment } from '@/lib/queries';
import { useAutoApiAccess } from '@/lib/useAutoApiAccess';
import Link from 'next/link';

interface PaymentForm {
  amount: string;
  currency: string;
  method: string;
  customerName: string;
  customerEmail: string;
  customerDocument: string;
  customerPhone: string;
  customerIp: string;
  description: string;
}

interface FormErrors {
  merchantId?: string;
  amount?: string;
  customerName?: string;
  customerEmail?: string;
  customerDocument?: string;
}

type ApiError = {
  error?: string;
  message?: string;
};

export default function NewPaymentPage() {
  const router = useRouter();
  const createPayment = useCreatePayment();
  const { merchantId } = useAutoApiAccess();

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const [errors, setErrors] = useState<FormErrors>({});

  const [form, setForm] = useState<PaymentForm>({
    amount: '',
    currency: 'BRL',
    method: 'credit_card',
    customerName: '',
    customerEmail: '',
    customerDocument: '',
    customerPhone: '',
    customerIp: '',
    description: '',
  });

  const validateForm = (): boolean => {
    const newErrors: FormErrors = {};

    if (!merchantId) {
      newErrors.merchantId = 'Merchant access is not configured.';
    }
    if (!form.amount || parseFloat(form.amount) <= 0) {
      newErrors.amount = 'Please enter a valid amount';
    }
    if (!form.customerName.trim()) {
      newErrors.customerName = 'Customer name is required';
    }
    if (!form.customerEmail.trim() || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.customerEmail)) {
      newErrors.customerEmail = 'Please enter a valid email';
    }
    if (!form.customerDocument.trim()) {
      newErrors.customerDocument = 'Document is required';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const generateIdempotencyKey = (): string => {
    return `idem_${Date.now()}_${Math.random().toString(36).substring(2, 15)}`;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!validateForm() || !merchantId) return;

    setLoading(true);

    try {
      await createPayment.mutateAsync({
        merchantId,
        amount: parseFloat(form.amount),
        currency: form.currency,
        method: form.method,
        customer: {
          name: form.customerName,
          email: form.customerEmail,
          document: form.customerDocument,
          phone: form.customerPhone || undefined,
          ip: form.customerIp || undefined,
        },
        description: form.description || undefined,
        idempotencyKey: generateIdempotencyKey(),
      });

      setSuccess(true);
      setTimeout(() => {
        router.push('/payments');
      }, 2000);
    } catch (err) {
      const axiosError = err as AxiosError<ApiError>;
      const errorMessage =
        axiosError.response?.data?.error ??
        axiosError.response?.data?.message ??
        'Failed to create payment. Please try again.';
      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  const handleChange = (field: keyof PaymentForm, value: string) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    if (errors[field as keyof FormErrors]) {
      setErrors((prev) => ({ ...prev, [field]: undefined }));
    }
  };

  if (!merchantId) {
    return (
      <div className="max-w-3xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-6 text-center">
          <Settings className="w-12 h-12 text-yellow-500 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-yellow-900 mb-2">Configure Merchant Access</h3>
          <p className="text-yellow-800 mb-4">
            Configure your Merchant ID and API Key in Settings before creating payments.
          </p>
          <Link href="/settings" className="inline-flex items-center px-4 py-2 bg-yellow-600 text-white rounded-lg hover:bg-yellow-700 transition-colors">
            Go to Settings
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-3xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <button
        onClick={() => router.back()}
        className="flex items-center gap-2 text-gray-600 hover:text-gray-900 mb-6"
      >
        <ArrowLeft className="w-4 h-4" />
        Back to Payments
      </button>

      <h1 className="text-2xl font-bold text-gray-900 mb-8">New Payment</h1>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-6 flex items-center gap-2">
          <AlertCircle className="w-5 h-5 flex-shrink-0" />
          {error}
        </div>
      )}

      {success && (
        <div className="bg-green-50 border border-green-200 text-green-700 px-4 py-3 rounded-lg mb-6 flex items-center gap-2">
          <CheckCircle className="w-5 h-5 flex-shrink-0" />
          Payment created successfully! Redirecting...
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-8">
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
            <CreditCard className="w-5 h-5" />
            Merchant
          </h2>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Merchant ID</label>
            <input
              type="text"
              value={merchantId}
              readOnly
              className="w-full px-4 py-2 border border-gray-300 bg-gray-50 rounded-lg text-gray-700"
            />
            {errors.merchantId && <p className="mt-1 text-sm text-red-600">{errors.merchantId}</p>}
          </div>
        </div>

        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
            <CreditCard className="w-5 h-5" />
            Payment Details
          </h2>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="md:col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Amount <span className="text-red-500">*</span>
              </label>
              <input
                type="number"
                step="0.01"
                min="0.01"
                required
                value={form.amount}
                onChange={(e) => handleChange('amount', e.target.value)}
                className={`w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 ${
                  errors.amount ? 'border-red-300 bg-red-50' : 'border-gray-300'
                }`}
                placeholder="0.00"
              />
              {errors.amount && <p className="mt-1 text-sm text-red-600">{errors.amount}</p>}
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Currency</label>
              <select
                value={form.currency}
                onChange={(e) => handleChange('currency', e.target.value)}
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              >
                <option value="BRL">BRL - Brazilian Real</option>
                <option value="USD">USD - US Dollar</option>
                <option value="EUR">EUR - Euro</option>
              </select>
            </div>
          </div>

          <div className="mt-4">
            <label className="block text-sm font-medium text-gray-700 mb-1">Payment Method</label>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
              {[
                { value: 'credit_card', label: 'Credit Card' },
                { value: 'debit_card', label: 'Debit Card' },
                { value: 'pix', label: 'PIX' },
                { value: 'boleto', label: 'Boleto' },
              ].map((method) => (
                <label
                  key={method.value}
                  className={`flex items-center justify-center p-3 border rounded-lg cursor-pointer transition-colors ${
                    form.method === method.value
                      ? 'border-blue-500 bg-blue-50 text-blue-700'
                      : 'border-gray-300 hover:bg-gray-50'
                  }`}
                >
                  <input
                    type="radio"
                    name="method"
                    value={method.value}
                    checked={form.method === method.value}
                    onChange={(e) => handleChange('method', e.target.value)}
                    className="sr-only"
                  />
                  <span className="text-sm font-medium">{method.label}</span>
                </label>
              ))}
            </div>
          </div>

          <div className="mt-4">
            <label className="block text-sm font-medium text-gray-700 mb-1">Description (optional)</label>
            <input
              type="text"
              value={form.description}
              onChange={(e) => handleChange('description', e.target.value)}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              placeholder="Order #12345"
            />
          </div>
        </div>

        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
            <User className="w-5 h-5" />
            Customer Information
          </h2>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                required
                value={form.customerName}
                onChange={(e) => handleChange('customerName', e.target.value)}
                className={`w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 ${
                  errors.customerName ? 'border-red-300 bg-red-50' : 'border-gray-300'
                }`}
                placeholder="John Doe"
              />
              {errors.customerName && <p className="mt-1 text-sm text-red-600">{errors.customerName}</p>}
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Email <span className="text-red-500">*</span>
              </label>
              <input
                type="email"
                required
                value={form.customerEmail}
                onChange={(e) => handleChange('customerEmail', e.target.value)}
                className={`w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 ${
                  errors.customerEmail ? 'border-red-300 bg-red-50' : 'border-gray-300'
                }`}
                placeholder="john@example.com"
              />
              {errors.customerEmail && <p className="mt-1 text-sm text-red-600">{errors.customerEmail}</p>}
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Document (CPF/CNPJ) <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                required
                value={form.customerDocument}
                onChange={(e) => handleChange('customerDocument', e.target.value)}
                className={`w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 ${
                  errors.customerDocument ? 'border-red-300 bg-red-50' : 'border-gray-300'
                }`}
                placeholder="123.456.789-00"
              />
              {errors.customerDocument && (
                <p className="mt-1 text-sm text-red-600">{errors.customerDocument}</p>
              )}
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Phone</label>
              <input
                type="tel"
                value={form.customerPhone}
                onChange={(e) => handleChange('customerPhone', e.target.value)}
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                placeholder="+55 11 99999-9999"
              />
            </div>

            <div className="md:col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">IP Address</label>
              <input
                type="text"
                value={form.customerIp}
                onChange={(e) => handleChange('customerIp', e.target.value)}
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                placeholder="192.168.1.1"
              />
            </div>
          </div>
        </div>

        <div className="flex justify-end gap-4">
          <button
            type="button"
            onClick={() => router.back()}
            disabled={loading}
            className="px-6 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={loading || success}
            className="inline-flex items-center gap-2 px-6 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {loading && <Loader2 className="w-4 h-4 animate-spin" />}
            {loading ? 'Creating...' : 'Create Payment'}
          </button>
        </div>
      </form>
    </div>
  );
}
