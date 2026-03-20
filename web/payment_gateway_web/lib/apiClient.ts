import axios, { AxiosError, AxiosInstance, AxiosResponse, InternalAxiosRequestConfig } from 'axios';
import { readApiAccess } from './apiAccess';

const API_BASE_URL = '';

let cachedGetSession: (() => Promise<{ accessToken?: string } | null>) | null = null;
async function safeGetSession() {
  try {
    if (!cachedGetSession) {
      const mod = await import('next-auth/react');
      cachedGetSession = mod.getSession as () => Promise<{ accessToken?: string } | null>;
    }
    return await cachedGetSession();
  } catch {
    return null;
  }
}

export interface CreatePaymentPayload {
  merchantId: string;
  amount: number;
  currency: string;
  method: string;
  customer: {
    email: string;
    name: string;
    document?: string;
    ip?: string;
    phone?: string;
  };
  description?: string;
  items?: Array<{
    sku: string;
    name: string;
    quantity: number;
    unitPrice: number;
  }>;
  metadata?: Record<string, string>;
  idempotencyKey?: string;
}

class ApiClient {
  private client: AxiosInstance;

  constructor() {
    this.client = axios.create({
      baseURL: API_BASE_URL,
      timeout: 30000,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    this.client.interceptors.request.use(
      async (config: InternalAxiosRequestConfig) => {
        const session = await safeGetSession();
        if (session?.accessToken && config.headers) {
          config.headers.Authorization = `Bearer ${session.accessToken}`;
        }

        try {
          const access = readApiAccess();
          if (access?.apiKey && config.headers) {
            config.headers['X-API-Key'] = access.apiKey;
          }
        } catch {
          // API access not configured
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    this.client.interceptors.response.use(
      (response: AxiosResponse) => response,
      (error: AxiosError) => {
        if (error.response) {
          const { status, data } = error.response;
          if (status === 401) {
            console.error('Unauthorized API access.', data);
          } else if (status === 403) {
            console.error('Access forbidden', data);
          } else if (status === 429) {
            console.error('Rate limit exceeded', data);
          } else if (status >= 500) {
            console.error('Server error', data);
          }
        } else if (error.request) {
          console.error('Network error - please check your connection');
        }

        return Promise.reject(error);
      }
    );
  }

  async getPayments(params?: {
    merchantId?: string;
    status?: string;
    from?: string;
    to?: string;
    limit?: number;
    offset?: number;
  }) {
    const response = await this.client.get('/api/v1/payments', { params });
    return response.data;
  }

  async getPayment(id: string) {
    const response = await this.client.get(`/api/v1/payments/${id}`);
    return response.data;
  }

  async createPayment(data: CreatePaymentPayload) {
    const headers = data.idempotencyKey
      ? { 'Idempotency-Key': data.idempotencyKey }
      : undefined;
    const response = await this.client.post('/api/v1/payments', data, { headers });
    return response.data;
  }

  async refundPayment(id: string, data: { amount?: number; reason?: string }) {
    const response = await this.client.post(`/api/v1/payments/${id}/refund`, data);
    return response.data;
  }

  async capturePayment(id: string, data: { amount?: number }) {
    const response = await this.client.post(`/api/v1/payments/${id}/capture`, data);
    return response.data;
  }

  async cancelPayment(id: string, data?: { reason?: string }) {
    const response = await this.client.post(`/api/v1/payments/${id}/cancel`, data ?? {});
    return response.data;
  }

  async reprocessPayment(id: string) {
    const response = await this.client.post(`/api/v1/payments/${id}/reprocess`, {});
    return response.data;
  }

  async getTransactions(params?: {
    merchantId?: string;
    paymentId?: string;
    type?: string;
    limit?: number;
    offset?: number;
  }) {
    const response = await this.client.get('/api/v1/transactions', { params });
    return response.data;
  }

  async getMerchant(id: string) {
    const response = await this.client.get(`/api/v1/merchants/${id}`);
    return response.data;
  }

  async createMerchant(data: {
    name: string;
    email: string;
    document: string;
    category: string;
    callbackUrl?: string;
  }) {
    const response = await this.client.post('/api/v1/merchants', data);
    return response.data;
  }

  async updateMerchant(
    id: string,
    data: Partial<{
      name: string;
      email: string;
      category: string;
      callbackUrl: string;
    }>
  ) {
    const response = await this.client.put(`/api/v1/merchants/${id}`, data);
    return response.data;
  }

  async getMerchantPayments(
    id: string,
    params?: { status?: string; limit?: number; offset?: number }
  ) {
    const response = await this.client.get(`/api/v1/merchants/${id}/payments`, { params });
    return response.data;
  }

  async getStatistics(params?: {
    merchantId?: string;
    from?: string;
    to?: string;
    groupBy?: string;
  }) {
    const response = await this.client.get('/api/v1/statistics', { params });
    return response.data;
  }

  async getWebhooks(merchantId: string) {
    const response = await this.client.get('/api/v1/webhooks', { params: { merchantId } });
    return response.data;
  }

  async createWebhook(data: {
    merchantId: string;
    url: string;
    events: string[];
    secret?: string;
    active?: boolean;
  }) {
    const response = await this.client.put('/api/v1/webhooks', data);
    return response.data;
  }

  async deleteWebhook(id: string) {
    const response = await this.client.delete(`/api/v1/webhooks/${id}`);
    return response.data;
  }

  async healthCheck() {
    const response = await this.client.get('/health');
    return response.data;
  }
}

export const apiClient = new ApiClient();
export default apiClient;
