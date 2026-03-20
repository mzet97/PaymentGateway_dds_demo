'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { usePayments, queryKeys } from '@/lib/queries';
import { useAutoApiAccess } from '@/lib/useAutoApiAccess';
import { usePaymentUpdates } from '@/lib/useWebSocket';
import {
  Plus,
  Search,
  Download,
  CreditCard,
  RefreshCw,
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  Settings,
} from 'lucide-react';
import Link from 'next/link';

interface Payment {
  id: string;
  merchantId: string;
  amount: number;
  currency: string;
  status: string;
  method: string;
  customer: {
    email?: string;
    name?: string;
  };
  createdAt: string;
  processedAt?: string;
}

const ITEMS_PER_PAGE = 10;

export default function PaymentsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { merchantId, apiKey } = useAutoApiAccess();
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [currentPage, setCurrentPage] = useState(1);

  const { data, isLoading, error, refetch } = usePayments({
    merchantId,
    limit: 100,
    status: statusFilter !== 'all' ? statusFilter : undefined,
  });

  const { isConnected: isRealtimeConnected } = usePaymentUpdates(
    apiKey,
    (payment) => {
      if (!payment) return;
      if (merchantId && payment.merchantId && payment.merchantId !== merchantId) return;
      queryClient.invalidateQueries({ queryKey: queryKeys.payments });
    }
  );

  const getPayments = (): Payment[] => {
    if (!data?.payments) return [];
    return data.payments.map((p) => ({
      id: p.id || p.paymentId || '',
      merchantId: p.merchantId,
      amount: p.amount,
      currency: p.currency || 'BRL',
      status: p.status,
      method: p.method,
      customer: p.customer || { email: '', name: '' },
      createdAt: p.createdAt,
      processedAt: p.processedAt,
    }));
  };

  const payments = getPayments();

  const filteredPayments = payments.filter((payment) => {
    const matchesSearch =
      searchTerm === '' ||
      payment.id.toLowerCase().includes(searchTerm.toLowerCase()) ||
      (payment.customer?.email || '').toLowerCase().includes(searchTerm.toLowerCase()) ||
      (payment.customer?.name || '').toLowerCase().includes(searchTerm.toLowerCase());
    return matchesSearch;
  });

  const totalPages = Math.ceil(filteredPayments.length / ITEMS_PER_PAGE);
  const paginatedPayments = filteredPayments.slice(
    (currentPage - 1) * ITEMS_PER_PAGE,
    currentPage * ITEMS_PER_PAGE
  );

  const getStatusBadge = (status: string) => {
    const styles: Record<string, string> = {
      approved: 'bg-green-100 text-green-800',
      completed: 'bg-green-100 text-green-800',
      rejected: 'bg-red-100 text-red-800',
      failed: 'bg-red-100 text-red-800',
      pending: 'bg-yellow-100 text-yellow-800',
      processing: 'bg-yellow-100 text-yellow-800',
      captured: 'bg-blue-100 text-blue-800',
      refunded: 'bg-purple-100 text-purple-800',
    };

    return (
      <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${styles[status?.toLowerCase()] || 'bg-gray-100 text-gray-800'}`}>
        {status}
      </span>
    );
  };

  const formatCurrency = (amount: number, currency: string = 'BRL') => {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency }).format(amount);
  };

  const formatDate = (dateString: string) => {
    if (!dateString) return '-';
    return new Date(dateString).toLocaleString('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  if (!merchantId) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-6 text-center">
          <Settings className="w-12 h-12 text-yellow-500 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-yellow-900 mb-2">Configure Merchant Access</h3>
          <p className="text-yellow-800 mb-4">
            Configure your Merchant ID and API Key in Settings to view payments.
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
          <h3 className="text-lg font-semibold text-red-900 mb-2">Failed to load payments</h3>
          <p className="text-red-600 mb-4">There was an error loading the payments.</p>
          <button
            onClick={() => refetch()}
            className="inline-flex items-center px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors"
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
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Payments</h1>
          <p className={`text-xs mt-1 ${isRealtimeConnected ? 'text-green-600' : 'text-gray-500'}`}>
            {isRealtimeConnected ? 'Realtime connected' : 'Realtime disconnected'}
          </p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={() => refetch()}
            className="inline-flex items-center gap-2 px-3 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
          <button
            onClick={() => router.push('/payments/new')}
            className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
          >
            <Plus className="w-4 h-4" />
            New Payment
          </button>
        </div>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-4 mb-6">
        <div className="flex flex-col sm:flex-row gap-4">
          <div className="flex-1 relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              type="text"
              placeholder="Search by ID, email or name..."
              value={searchTerm}
              onChange={(e) => {
                setSearchTerm(e.target.value);
                setCurrentPage(1);
              }}
              className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            />
          </div>
          <div className="flex gap-4">
            <select
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value);
                setCurrentPage(1);
              }}
              className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            >
              <option value="all">All Status</option>
              <option value="pending">Pending</option>
              <option value="processing">Processing</option>
              <option value="approved">Approved</option>
              <option value="completed">Completed</option>
              <option value="rejected">Rejected</option>
              <option value="failed">Failed</option>
              <option value="captured">Captured</option>
              <option value="refunded">Refunded</option>
            </select>
            <button className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors">
              <Download className="w-4 h-4" />
              Export
            </button>
          </div>
        </div>
      </div>

      {/* Payments Table */}
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
        <table className="min-w-full divide-y divide-gray-200">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">ID</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Customer</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Amount</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Method</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Created</th>
            </tr>
          </thead>
          <tbody className="bg-white divide-y divide-gray-200">
            {paginatedPayments.length > 0 ? (
              paginatedPayments.map((payment) => (
                <tr
                  key={payment.id}
                  className="hover:bg-gray-50 transition-colors cursor-pointer"
                  onClick={() => router.push(`/payments/${payment.id}`)}
                >
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="flex items-center">
                      <CreditCard className="w-4 h-4 text-gray-400 mr-3" />
                      <span className="text-sm font-medium text-gray-900">{payment.id}</span>
                    </div>
                  </td>
                  <td className="px-6 py-4">
                    <div className="text-sm text-gray-900">{payment.customer?.name || '-'}</div>
                    <div className="text-sm text-gray-500">{payment.customer?.email || '-'}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm font-medium text-gray-900">
                      {formatCurrency(payment.amount, payment.currency)}
                    </div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <span className="text-sm text-gray-500 capitalize">{payment.method?.replace('_', ' ') || '-'}</span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">{getStatusBadge(payment.status)}</td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <span className="text-sm text-gray-500">{formatDate(payment.createdAt)}</span>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={6} className="px-6 py-12 text-center">
                  <p className="text-gray-500">No payments found</p>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {filteredPayments.length > 0 && (
        <div className="flex justify-between items-center mt-6">
          <p className="text-sm text-gray-500">
            Showing {(currentPage - 1) * ITEMS_PER_PAGE + 1} to {Math.min(currentPage * ITEMS_PER_PAGE, filteredPayments.length)} of {filteredPayments.length} payments
          </p>
          <div className="flex gap-2">
            <button
              onClick={() => setCurrentPage(currentPage - 1)}
              disabled={currentPage === 1}
              className="inline-flex items-center px-3 py-1.5 text-sm border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <ChevronLeft className="w-4 h-4" />
              Previous
            </button>
            {Array.from({ length: Math.min(5, totalPages) }, (_, i) => i + 1).map((page) => (
              <button
                key={page}
                onClick={() => setCurrentPage(page)}
                className={`px-3 py-1.5 text-sm border rounded ${
                  currentPage === page
                    ? 'bg-blue-600 text-white border-blue-600'
                    : 'border-gray-300 hover:bg-gray-50'
                }`}
              >
                {page}
              </button>
            ))}
            <button
              onClick={() => setCurrentPage(currentPage + 1)}
              disabled={currentPage === totalPages}
              className="inline-flex items-center px-3 py-1.5 text-sm border border-gray-300 rounded hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              Next
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
