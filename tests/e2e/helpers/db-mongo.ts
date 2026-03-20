import { MongoClient, Db } from 'mongodb';
import dotenv from 'dotenv';
import path from 'path';

dotenv.config({ path: path.resolve(__dirname, '..', '.env') });

const MONGO_URL = process.env.MONGO_URL || 'mongodb://admin:Admin%40123@mongodb.home.arpa:27017/?authSource=admin';
const MONGO_DB = process.env.MONGO_DB || 'payment-gateway';

let client: MongoClient | null = null;
let db: Db | null = null;

export async function connect(): Promise<Db> {
  if (db) return db;
  client = new MongoClient(MONGO_URL);
  await client.connect();
  db = client.db(MONGO_DB);
  return db;
}

export async function disconnect(): Promise<void> {
  if (client) {
    await client.close();
    client = null;
    db = null;
  }
}

export async function findPayment(id: string) {
  const database = await connect();
  return database.collection('pending_payments').findOne({ _id: id } as Record<string, unknown>);
}

export async function findWebhooks(merchantId: string) {
  const database = await connect();
  return database.collection('webhooks').find({ merchantId }).toArray();
}

export async function findEvents(paymentId: string) {
  const database = await connect();
  return database.collection('transaction_events').find({ paymentId }).toArray();
}

export async function countPayments(filter: Record<string, unknown> = {}) {
  const database = await connect();
  return database.collection('pending_payments').countDocuments(filter);
}
