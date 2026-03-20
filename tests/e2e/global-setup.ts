import dotenv from 'dotenv';
import path from 'path';

dotenv.config({ path: path.resolve(__dirname, '.env') });

async function globalSetup() {
  const apiUrl = process.env.API_BASE_URL || 'http://localhost:5000';
  const webhookUrl = process.env.WEBHOOK_RECEIVER_URL || 'http://localhost:4000';

  console.log('Global Setup: Checking service availability...');

  // Check API
  try {
    const res = await fetch(`${apiUrl}/health`);
    if (!res.ok) throw new Error(`API returned ${res.status}`);
    console.log(`  API (${apiUrl}): OK`);
  } catch (e) {
    throw new Error(`API not reachable at ${apiUrl}. Start services first. Error: ${e}`);
  }

  // Check webhook receiver
  try {
    const res = await fetch(`${webhookUrl}/health`);
    if (!res.ok) throw new Error(`Webhook receiver returned ${res.status}`);
    console.log(`  Webhook Receiver (${webhookUrl}): OK`);
  } catch (e) {
    console.warn(`  Webhook Receiver (${webhookUrl}): NOT AVAILABLE - webhook tests will fail`);
  }

  console.log('Global Setup: Complete');
}

export default globalSetup;
