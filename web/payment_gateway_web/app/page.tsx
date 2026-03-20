'use client';

import { usePayments, useStatistics } from '@/lib/queries';
import { useAutoApiAccess } from '@/lib/useAutoApiAccess';
import {
  CreditCard,
  DollarSign,
  TrendingUp,
  AlertCircle,
  CheckCircle,
  XCircle,
  Clock,
  RefreshCw,
  Settings,
} from 'lucide-react';
import Link from 'next/link';

// Types
interface PaymentStats {
  totalTransactions: number;
  totalAmount: number;
  approvedCount: number;
  rejectedCount: number;
  pendingCount: number;
  averageAmount: number;
  byStatus?: Record<string, number>;
  byMethod?: Record<string, number>;
}

interface RecentPayment {
  id: string;
  amount: number;
  currency: string;
  status: string;
  customer: string;
  createdAt: string;
  method?: string;
}

export default function Dashboard() {
  const { merchantId } = useAutoApiAccess();

  const { data: statsData, isLoading: statsLoading, error: statsError, refetch: refetchStats } = useStatistics({
    merchantId,
  });
  const { data: paymentsData, isLoading: paymentsLoading, error: paymentsError, refetch: refetchPayments } = usePayments({
    merchantId,
    limit: 5,
  });

  const getStats = (): PaymentStats => {
    if (!statsData) {
      return {
        totalTransactions: 0,
        totalAmount: 0,
        approvedCount: 0,
        rejectedCount: 0,
        pendingCount: 0,
        averageAmount: 0,
      };
    }
    return {
      totalTransactions: statsData.totalTransactions || 0,
      totalAmount: statsData.totalAmount || 0,
      approvedCount: statsData.byStatus?.approved || 0,
      rejectedCount: statsData.byStatus?.rejected || 0,
      pendingCount: statsData.byStatus?.pending || 0,
      averageAmount: statsData.averageAmount || 0,
      byStatus: statsData.byStatus,
      byMethod: statsData.byMethod,
    };
  };

  const getRecentPayments = (): RecentPayment[] => {
    if (!paymentsData?.payments) return [];
    return paymentsData.payments.map((p) => ({
      id: p.id || p.paymentId || '',
      amount: p.amount,
      currency: p.currency || 'BRL',
      status: p.status,
      customer: p.customer?.email || p.customer?.name || 'Unknown',
      createdAt: p.createdAt,
      method: p.method,
    }));
  };

  const stats = getStats();
  const recentPayments = getRecentPayments();
  const isLoading = statsLoading || paymentsLoading;
  const error = statsError || paymentsError;

  const getStatusIcon = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'approved':
      case 'completed':
        return <CheckCircle className="w-5 h-5 text-green-500" />;
      case 'rejected':
      case 'failed':
        return <XCircle className="w-5 h-5 text-red-500" />;
      case 'pending':
      case 'processing':
        return <Clock className="w-5 h-5 text-yellow-500" />;
      default:
        return <AlertCircle className="w-5 h-5 text-gray-500" />;
    }
  };

  const formatCurrency = (amount: number, currency: string = 'BRL') => {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency,
    }).format(amount);
  };

  if (!merchantId) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-6 text-center">
          <Settings className="w-12 h-12 text-yellow-500 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-yellow-900 mb-2">Configure Merchant Access</h3>
          <p className="text-yellow-800 mb-4">
            Your Authentik account does not include a <code>merchant_id</code> claim.
            Configure your Merchant ID and API Key manually in Settings.
          </p>
          <Link
            href="/settings"
            className="inline-flex items-center px-4 py-2 bg-yellow-600 text-white rounded-lg hover:bg-yellow-700 transition-colors"
          >
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
          <h3 className="text-lg font-semibold text-red-900 mb-2">Failed to load data</h3>
          <p className="text-red-600 mb-4">There was an error loading the dashboard data.</p>
          <div className="flex gap-3 justify-center">
            <button
              onClick={() => {
                refetchStats();
                refetchPayments();
              }}
              className="inline-flex items-center px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors"
            >
              <RefreshCw className="w-4 h-4 mr-2" />
              Retry
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex items-center justify-between mb-8">
        <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>
        <button
          onClick={() => {
            refetchStats();
            refetchPayments();
          }}
          className="inline-flex items-center px-3 py-2 text-sm text-gray-600 hover:text-gray-900 transition-colors"
        >
          <RefreshCw className="w-4 h-4 mr-2" />
          Refresh
        </button>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
        <StatCard
          title="Total Transactions"
          value={stats.totalTransactions.toLocaleString()}
          icon={<CreditCard className="w-6 h-6 text-blue-600" />}
        />
        <StatCard
          title="Total Amount"
          value={formatCurrency(stats.totalAmount)}
          icon={<DollarSign className="w-6 h-6 text-green-600" />}
        />
        <StatCard
          title="Approved Rate"
          value={stats.totalTransactions > 0 ? ((stats.approvedCount / stats.totalTransactions) * 100).toFixed(1) + '%' : '0%'}
          icon={<TrendingUp className="w-6 h-6 text-purple-600" />}
        />
        <StatCard
          title="Average Amount"
          value={formatCurrency(stats.averageAmount)}
          icon={<CreditCard className="w-6 h-6 text-orange-600" />}
        />
      </div>

      {/* Status Overview */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
        <StatusCard
          title="Approved"
          count={stats.approvedCount}
          color="green"
          icon={<CheckCircle className="w-6 h-6" />}
        />
        <StatusCard
          title="Pending"
          count={stats.pendingCount}
          color="yellow"
          icon={<Clock className="w-6 h-6" />}
        />
        <StatusCard
          title="Rejected"
          count={stats.rejectedCount}
          color="red"
          icon={<XCircle className="w-6 h-6" />}
        />
      </div>

      {/* Recent Payments */}
      <div className="bg-white rounded-lg shadow-sm border border-gray-200">
        <div className="px-6 py-4 border-b border-gray-200">
          <h2 className="text-lg font-semibold text-gray-900">Recent Payments</h2>
        </div>
        {recentPayments.length > 0 ? (
          <div className="divide-y divide-gray-200">
            {recentPayments.map((payment) => (
              <div
                key={payment.id}
                className="px-6 py-4 flex items-center justify-between hover:bg-gray-50 transition-colors"
              >
                <div className="flex items-center gap-4">
                  {getStatusIcon(payment.status)}
                  <div>
                    <p className="font-medium text-gray-900">{payment.id}</p>
                    <p className="text-sm text-gray-500">{payment.customer}</p>
                  </div>
                </div>
                <div className="text-right">
                  <p className="font-semibold text-gray-900">
                    {formatCurrency(payment.amount, payment.currency)}
                  </p>
                  <p className="text-sm text-gray-500 capitalize">{payment.status}</p>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="px-6 py-8 text-center text-gray-500">
            No payments found. Create your first payment to see it here.
          </div>
        )}
        <div className="px-6 py-4 border-t border-gray-200">
          <Link
            href="/payments"
            className="text-sm text-blue-600 hover:text-blue-800 font-medium"
          >
            View all payments →
          </Link>
        </div>
      </div>

      {/* Quick Actions */}
      <div className="mt-8 grid grid-cols-1 md:grid-cols-3 gap-6">
        <QuickAction
          title="New Payment"
          description="Create a new payment"
          href="/payments/new"
          buttonText="Create Payment"
        />
        <QuickAction
          title="Merchant Portal"
          description="Manage your merchants"
          href="/merchants"
          buttonText="View Merchants"
        />
        <QuickAction
          title="View Analytics"
          description="Check transaction analytics"
          href="/analytics"
          buttonText="View Analytics"
        />
      </div>
    </div>
  );
}

function StatCard({
  title,
  value,
  icon,
}: {
  title: string;
  value: string;
  icon: React.ReactNode;
}) {
  return (
    <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-4">
        <span className="text-sm font-medium text-gray-500">{title}</span>
        {icon}
      </div>
      <span className="text-2xl font-bold text-gray-900">{value}</span>
    </div>
  );
}

function StatusCard({
  title,
  count,
  color,
  icon,
}: {
  title: string;
  count: number;
  color: 'green' | 'yellow' | 'red';
  icon: React.ReactNode;
}) {
  const colors = {
    green: 'bg-green-50 border-green-200 text-green-800',
    yellow: 'bg-yellow-50 border-yellow-200 text-yellow-800',
    red: 'bg-red-50 border-red-200 text-red-800',
  };

  const iconColors = {
    green: 'text-green-600',
    yellow: 'text-yellow-600',
    red: 'text-red-600',
  };

  return (
    <div className={`rounded-lg border p-6 ${colors[color]}`}>
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium opacity-80">{title}</p>
          <p className="text-3xl font-bold mt-1">{count.toLocaleString()}</p>
        </div>
        <div className={iconColors[color]}>{icon}</div>
      </div>
    </div>
  );
}

function QuickAction({
  title,
  description,
  href,
  buttonText,
}: {
  title: string;
  description: string;
  href: string;
  buttonText: string;
}) {
  return (
    <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
      <h3 className="text-lg font-semibold text-gray-900 mb-2">{title}</h3>
      <p className="text-sm text-gray-500 mb-4">{description}</p>
      <Link
        href={href}
        className="inline-flex items-center justify-center w-full px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
      >
        {buttonText}
      </Link>
    </div>
  );
}
