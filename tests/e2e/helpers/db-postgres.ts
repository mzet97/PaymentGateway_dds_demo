import { Pool, PoolClient } from 'pg';
import dotenv from 'dotenv';
import path from 'path';

dotenv.config({ path: path.resolve(__dirname, '..', '.env') });

let pool: Pool | null = null;

function getPool(): Pool {
  if (!pool) {
    pool = new Pool({
      host: process.env.POSTGRES_HOST || 'spsql.home.arpa',
      port: parseInt(process.env.POSTGRES_PORT || '5432'),
      database: process.env.POSTGRES_DB || 'demo-gateway',
      user: process.env.POSTGRES_USER || 'app',
      password: process.env.POSTGRES_PASSWORD || 'Admin@123',
      max: 3,
    });
  }
  return pool;
}

export async function connect(): Promise<PoolClient> {
  return getPool().connect();
}

export async function disconnect(): Promise<void> {
  if (pool) {
    await pool.end();
    pool = null;
  }
}

export async function query(sql: string, params: unknown[] = []) {
  const client = await connect();
  try {
    return await client.query(sql, params);
  } finally {
    client.release();
  }
}

export async function findPayment(id: string) {
  const result = await query('SELECT * FROM payments WHERE id = $1', [id]);
  return result.rows[0] || null;
}

export async function findTransactions(paymentId: string) {
  const result = await query(
    'SELECT * FROM transactions WHERE payment_id = $1 ORDER BY "CreatedAt"',
    [paymentId],
  );
  return result.rows;
}

export async function findMerchant(id: string) {
  const result = await query('SELECT * FROM merchants WHERE id = $1', [id]);
  return result.rows[0] || null;
}

export async function countPayments(merchantId?: string) {
  if (merchantId) {
    const result = await query('SELECT COUNT(*) as count FROM payments WHERE merchant_id = $1', [merchantId]);
    return parseInt(result.rows[0].count);
  }
  const result = await query('SELECT COUNT(*) as count FROM payments');
  return parseInt(result.rows[0].count);
}
