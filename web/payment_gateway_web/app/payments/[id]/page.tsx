'use client';

import { use, ReactNode, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { usePayment, useReprocessPayment, queryKeys } from '@/lib/queries';
import {
  ArrowLeft,
  CreditCard,
  User,
  Clock,
  CheckCircle,
  XCircle,
  AlertCircle,
  Loader2,
  RefreshCw,
  Shield,
  RotateCcw,
} from 'lucide-react';

export default function PaymentDetailsPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const reprocessPayment = useReprocessPayment();
  const [reprocessing, setReprocessing] = useState(false);
  const [reprocessMsg, setReprocessMsg] = useState<string | null>(null);

  const { data: payment, isLoading, error, refetch } = usePayment(id);

  const handleReprocess = async () => {
    setReprocessing(true);
    setReprocessMsg(null);
    try {
      await reprocessPayment.mutateAsync(id);
      setReprocessMsg('Payment queued for reprocessing via DDS');
      // Poll for update
      setTimeout(() => {
        refetch();
        queryClient.invalidateQueries({ queryKey: queryKeys.payments });
      }, 5000);
      setTimeout(() => {
        refetch();
      }, 15000);
    } catch {
      setReprocessMsg('Failed to reprocess payment');
    } finally {
      setReprocessing(false);
    }
  };

  const canReprocess = payment && ['pending', 'failed', 'expired'].includes(payment.status?.toLowerCase());

  const formatCurrency = (amount: number, currency: string = 'BRL') => {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency }).format(amount);
  };

  const formatDate = (dateString: string) => {
    if (!dateString) return '-';
    return new Date(dateString).toLocaleString('pt-BR', {
      day: '2-digit', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit',
    });
  };

  const getStatusBadge = (status: string) => {
    const styles: Record<string, { bg: string; text: string; icon: ReactNode }> = {
      approved: { bg: 'bg-green-100', text: 'text-green-800', icon: <CheckCircle className="w-4 h-4" /> },
      completed: { bg: 'bg-green-100', text: 'text-green-800', icon: <CheckCircle className="w-4 h-4" /> },
      authorized: { bg: 'bg-blue-100', text: 'text-blue-800', icon: <CheckCircle className="w-4 h-4" /> },
      rejected: { bg: 'bg-red-100', text: 'text-red-800', icon: <XCircle className="w-4 h-4" /> },
      failed: { bg: 'bg-red-100', text: 'text-red-800', icon: <XCircle className="w-4 h-4" /> },
      pending: { bg: 'bg-yellow-100', text: 'text-yellow-800', icon: <Clock className="w-4 h-4" /> },
      processing: { bg: 'bg-yellow-100', text: 'text-yellow-800', icon: <Loader2 className="w-4 h-4 animate-spin" /> },
      captured: { bg: 'bg-blue-100', text: 'text-blue-800', icon: <CheckCircle className="w-4 h-4" /> },
      refunded: { bg: 'bg-purple-100', text: 'text-purple-800', icon: <RefreshCw className="w-4 h-4" /> },
    };
    const style = styles[status?.toLowerCase()] || { bg: 'bg-gray-100', text: 'text-gray-800', icon: <AlertCircle className="w-4 h-4" /> };
    return (
      <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-sm font-medium ${style.bg} ${style.text}`}>
        {style.icon}
        {status}
      </span>
    );
  };

  const getFraudScoreColor = (score: number | undefined | null) => {
    if (score == null) return 'text-gray-400';
    if (score <= 25) return 'text-green-600';
    if (score <= 60) return 'text-yellow-600';
    return 'text-red-600';
  };

  const getFraudScoreBar = (score: number | undefined | null) => {
    if (score == null) return null;
    const width = Math.min(score, 100);
    const color = score <= 25 ? 'bg-green-500' : score <= 60 ? 'bg-yellow-500' : 'bg-red-500';
    return (
      <div className="w-full h-2 bg-gray-200 rounded-full mt-1">
        <div className={`h-full rounded-full ${color}`} style={{ width: `${width}%` }} />
      </div>
    );
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  if (error || !payment) {
    return (
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <button onClick={() => router.back()} className="flex items-center gap-2 text-gray-600 hover:text-gray-900 mb-6">
          <ArrowLeft className="w-4 h-4" /> Voltar
        </button>
        <div className="bg-red-50 border border-red-200 rounded-lg p-6 text-center">
          <AlertCircle className="w-12 h-12 text-red-500 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-red-900 mb-2">Pagamento nao encontrado</h3>
          <button onClick={() => router.push('/payments')} className="inline-flex items-center px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700">
            Ver Pagamentos
          </button>
        </div>
      </div>
    );
  }

  const fraudScore = payment.fraudScore ?? null;
  const fraudDecision = payment.fraudDecision ?? null;
  const transactionId = payment.transactionId ?? null;

  return (
    <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <button onClick={() => router.back()} className="flex items-center gap-2 text-gray-600 hover:text-gray-900 mb-6">
        <ArrowLeft className="w-4 h-4" /> Voltar
      </button>

      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Detalhes do Pagamento</h1>
          <p className="text-gray-500 mt-1 font-mono text-sm">{payment.id || payment.paymentId}</p>
        </div>
        <div className="flex items-center gap-3">
          {canReprocess && (
            <button
              onClick={handleReprocess}
              disabled={reprocessing}
              className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-orange-600 rounded-lg hover:bg-orange-700 disabled:opacity-50 transition-colors"
            >
              {reprocessing ? <Loader2 className="w-4 h-4 animate-spin" /> : <RotateCcw className="w-4 h-4" />}
              {reprocessing ? 'Reprocessando...' : 'Reprocessar'}
            </button>
          )}
          <button onClick={() => refetch()} className="p-2 text-gray-500 hover:text-gray-700 transition-colors">
            <RefreshCw className="w-4 h-4" />
          </button>
          {getStatusBadge(payment.status)}
        </div>
      </div>

      {reprocessMsg && (
        <div className={`mb-6 px-4 py-3 rounded-lg text-sm ${reprocessMsg.includes('Failed') ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-700'}`}>
          {reprocessMsg}
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Payment Info */}
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
            <CreditCard className="w-5 h-5" /> Pagamento
          </h2>
          <dl className="space-y-3">
            <div className="flex justify-between">
              <dt className="text-gray-500">Valor</dt>
              <dd className="font-semibold text-gray-900">{formatCurrency(payment.amount, payment.currency)}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-gray-500">Moeda</dt>
              <dd className="text-gray-900">{payment.currency}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-gray-500">Metodo</dt>
              <dd className="text-gray-900 capitalize">{payment.method?.replace('_', ' ')}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-gray-500">Status</dt>
              <dd>{getStatusBadge(payment.status)}</dd>
            </div>
            {transactionId && (
              <div className="flex justify-between">
                <dt className="text-gray-500">Transaction ID</dt>
                <dd className="text-gray-900 font-mono text-xs">{transactionId}</dd>
              </div>
            )}
            {payment.description && (
              <div className="flex flex-col">
                <dt className="text-gray-500 mb-1">Descricao</dt>
                <dd className="text-gray-900">{payment.description}</dd>
              </div>
            )}
          </dl>
        </div>

        {/* Fraud Analysis */}
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
            <Shield className="w-5 h-5" /> Analise de Fraude (AI)
          </h2>
          {fraudScore != null ? (
            <dl className="space-y-3">
              <div>
                <dt className="text-gray-500 text-sm mb-1">Risk Score</dt>
                <dd className={`text-3xl font-bold ${getFraudScoreColor(fraudScore)}`}>{fraudScore}</dd>
                {getFraudScoreBar(fraudScore)}
                <p className="text-xs text-gray-400 mt-1">0 = seguro, 100 = fraude</p>
              </div>
              <div className="flex justify-between">
                <dt className="text-gray-500">Decisao</dt>
                <dd className="capitalize font-medium">{fraudDecision}</dd>
              </div>
              <div className="pt-2 border-t border-gray-100">
                <p className="text-xs text-gray-400">
                  Escala: 0-25 Aprovado | 26-60 Revisao | 61-100 Rejeitado
                </p>
                <p className="text-xs text-gray-400">
                  Modelo: minimax/minimax-m2.5 via OpenRouter
                </p>
              </div>
            </dl>
          ) : (
            <div className="text-center py-6 text-gray-400">
              <Shield className="w-10 h-10 mx-auto mb-2 opacity-50" />
              <p>Analise de fraude pendente</p>
              {canReprocess && (
                <button onClick={handleReprocess} disabled={reprocessing}
                  className="mt-3 text-sm text-orange-600 hover:text-orange-700 font-medium">
                  Reprocessar pagamento
                </button>
              )}
            </div>
          )}
        </div>

        {/* Customer Info */}
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
            <User className="w-5 h-5" /> Cliente
          </h2>
          <dl className="space-y-3">
            <div className="flex flex-col">
              <dt className="text-gray-500 mb-1">Nome</dt>
              <dd className="text-gray-900">{payment.customer?.name || '-'}</dd>
            </div>
            <div className="flex flex-col">
              <dt className="text-gray-500 mb-1">Email</dt>
              <dd className="text-gray-900">{payment.customer?.email || '-'}</dd>
            </div>
            {payment.customer?.document && (
              <div className="flex flex-col">
                <dt className="text-gray-500 mb-1">Documento</dt>
                <dd className="text-gray-900">{payment.customer.document}</dd>
              </div>
            )}
            {payment.customer?.phone && (
              <div className="flex flex-col">
                <dt className="text-gray-500 mb-1">Telefone</dt>
                <dd className="text-gray-900">{payment.customer.phone}</dd>
              </div>
            )}
            {payment.customer?.ip && (
              <div className="flex flex-col">
                <dt className="text-gray-500 mb-1">IP</dt>
                <dd className="text-gray-900 font-mono text-sm">{payment.customer.ip}</dd>
              </div>
            )}
          </dl>
        </div>

        {/* Timeline */}
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4 flex items-center gap-2">
            <Clock className="w-5 h-5" /> Timeline
          </h2>
          <div className="space-y-4">
            <div className="flex items-start gap-4">
              <div className="w-2.5 h-2.5 bg-blue-600 rounded-full mt-1.5 flex-shrink-0"></div>
              <div>
                <p className="font-medium text-gray-900">Pagamento criado</p>
                <p className="text-sm text-gray-500">{formatDate(payment.createdAt)}</p>
              </div>
            </div>
            {fraudScore != null && (
              <div className="flex items-start gap-4">
                <div className={`w-2.5 h-2.5 rounded-full mt-1.5 flex-shrink-0 ${
                  fraudScore <= 25 ? 'bg-green-600' : fraudScore <= 60 ? 'bg-yellow-500' : 'bg-red-600'
                }`}></div>
                <div>
                  <p className="font-medium text-gray-900">Fraude analisada (score: {fraudScore})</p>
                  <p className="text-sm text-gray-500">Decisao: {fraudDecision}</p>
                </div>
              </div>
            )}
            {payment.processedAt && (
              <div className="flex items-start gap-4">
                <div className="w-2.5 h-2.5 bg-green-600 rounded-full mt-1.5 flex-shrink-0"></div>
                <div>
                  <p className="font-medium text-gray-900">Pagamento processado</p>
                  <p className="text-sm text-gray-500">{formatDate(payment.processedAt)}</p>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
