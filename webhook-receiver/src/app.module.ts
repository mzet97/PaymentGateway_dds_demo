import { Module } from '@nestjs/common';
import { WebhookController } from './webhook.controller';
import { WebhookService } from './webhook.service';
import { HealthController } from './health.controller';

@Module({
  controllers: [WebhookController, HealthController],
  providers: [WebhookService],
})
export class AppModule {}
