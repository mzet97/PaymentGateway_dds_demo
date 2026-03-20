import { useMutation, useQuery, UseQueryResult } from '@tanstack/react-query';
import apiClient from './apiClient';

// Types for API responses
export interface Payment {
  id: string;
  paymentId?: string;
  merchantId: string;
  amount: number;
  currency: string;
  status: string;
  method: string;
  description?: string;
  customer: {
    name?: string;
    email?: string;
    document?: string;
    phone?: string;
    ip?: string;
  };
  fraudScore?: number | null;
  fraudDecision?: string | null;
  transactionId?: string | null;
  createdAt: string;
  processedAt?: string;
}

export interface Merchant {
  id: string;
  name: string;
  email: string;
  document: string;
  category: string;
  callbackUrl?: string;
  status?: string;
  createdAt?: string;
}

export interface Webhook {
  id: string;
  merchantId: string;
  url: string;
  events: string[];
  active: boolean;
  createdAt?: string;
}

export interface Statistics {
  totalTransactions: number;
  totalAmount: number;
  averageAmount: number;
  byStatus: Record<string, number>;
  byMethod: Record<string, number>;
  approvedCount?: number;
  rejectedCount?: number;
  refundedCount?: number;
  groupedData?: Array<{ period: string; amount: number; count: number }>;
  timeSeries: Array<{ date: string; amount: number; count: number }>;
}

export interface PaymentsResponse {
  payments: Payment[];
  total: number;
  limit: number;
  offset: number;
  hasMore: boolean;
}

export interface WebhooksResponse {
  webhooks: Webhook[];
}

export interface Transaction {
  id: string;
  paymentId: string;
  type: string;
  amount: number;
  currency: string;
  reference?: string;
  reason?: string;
  success: boolean;
  errorCode?: string;
  createdAt: string;
}

export interface TransactionsResponse {
  items: Transaction[];
  total: number;
  limit: number;
  offset: number;
  hasMore: boolean;
}

// Query Keys
export const queryKeys = {
  payments: ['payments'] as const,
  payment: (id: string) => ['payments', id] as const,
  merchantPayments: (merchantId: string) => ['merchants', merchantId, 'payments'] as const,
  merchant: (id: string) => ['merchants', id] as const,
  transactions: ['transactions'] as const,
  statistics: (params?: Record<string, string>) => ['statistics', params] as const,
  webhooks: (merchantId: string) => ['webhooks', merchantId] as const,
  health: ['health'] as const,
};

function normalizePayment(raw: Partial<Payment> & { id?: string; paymentId?: string }): Payment {
  return {
    id: raw.id ?? raw.paymentId ?? '',
    paymentId: raw.paymentId,
    merchantId: raw.merchantId ?? '',
    amount: raw.amount ?? 0,
    currency: raw.currency ?? 'BRL',
    status: raw.status ?? 'unknown',
    method: raw.method ?? '',
    description: raw.description,
    customer: raw.customer ?? {},
    fraudScore: raw.fraudScore,
    fraudDecision: raw.fraudDecision,
    transactionId: raw.transactionId,
    createdAt: raw.createdAt ?? '',
    processedAt: raw.processedAt,
  };
}

function normalizePaymentsResponse(raw: unknown): PaymentsResponse {
  const payload = raw as {
    items?: Array<Partial<Payment> & { id?: string; paymentId?: string }>;
    payments?: Array<Partial<Payment> & { id?: string; paymentId?: string }>;
    total?: number;
    limit?: number;
    offset?: number;
    hasMore?: boolean;
  };

  const items = payload.items ?? payload.payments ?? [];

  return {
    payments: items.map(normalizePayment),
    total: payload.total ?? items.length,
    limit: payload.limit ?? items.length,
    offset: payload.offset ?? 0,
    hasMore: payload.hasMore ?? false,
  };
}

function normalizeMerchant(raw: unknown): Merchant {
  const payload = raw as {
    merchantId?: string;
    id?: string;
    name?: string;
    email?: string;
    document?: string;
    category?: string;
    callbackUrl?: string;
    status?: string;
    createdAt?: string;
  };

  return {
    id: payload.id ?? payload.merchantId ?? '',
    name: payload.name ?? '',
    email: payload.email ?? '',
    document: payload.document ?? '',
    category: payload.category ?? '',
    callbackUrl: payload.callbackUrl,
    status: payload.status,
    createdAt: payload.createdAt,
  };
}

function normalizeWebhooksResponse(raw: unknown): WebhooksResponse {
  const list = (Array.isArray(raw) ? raw : []) as Array<{
    webhookId?: string;
    id?: string;
    merchantId?: string;
    url?: string;
    events?: string[];
    active?: boolean;
    createdAt?: string;
  }>;

  return {
    webhooks: list.map((item) => ({
      id: item.id ?? item.webhookId ?? '',
      merchantId: item.merchantId ?? '',
      url: item.url ?? '',
      events: item.events ?? [],
      active: item.active ?? false,
      createdAt: item.createdAt,
    })),
  };
}

