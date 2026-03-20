import { WebhookService } from './webhook.service';
export declare class HealthController {
    private readonly webhookService;
    constructor(webhookService: WebhookService);
    health(): {
        status: string;
        service: string;
        timestamp: string;
        eventsReceived: number;
    };
}
