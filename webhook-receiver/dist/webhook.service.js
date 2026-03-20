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
var WebhookService_1;
Object.defineProperty(exports, "__esModule", { value: true });
exports.WebhookService = void 0;
const common_1 = require("@nestjs/common");
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
let WebhookService = WebhookService_1 = class WebhookService {
    constructor() {
        this.logger = new common_1.Logger(WebhookService_1.name);
        this.secret = process.env.WEBHOOK_SECRET || 'webhook-secret-demo-2026';
        this.logsDir = path.join(process.cwd(), 'logs');
        if (!fs.existsSync(this.logsDir)) {
            fs.mkdirSync(this.logsDir, { recursive: true });
        }
        this.logger.log(`Webhook secret: ${this.secret.substring(0, 8)}...`);
        this.logger.log(`Logs directory: ${this.logsDir}`);
    }
    validateSignature(payload, signature) {
        if (!signature)
            return false;
        const expected = crypto
            .createHmac('sha256', this.secret)
            .update(payload)
            .digest('hex');
        const sig = signature.replace('sha256=', '');
        return crypto.timingSafeEqual(Buffer.from(sig, 'hex'), Buffer.from(expected, 'hex'));
    }
    processEvent(type, body, rawBody, signatureHeader) {
        const signatureValid = this.validateSignature(rawBody, signatureHeader);
        const event = {
            id: crypto.randomUUID(),
            type,
            receivedAt: new Date().toISOString(),
            signatureValid,
            payload: body,
        };
        this.logger.log(`[${type}] payment=${body.paymentId || body.id || '?'} ` +
            `signature=${signatureValid ? 'VALID' : 'INVALID/MISSING'} ` +
            `amount=${body.amount || '?'}`);
        this.saveToFile(event);
        return event;
    }
    saveToFile(event) {
        const line = JSON.stringify(event) + '\n';
        const typeFile = path.join(this.logsDir, `${event.type}.jsonl`);
        fs.appendFileSync(typeFile, line);
        const allFile = path.join(this.logsDir, 'all-events.jsonl');
        fs.appendFileSync(allFile, line);
    }
    getEvents(type) {
        const fileName = type ? `${type}.jsonl` : 'all-events.jsonl';
        const filePath = path.join(this.logsDir, fileName);
        if (!fs.existsSync(filePath))
            return [];
        return fs
            .readFileSync(filePath, 'utf-8')
            .split('\n')
            .filter((line) => line.trim())
            .map((line) => {
            try {
                return JSON.parse(line);
            }
            catch {
                return null;
            }
        })
            .filter((e) => e !== null)
            .reverse();
    }
    getSecret() {
        return this.secret;
    }
};
exports.WebhookService = WebhookService;
exports.WebhookService = WebhookService = WebhookService_1 = __decorate([
    (0, common_1.Injectable)(),
    __metadata("design:paramtypes", [])
], WebhookService);
//# sourceMappingURL=webhook.service.js.map