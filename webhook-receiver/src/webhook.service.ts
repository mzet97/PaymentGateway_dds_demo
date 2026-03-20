import { Injectable, Logger } from '@nestjs/common';
import * as crypto from 'crypto';
import * as fs from 'fs';
import * as path from 'path';

export interface WebhookEvent {
  id: string;
  type: string;
  receivedAt: string;
  signatureValid: boolean;
  payload: Record<string, unknown>;
}

@Injectable()
export class WebhookService {
  private readonly logger = new Logger(WebhookService.name);
  private readonly secret = process.env.WEBHOOK_SECRET || 'webhook-secret-demo-2026';
  private readonly logsDir = path.join(process.cwd(), 'logs');

  constructor() {
    if (!fs.existsSync(this.logsDir)) {
      fs.mkdirSync(this.logsDir, { recursive: true });
    }
    this.logger.log(`Webhook secret: ${this.secret.substring(0, 8)}...`);
    this.logger.log(`Logs directory: ${this.logsDir}`);
  }

  /**
   * Validates HMAC-SHA256 signature from the X-Webhook-Signature header.
   */
  validateSignature(payload: string, signature: string | undefined): boolean {
    if (!signature) return false;
    const expected = crypto
      .createHmac('sha256', this.secret)
      .update(payload)
      .digest('hex');
    const sig = signature.replace('sha256=', '');
    return crypto.timingSafeEqual(
      Buffer.from(sig, 'hex'),
      Buffer.from(expected, 'hex'),
    );
  }

  /**
   * Process and store a webhook event.
   */
  processEvent(
    type: string,
    body: Record<string, unknown>,
    rawBody: string,
    signatureHeader: string | undefined,
  ): WebhookEvent {
    const signatureValid = this.validateSignature(rawBody, signatureHeader);
    const event: WebhookEvent = {
      id: crypto.randomUUID(),
      type,
      receivedAt: new Date().toISOString(),
      signatureValid,
      payload: body,
    };

    // Log to console
    this.logger.log(
      `[${type}] payment=${body.paymentId || body.id || '?'} ` +
      `signature=${signatureValid ? 'VALID' : 'INVALID/MISSING'} ` +
      `amount=${body.amount || '?'}`,
    );

    // Save to file
    this.saveToFile(event);

    return event;
  }

  /**
   * Save event to a JSON lines file, one per event type + a combined file.
   */
  private saveToFile(event: WebhookEvent): void {
    const line = JSON.stringify(event) + '\n';

    // Per-type file: logs/payment.created.jsonl
    const typeFile = path.join(this.logsDir, `${event.type}.jsonl`);
    fs.appendFileSync(typeFile, line);

    // Combined file: logs/all-events.jsonl
    const allFile = path.join(this.logsDir, 'all-events.jsonl');
    fs.appendFileSync(allFile, line);
  }

  /**
   * Read all stored events, optionally filtered by type.
   */
  getEvents(type?: string): WebhookEvent[] {
    const fileName = type ? `${type}.jsonl` : 'all-events.jsonl';
    const filePath = path.join(this.logsDir, fileName);

    if (!fs.existsSync(filePath)) return [];

    return fs
      .readFileSync(filePath, 'utf-8')
      .split('\n')
      .filter((line) => line.trim())
      .map((line) => {
        try {
          return JSON.parse(line) as WebhookEvent;
        } catch {
          return null;
        }
      })
      .filter((e): e is WebhookEvent => e !== null)
      .reverse(); // newest first
  }

  getSecret(): string {
    return this.secret;
  }
}
