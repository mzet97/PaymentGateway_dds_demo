export interface WebhookEvent {
    id: string;
    type: string;
    receivedAt: string;
    signatureValid: boolean;
    payload: Record<string, unknown>;
}
export declare class WebhookService {
    private readonly logger;
    private readonly secret;
    private readonly logsDir;
    constructor();
    validateSignature(payload: string, signature: string | undefined): boolean;
    processEvent(type: string, body: Record<string, unknown>, rawBody: string, signatureHeader: string | undefined): WebhookEvent;
    private saveToFile;
    getEvents(type?: string): WebhookEvent[];
    getSecret(): string;
}
