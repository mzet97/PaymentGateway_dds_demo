import { WEBHOOK_RECEIVER_URL } from './fixtures';
import { waitForCondition } from './wait-utils';

async function fetchJson(path: string) {
  const res = await fetch(`${WEBHOOK_RECEIVER_URL}${path}`);
  if (!res.ok) throw new Error(`Webhook receiver ${path}: ${res.status}`);
  return res.json();
}

export async function healthCheck() {
  return fetchJson('/health');
}

export async function getAllEvents(): Promise<{ count: number; events: WebhookEvent[] }> {
  return fetchJson('/webhooks/events');
}

export async function getEventsByType(type: string): Promise<{ count: number; type: string; events: WebhookEvent[] }> {
  return fetchJson(`/webhooks/events/${type}`);
}

export async function waitForEvent(
  paymentId: string,
  eventType: string,
  timeoutMs = 30_000,
): Promise<WebhookEvent> {
  return waitForCondition(
    async () => {
      const data = await getEventsByType(eventType);
      const match = data.events.find(
        (e) => e.payload && (e.payload as Record<string, unknown>).paymentId === paymentId,
      );
      return match || null;
    },
    { timeoutMs, intervalMs: 2_000, label: `webhook ${eventType} for payment ${paymentId}` },
  );
}

export interface WebhookEvent {
  id: string;
  type: string;
  receivedAt: string;
  signatureValid: boolean;
  payload: Record<string, unknown>;
}
