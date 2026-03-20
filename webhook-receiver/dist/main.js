"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const core_1 = require("@nestjs/core");
const app_module_1 = require("./app.module");
async function bootstrap() {
    const app = await core_1.NestFactory.create(app_module_1.AppModule);
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
//# sourceMappingURL=main.js.map