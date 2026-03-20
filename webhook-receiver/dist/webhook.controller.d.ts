import { RawBodyRequest } from '@nestjs/common';
import { Request } from 'express';
import { WebhookService, WebhookEvent } from './webhook.service';
export declare class WebhookController {
    private readonly webhookService;
    constructor(webhookService: WebhookService);
    paymentCreated(body: Record<string, unknown>, signature: string, req: RawBodyRequest<Request>): WebhookEvent;
    paymentApproved(body: Record<string, unknown>, signature: string, req: RawBodyRequest<Request>): WebhookEvent;
    paymentRejected(body: Record<string, unknown>, signature: string, req: RawBodyRequest<Request>): WebhookEvent;
    paymentRefunded(body: Record<string, unknown>, signature: string, req: RawBodyRequest<Request>): WebhookEvent;
    paymentCaptured(body: Record<string, unknown>, signature: string, req: RawBodyRequest<Request>): WebhookEvent;
    getAllEvents(): {
        count: number;
        events: WebhookEvent[];
    };
    getEventsByType(type: string): {
        count: number;
        type: string;
        events: WebhookEvent[];
    };
}
