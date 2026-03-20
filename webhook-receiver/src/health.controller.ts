import { Controller, Get } from '@nestjs/common';
import { WebhookService } from './webhook.service';

@Controller()
export class HealthController {
  constructor(private readonly webhookService: WebhookService) {}

  @Get('health')
  health() {
    const events = this.webhookService.getEvents();
    return {
      status: 'healthy',
      service: 'webhook-receiver',
      timestamp: new Date().toISOString(),
      eventsReceived: events.length,
    };
  }
}
