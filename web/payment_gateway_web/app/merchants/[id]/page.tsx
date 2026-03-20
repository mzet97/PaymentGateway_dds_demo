'use client';

import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useMerchant, useMerchantPayments } from '@/lib/queries';

export default function MerchantDetailsPage() {
  const params = useParams<{ id: string }>();
  const merchantId = params?.id ?? '';

  const { data: merchant, isLoading, error } = useMerchant(merchantId);
  const { data: payments } = useMerchantPayments(merchantId, { limit: 20, offset: 0 });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  if (error || !merchant) {
    return (
      <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-red-50 border border-red-200 rounded-lg p-6">
          <p className="text-red-700">Não foi possível carregar os detalhes do merchant.</p>
          <Link href="/merchants" className="inline-block mt-3 text-sm text-blue-600 hover:text-blue-700">
            Voltar
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">{merchant.name}</h1>
          <p className="text-sm text-gray-500">Merchant ID: {merchant.id}</p>
        </div>
        <Link href="/merchants" className="text-sm text-blue-600 hover:text-blue-700">
          Voltar
        </Link>
      </div>

      <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
          <p><span className="font-medium">Email:</span> {merchant.email}</p>
          <p><span className="font-medium">Document:</span> {merchant.document}</p>
          <p><span className="font-medium">Category:</span> {merchant.category}</p>
          <p><span className="font-medium">Status:</span> {merchant.status ?? 'unknown'}</p>
          {merchant.callbackUrl && (
            <p className="md:col-span-2">
              <span className="font-medium">Callback URL:</span> {merchant.callbackUrl}
            </p>
          )}
        </div>
      </div>

      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-200">
          <h2 className="text-lg font-semibold text-gray-900">Pagamentos recentes</h2>
        </div>
        <table className="min-w-full divide-y divide-gray-200">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">ID</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Valor</th>
            </tr>
          </thead>
          <tbody className="bg-white divide-y divide-gray-200">
            {(payments?.payments ?? []).length === 0 ? (
              <tr>
                <td className="px-6 py-4 text-sm text-gray-500" colSpan={3}>
                  Nenhum pagamento encontrado.
                </td>
              </tr>
            ) : (
              (payments?.payments ?? []).map((payment) => (
                <tr key={payment.id}>
                  <td className="px-6 py-4 text-sm text-gray-900">{payment.id}</td>
                  <td className="px-6 py-4 text-sm text-gray-700">{payment.status}</td>
                  <td className="px-6 py-4 text-sm text-gray-700">
                    {new Intl.NumberFormat('pt-BR', {
                      style: 'currency',
                      currency: payment.currency || 'BRL',
                    }).format(payment.amount)}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
