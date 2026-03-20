"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __metadata = (this && this.__metadata) || function (k, v) {
    if (typeof Reflect === "object" && typeof Reflect.metadata === "function") return Reflect.metadata(k, v);
};
var __param = (this && this.__param) || function (paramIndex, decorator) {
    return function (target, key) { decorator(target, key, paramIndex); }
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.WebhookController = void 0;
const common_1 = require("@nestjs/common");
const webhook_service_1 = require("./webhook.service");
let WebhookController = class WebhookController {
    constructor(webhookService) {
        this.webhookService = webhookService;
    }
    paymentCreated(body, signature, req) {
        const raw = req.rawBody?.toString() || JSON.stringify(body);
        return this.webhookService.processEvent('payment.created', body, raw, signature);
    }
    paymentApproved(body, signature, req) {
        const raw = req.rawBody?.toString() || JSON.stringify(body);
        return this.webhookService.processEvent('payment.approved', body, raw, signature);
    }
    paymentRejected(body, signature, req) {
        const raw = req.rawBody?.toString() || JSON.stringify(body);
        return this.webhookService.processEvent('payment.rejected', body, raw, signature);
    }
    paymentRefunded(body, signature, req) {
        const raw = req.rawBody?.toString() || JSON.stringify(body);
        return this.webhookService.processEvent('payment.refunded', body, raw, signature);
    }
    paymentCaptured(body, signature, req) {
        const raw = req.rawBody?.toString() || JSON.stringify(body);
        return this.webhookService.processEvent('payment.captured', body, raw, signature);
    }
    getAllEvents() {
        const events = this.webhookService.getEvents();
        return { count: events.length, events };
    }
    getEventsByType(type) {
        const events = this.webhookService.getEvents(type);
        return { count: events.length, type, events };
    }
};
exports.WebhookController = WebhookController;
__decorate([
    (0, common_1.Post)('payment.created'),
    (0, common_1.HttpCode)(200),
    __param(0, (0, common_1.Body)()),
    __param(1, (0, common_1.Headers)('x-webhook-signature')),
    __param(2, (0, common_1.Req)()),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object, String, Object]),
    __metadata("design:returntype", Object)
], WebhookController.prototype, "paymentCreated", null);
__decorate([
    (0, common_1.Post)('payment.approved'),
    (0, common_1.HttpCode)(200),
    __param(0, (0, common_1.Body)()),
    __param(1, (0, common_1.Headers)('x-webhook-signature')),
    __param(2, (0, common_1.Req)()),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object, String, Object]),
    __metadata("design:returntype", Object)
], WebhookController.prototype, "paymentApproved", null);
__decorate([
    (0, common_1.Post)('payment.rejected'),
    (0, common_1.HttpCode)(200),
    __param(0, (0, common_1.Body)()),
    __param(1, (0, common_1.Headers)('x-webhook-signature')),
    __param(2, (0, common_1.Req)()),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object, String, Object]),
    __metadata("design:returntype", Object)
], WebhookController.prototype, "paymentRejected", null);
__decorate([
    (0, common_1.Post)('payment.refunded'),
    (0, common_1.HttpCode)(200),
    __param(0, (0, common_1.Body)()),
    __param(1, (0, common_1.Headers)('x-webhook-signature')),
    __param(2, (0, common_1.Req)()),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object, String, Object]),
    __metadata("design:returntype", Object)
], WebhookController.prototype, "paymentRefunded", null);
__decorate([
    (0, common_1.Post)('payment.captured'),
    (0, common_1.HttpCode)(200),
    __param(0, (0, common_1.Body)()),
    __param(1, (0, common_1.Headers)('x-webhook-signature')),
    __param(2, (0, common_1.Req)()),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [Object, String, Object]),
    __metadata("design:returntype", Object)
], WebhookController.prototype, "paymentCaptured", null);
__decorate([
    (0, common_1.Get)('events'),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", []),
    __metadata("design:returntype", Object)
], WebhookController.prototype, "getAllEvents", null);
__decorate([
    (0, common_1.Get)('events/:type'),
    __param(0, (0, common_1.Param)('type')),
    __metadata("design:type", Function),
    __metadata("design:paramtypes", [String]),
    __metadata("design:returntype", Object)
], WebhookController.prototype, "getEventsByType", null);
exports.WebhookController = WebhookController = __decorate([
    (0, common_1.Controller)('webhooks'),
    __metadata("design:paramtypes", [webhook_service_1.WebhookService])
], WebhookController);
//# sourceMappingURL=webhook.controller.js.map