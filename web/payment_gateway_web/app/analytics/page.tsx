'use client';

import { useMemo, useState } from 'react';
import { useStatistics } from '@/lib/queries';
import { useAutoApiAccess } from '@/lib/useAutoApiAccess';
import {
  TrendingUp,
  DollarSign,
  CreditCard,
  RefreshCw,
  AlertCircle,
  Calendar,
  Settings,
  CheckCircle,
  XCircle,
  Clock,
} from 'lucide-react';
import Link from 'next/link';

const PERIODS = [
  { value: 'day', label: 'Hoje' },
  { value: 'week', label: 'Esta Semana' },
  { value: 'month', label: 'Este Mes' },
  { value: 'year', label: 'Este Ano' },
  { value: 'all', label: 'Todos' },
];

export default function AnalyticsPage() {
  const [period, setPeriod] = useState('all');
  const { merchantId } = useAutoApiAccess();

  const fromDate = useMemo(() => {
    const now = new Date();
    const from = new Date(now);
    if (period === 'day') from.setDate(now.getDate() - 1);
    else if (period === 'week') from.setDate(now.getDate() - 7);
    else if (period === 'month') from.setMonth(now.getMonth() - 1);
    else if (period === 'year') from.setFullYear(now.getFullYear() - 1);
    else return undefined;
    from.setSeconds(0, 0);
    return from.toISOString();
  }, [period]);

  const { data, isLoading, error, refetch } = useStatistics({
    merchantId,
    from: fromDate,
  });

  const stats = useMemo(() => {
    if (!data) return { totalTransactions: 0, totalAmount: 0, averageAmount: 0, approvedRate: 0, byStatus: {} as Record<string, number>, byMethod: {} as Record<string, number> };
    const total = data.totalTransactions || 0;
    const byStatus = data.byStatus || {};
    const approved = (byStatus.approved || 0) + (byStatus.completed || 0);
    return {
      totalTransactions: total,
      totalAmount: data.totalAmount || 0,
      averageAmount: data.averageAmount || 0,
      approvedRate: total > 0 ? (approved / total) * 100 : 0,
      byStatus,
      byMethod: data.byMethod || {},
    };
  }, [data]);

  const formatCurrency = (amount: number) =>
    new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(amount);

  if (!merchantId) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-6 text-center">
          <Settings className="w-12 h-12 text-yellow-500 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-yellow-900 mb-2">Configurar Acesso</h3>
          <Link href="/settings" className="inline-flex items-center px-4 py-2 bg-yellow-600 text-white rounded-lg hover:bg-yellow-700">Configuracoes</Link>
        </div>
      </div>
    );
  }

  if (isLoading) {
    return <div className="flex items-center justify-center min-h-[400px]"><div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div></div>;
  }

  if (error) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="bg-red-50 border border-red-200 rounded-lg p-6 text-center">
          <AlertCircle className="w-12 h-12 text-red-500 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-red-900 mb-2">Erro ao carregar analytics</h3>
          <button onClick={() => refetch()} className="inline-flex items-center px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700">
            <RefreshCw className="w-4 h-4 mr-2" /> Tentar novamente
          </button>
        </div>
      </div>
    );
  }

  const statusEntries = Object.entries(stats.byStatus).sort((a, b) => (b[1] as number) - (a[1] as number));
  const methodEntries = Object.entries(stats.byMethod).sort((a, b) => (b[1] as number) - (a[1] as number));
  const maxStatus = statusEntries.length > 0 ? Math.max(...statusEntries.map(([, v]) => v as number)) : 1;
  const maxMethod = methodEntries.length > 0 ? Math.max(...methodEntries.map(([, v]) => v as number)) : 1;

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-2xl font-bold text-gray-900">Analytics</h1>
        <button onClick={() => refetch()} className="inline-flex items-center gap-2 px-3 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50">
          <RefreshCw className="w-4 h-4" /> Atualizar
        </button>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-4 mb-6">
        <div className="flex items-center gap-2">
          <Calendar className="w-4 h-4 text-gray-500" />
          <span className="text-sm font-medium text-gray-700">Periodo:</span>
          <div className="flex gap-1">
            {PERIODS.map((p) => (
              <button
                key={p.value}
                onClick={() => setPeriod(p.value)}
                className={`px-3 py-1.5 text-sm rounded-lg transition-colors ${
                  period === p.value
                    ? 'bg-blue-600 text-white'
                    : 'text-gray-600 hover:bg-gray-100'
                }`}
              >
                {p.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Key Metrics */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
        <MetricCard title="Total Transacoes" value={stats.totalTransactions.toLocaleString()} icon={<CreditCard className="w-6 h-6 text-blue-600" />} />
        <MetricCard title="Receita Total" value={formatCurrency(stats.totalAmount)} icon={<DollarSign className="w-6 h-6 text-green-600" />} />
        <MetricCard title="Ticket Medio" value={formatCurrency(stats.averageAmount)} icon={<TrendingUp className="w-6 h-6 text-purple-600" />} />
        <MetricCard title="Taxa Aprovacao" value={stats.approvedRate.toFixed(1) + '%'} icon={<CheckCircle className="w-6 h-6 text-green-600" />} />
      </div>

      {/* Charts */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
        {/* Status Donut Chart */}
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-6">Distribuicao por Status</h2>
          {statusEntries.length > 0 ? (
            <div className="flex items-center gap-8">
              <DonutChart data={statusEntries.map(([k, v]) => ({ label: k, value: v as number }))} />
              <div className="space-y-3 flex-1">
                {statusEntries.map(([status, count]) => (
                  <div key={status} className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      {status === 'approved' ? <CheckCircle className="w-4 h-4 text-green-500" /> :
                       status === 'rejected' ? <XCircle className="w-4 h-4 text-red-500" /> :
                       <Clock className="w-4 h-4 text-yellow-500" />}
                      <span className="text-sm capitalize">{status}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-semibold">{(count as number).toLocaleString()}</span>
                      <span className="text-xs text-gray-400">
                        ({stats.totalTransactions > 0 ? (((count as number) / stats.totalTransactions) * 100).toFixed(0) : 0}%)
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ) : (
            <p className="text-sm text-gray-500 text-center py-8">Sem dados</p>
          )}
        </div>

        {/* Method Bar Chart */}
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-6">Por Metodo de Pagamento</h2>
          {methodEntries.length > 0 ? (
            <div className="space-y-4">
              {methodEntries.map(([method, count]) => (
                <div key={method}>
                  <div className="flex justify-between mb-1">
                    <span className="text-sm text-gray-700 capitalize">{method.replace('_', ' ').replace('creditcard', 'Cartao Credito').replace('pix', 'PIX').replace('debitcard', 'Cartao Debito').replace('boleto', 'Boleto')}</span>
                    <span className="text-sm font-semibold text-gray-900">{(count as number).toLocaleString()}</span>
                  </div>
                  <div className="w-full h-6 bg-gray-100 rounded-lg overflow-hidden">
                    <div
                      className="h-full bg-gradient-to-r from-blue-500 to-blue-600 rounded-lg transition-all duration-500"
                      style={{ width: `${((count as number) / maxMethod) * 100}%` }}
                    />
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-gray-500 text-center py-8">Sem dados</p>
          )}
        </div>
      </div>

      {/* Status Horizontal Bar */}
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
        <h2 className="text-lg font-semibold text-gray-900 mb-6">Volume por Status</h2>
        {statusEntries.length > 0 ? (
          <div className="space-y-4">
            {statusEntries.map(([status, count]) => {
              const color = status === 'approved' || status === 'completed' ? 'from-green-400 to-green-600'
                : status === 'rejected' || status === 'failed' ? 'from-red-400 to-red-600'
                : status === 'pending' ? 'from-yellow-400 to-yellow-600'
                : 'from-gray-400 to-gray-500';
              return (
                <div key={status}>
                  <div className="flex justify-between mb-1">
                    <span className="text-sm text-gray-700 capitalize font-medium">{status}</span>
                    <div className="text-sm">
                      <span className="font-semibold">{(count as number).toLocaleString()}</span>
                      <span className="text-gray-400 ml-1">
                        ({stats.totalTransactions > 0 ? (((count as number) / stats.totalTransactions) * 100).toFixed(1) : 0}%)
                      </span>
                    </div>
                  </div>
                  <div className="w-full h-4 bg-gray-100 rounded-full overflow-hidden">
                    <div
                      className={`h-full bg-gradient-to-r ${color} rounded-full transition-all duration-500`}
                      style={{ width: `${((count as number) / maxStatus) * 100}%` }}
                    />
                  </div>
                </div>
              );
            })}
          </div>
        ) : (
          <p className="text-sm text-gray-500 text-center py-8">Sem dados</p>
        )}
      </div>
    </div>
  );
}

function MetricCard({ title, value, icon }: { title: string; value: string; icon: React.ReactNode }) {
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

function DonutChart({ data }: { data: { label: string; value: number }[] }) {
  const total = data.reduce((sum, d) => sum + d.value, 0);
  if (total === 0) return null;

  const colors: Record<string, string> = {
    approved: '#22c55e', completed: '#22c55e',
    rejected: '#ef4444', failed: '#ef4444',
    pending: '#eab308', processing: '#f59e0b',
    captured: '#3b82f6', refunded: '#a855f7',
    authorized: '#60a5fa',
  };

  const radius = 60;
  const strokeWidth = 20;
  const circumference = 2 * Math.PI * radius;
  let offset = 0;

  return (
    <div className="relative flex-shrink-0" style={{ width: 160, height: 160 }}>
      <svg viewBox="0 0 160 160" className="w-full h-full -rotate-90">
        {data.map((d) => {
          const pct = d.value / total;
          const dashLength = circumference * pct;
          const gap = circumference - dashLength;
          const currentOffset = offset;
          offset += dashLength;
          return (
            <circle
              key={d.label}
              cx="80" cy="80" r={radius}
              fill="none"
              stroke={colors[d.label] || '#9ca3af'}
              strokeWidth={strokeWidth}
              strokeDasharray={`${dashLength} ${gap}`}
              strokeDashoffset={-currentOffset}
              className="transition-all duration-500"
            />
          );
        })}
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="text-2xl font-bold text-gray-900">{total.toLocaleString()}</span>
        <span className="text-xs text-gray-500">total</span>
      </div>
    </div>
  );
}
