import {
  Controller,
  Post,
  Get,
  Body,
  Headers,
  Param,
  Req,
  HttpCode,
  RawBodyRequest,
} from '@nestjs/common';
import { Request } from 'express';
import { WebhookService, WebhookEvent } from './webhook.service';

@Controller('webhooks')
export class WebhookController {
  constructor(private readonly webhookService: WebhookService) {}

  // ─── Individual event endpoints ───

  @Post('payment.created')
  @HttpCode(200)
  paymentCreated(
    @Body() body: Record<string, unknown>,
    @Headers('x-webhook-signature') signature: string,
    @Req() req: RawBodyRequest<Request>,
  ): WebhookEvent {
    const raw = req.rawBody?.toString() || JSON.stringify(body);
    return this.webhookService.processEvent('payment.created', body, raw, signature);
  }

  @Post('payment.approved')
  @HttpCode(200)
  paymentApproved(
    @Body() body: Record<string, unknown>,
    @Headers('x-webhook-signature') signature: string,
    @Req() req: RawBodyRequest<Request>,
  ): WebhookEvent {
    const raw = req.rawBody?.toString() || JSON.stringify(body);
    return this.webhookService.processEvent('payment.approved', body, raw, signature);
  }

  @Post('payment.rejected')
  @HttpCode(200)
  paymentRejected(
    @Body() body: Record<string, unknown>,
    @Headers('x-webhook-signature') signature: string,
    @Req() req: RawBodyRequest<Request>,
  ): WebhookEvent {
    const raw = req.rawBody?.toString() || JSON.stringify(body);
    return this.webhookService.processEvent('payment.rejected', body, raw, signature);
  }

  @Post('payment.refunded')
  @HttpCode(200)
  paymentRefunded(
    @Body() body: Record<string, unknown>,
    @Headers('x-webhook-signature') signature: string,
    @Req() req: RawBodyRequest<Request>,
  ): WebhookEvent {
    const raw = req.rawBody?.toString() || JSON.stringify(body);
    return this.webhookService.processEvent('payment.refunded', body, raw, signature);
  }

  @Post('payment.captured')
  @HttpCode(200)
  paymentCaptured(
    @Body() body: Record<string, unknown>,
    @Headers('x-webhook-signature') signature: string,
    @Req() req: RawBodyRequest<Request>,
  ): WebhookEvent {
    const raw = req.rawBody?.toString() || JSON.stringify(body);
    return this.webhookService.processEvent('payment.captured', body, raw, signature);
  }

  // ─── Query endpoints ───

  @Get('events')
  getAllEvents(): { count: number; events: WebhookEvent[] } {
    const events = this.webhookService.getEvents();
    return { count: events.length, events };
  }

  @Get('events/:type')
  getEventsByType(@Param('type') type: string): { count: number; type: string; events: WebhookEvent[] } {
    const events = this.webhookService.getEvents(type);
    return { count: events.length, type, events };
  }
}
