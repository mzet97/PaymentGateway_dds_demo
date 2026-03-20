import { APIRequestContext } from '@playwright/test';

export class ApiClient {
  constructor(private request: APIRequestContext) {}

  async healthCheck() {
    return this.request.get('/health');
  }

  // Payments
  async createPayment(data: Record<string, unknown>) {
    return this.request.post('/api/v1/payments', { data });
  }

  async getPayment(id: string) {
    return this.request.get(`/api/v1/payments/${id}`);
  }

  async listPayments(params: Record<string, string> = {}) {
    return this.request.get('/api/v1/payments', { params });
  }

  async refundPayment(id: string, data: Record<string, unknown> = {}) {
    return this.request.post(`/api/v1/payments/${id}/refund`, { data });
  }

  async capturePayment(id: string, data: Record<string, unknown> = {}) {
    return this.request.post(`/api/v1/payments/${id}/capture`, { data });
  }

  async cancelPayment(id: string, data: Record<string, unknown> = {}) {
    return this.request.post(`/api/v1/payments/${id}/cancel`, { data });
  }

  async reprocessPayment(id: string) {
    return this.request.post(`/api/v1/payments/${id}/reprocess`, { data: {} });
  }

  // Merchants
  async getMerchant(id: string) {
    return this.request.get(`/api/v1/merchants/${id}`);
  }

  async updateMerchant(id: string, data: Record<string, unknown>) {
    return this.request.put(`/api/v1/merchants/${id}`, { data });
  }

  async getMerchantPayments(id: string, params: Record<string, string> = {}) {
    return this.request.get(`/api/v1/merchants/${id}/payments`, { params });
  }

  // Webhooks
  async createWebhook(data: Record<string, unknown>) {
    return this.request.put('/api/v1/webhooks', { data });
  }

  async listWebhooks(merchantId: string) {
    return this.request.get('/api/v1/webhooks', { params: { merchantId } });
  }

  async deleteWebhook(id: string) {
    return this.request.delete(`/api/v1/webhooks/${id}`);
  }

  // Transactions & Statistics
  async getTransactions(params: Record<string, string> = {}) {
    return this.request.get('/api/v1/transactions', { params });
  }

  async getStatistics(params: Record<string, string> = {}) {
    return this.request.get('/api/v1/statistics', { params });
  }

  // Raw request without auth (for error tests)
  async rawGet(path: string, headers: Record<string, string> = {}) {
    return this.request.get(path, { headers });
  }

  async rawPost(path: string, data: Record<string, unknown>, headers: Record<string, string> = {}) {
    return this.request.post(path, { data, headers });
  }
}
