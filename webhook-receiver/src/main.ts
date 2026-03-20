import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);
  const port = process.env.PORT || 4000;
  await app.listen(port);
  console.log(`Webhook Receiver running on http://localhost:${port}`);
  console.log(`Endpoints:`);
  console.log(`  POST /webhooks/payment.created`);
  console.log(`  POST /webhooks/payment.approved`);
  console.log(`  POST /webhooks/payment.rejected`);
  console.log(`  POST /webhooks/payment.refunded`);
  console.log(`  POST /webhooks/payment.captured`);
  console.log(`  GET  /webhooks/events          (list all received events)`);
  console.log(`  GET  /webhooks/events/:type     (list events by type)`);
  console.log(`  GET  /health`);
}
bootstrap();