function normalizeStatistics(raw: unknown): Statistics {
  const payload = raw as {
    totalTransactions?: number;
    totalAmount?: number;
    averageAmount?: number;
    approvedCount?: number;
    rejectedCount?: number;
    refundedCount?: number;
    byStatus?: Record<string, number>;
    byMethod?: Record<string, number>;
    groupedData?: Array<{ period: string; amount: number; count: number }>;
    timeSeries?: Array<{ date: string; amount: number; count: number }>;
  };

  const groupedData = payload.groupedData ?? [];

  return {
    totalTransactions: payload.totalTransactions ?? 0,
    totalAmount: payload.totalAmount ?? 0,
    averageAmount: payload.averageAmount ?? 0,
    approvedCount: payload.approvedCount ?? 0,
    rejectedCount: payload.rejectedCount ?? 0,
    refundedCount: payload.refundedCount ?? 0,
    byStatus: payload.byStatus ?? {},
    byMethod: payload.byMethod ?? {},
    groupedData,
    timeSeries:
      payload.timeSeries ??
      groupedData.map((point) => ({
        date: point.period,
        amount: point.amount,
        count: point.count,
      })),
  };
}

// Hooks for Payments
export function usePayments(params?: {
  merchantId?: string;
  status?: string;
  from?: string;
  to?: string;
  limit?: number;
  offset?: number;
}): UseQueryResult<PaymentsResponse, Error> {
  return useQuery({
    queryKey: [...queryKeys.payments, params],
    queryFn: async () => normalizePaymentsResponse(await apiClient.getPayments(params)),
  }) as UseQueryResult<PaymentsResponse, Error>;
}

export function usePayment(id: string): UseQueryResult<Payment, Error> {
  return useQuery({
    queryKey: queryKeys.payment(id),
    queryFn: async () => normalizePayment(await apiClient.getPayment(id)),
    enabled: !!id,
  }) as UseQueryResult<Payment, Error>;
}

export function useCreatePayment() {
  return useMutation({
    mutationFn: (data: Parameters<typeof apiClient.createPayment>[0]) =>
      apiClient.createPayment(data),
  });
}

export function useRefundPayment() {
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: Parameters<typeof apiClient.refundPayment>[1] }) =>
      apiClient.refundPayment(id, data),
  });
}

export function useCapturePayment() {
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: Parameters<typeof apiClient.capturePayment>[1] }) =>
      apiClient.capturePayment(id, data),
  });
}

export function useCancelPayment() {
  return useMutation({
    mutationFn: (id: string) => apiClient.cancelPayment(id),
  });
}

export function useReprocessPayment() {
  return useMutation({
    mutationFn: (id: string) => apiClient.reprocessPayment(id),
  });
}

export function useMerchant(id: string): UseQueryResult<Merchant, Error> {
  return useQuery({
    queryKey: queryKeys.merchant(id),
    queryFn: async () => normalizeMerchant(await apiClient.getMerchant(id)),
    enabled: !!id,
  }) as UseQueryResult<Merchant, Error>;
}

export function useMerchantPayments(merchantId: string, params?: { status?: string; limit?: number; offset?: number }): UseQueryResult<PaymentsResponse, Error> {
  return useQuery({
    queryKey: queryKeys.merchantPayments(merchantId),
    queryFn: async () => normalizePaymentsResponse(await apiClient.getMerchantPayments(merchantId, params)),
    enabled: !!merchantId,
  }) as UseQueryResult<PaymentsResponse, Error>;
}

export function useCreateMerchant() {
  return useMutation({
    mutationFn: (data: Parameters<typeof apiClient.createMerchant>[0]) =>
      apiClient.createMerchant(data),
  });
}

export function useUpdateMerchant() {
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: Parameters<typeof apiClient.updateMerchant>[1] }) =>
      apiClient.updateMerchant(id, data),
  });
}

// Hooks for Statistics
export function useStatistics(params?: {
  merchantId?: string;
  from?: string;
  to?: string;
  groupBy?: string;
}): UseQueryResult<Statistics, Error> {
  return useQuery({
    queryKey: queryKeys.statistics(params as Record<string, string>),
    queryFn: async () => normalizeStatistics(await apiClient.getStatistics(params)),
  }) as UseQueryResult<Statistics, Error>;
}

// Hooks for Transactions
export function useTransactions(params?: {
  merchantId?: string;
  paymentId?: string;
  type?: string;
  limit?: number;
  offset?: number;
}): UseQueryResult<TransactionsResponse, Error> {
  return useQuery({
    queryKey: [...queryKeys.transactions, params],
    queryFn: async () => {
      const data = await apiClient.getTransactions(params);
      return data as TransactionsResponse;
    },
    enabled: !!(params?.merchantId || params?.paymentId),
  }) as UseQueryResult<TransactionsResponse, Error>;
}

// Hooks for Webhooks
export function useWebhooks(merchantId: string): UseQueryResult<WebhooksResponse, Error> {
  return useQuery({
    queryKey: queryKeys.webhooks(merchantId),
    queryFn: async () => normalizeWebhooksResponse(await apiClient.getWebhooks(merchantId)),
    enabled: !!merchantId,
  }) as UseQueryResult<WebhooksResponse, Error>;
}

export function useCreateWebhook() {
  return useMutation({
    mutationFn: (data: Parameters<typeof apiClient.createWebhook>[0]) =>
      apiClient.createWebhook(data),
  });
}

export function useDeleteWebhook() {
  return useMutation({
    mutationFn: (id: string) => apiClient.deleteWebhook(id),
  });
}

// Hook for Health Check
export function useHealthCheck(): UseQueryResult<{ status: string }, Error> {
  return useQuery({
    queryKey: queryKeys.health,
    queryFn: () => apiClient.healthCheck(),
    refetchInterval: 30000,
  }) as UseQueryResult<{ status: string }, Error>;
}
