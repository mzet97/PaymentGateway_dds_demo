import NextAuth, { type NextAuthOptions } from 'next-auth';
import AuthentikProvider from 'next-auth/providers/authentik';

function decodeJwtPayload(token: string): Record<string, unknown> {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return {};
    const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const decoded = Buffer.from(payload, 'base64').toString('utf-8');
    return JSON.parse(decoded) as Record<string, unknown>;
  } catch {
    return {};
  }
}

function extractMerchantId(claims: Record<string, unknown>): string | undefined {
  // Try the same claim names the backend checks:
  // merchant_id, merchantId, sub, NameIdentifier
  for (const key of ['merchant_id', 'merchantId']) {
    const val = claims[key];
    if (typeof val === 'string' && val.trim()) return val.trim();
  }
  return undefined;
}

function extractRoles(claims: Record<string, unknown>): string[] {
  // Backend checks: roles, role, groups
  for (const key of ['roles', 'role', 'groups']) {
    const val = claims[key];
    if (Array.isArray(val)) return val.filter((r): r is string => typeof r === 'string');
    if (typeof val === 'string') return [val];
  }
  return [];
}

export const authOptions: NextAuthOptions = {
  providers: [
    AuthentikProvider({
      clientId: process.env.AUTHENTIK_CLIENT_ID || 'payment-gateway',
      clientSecret: process.env.AUTHENTIK_CLIENT_SECRET || '',
      issuer: process.env.AUTHENTIK_ISSUER || 'https://authentik.home.arpa',
      authorization: {
        params: {
          scope: 'openid profile email',
        },
      },
    }),
  ],
  pages: {
    signIn: '/auth/signin',
    error: '/auth/error',
  },
  session: {
    strategy: 'jwt',
    maxAge: 24 * 60 * 60,
  },
  callbacks: {
    async jwt({ token, account }) {
      if (account) {
        token.accessToken = account.access_token;
        token.idToken = account.id_token;
        token.refreshToken = account.refresh_token;
        token.expiresAt = account.expires_at;

        // Decode the access token to extract merchant_id and roles
        const accessClaims = account.access_token
          ? decodeJwtPayload(account.access_token)
          : {};
        const idClaims = account.id_token
          ? decodeJwtPayload(account.id_token)
          : {};

        // Merge claims - access token takes precedence
        const allClaims = { ...idClaims, ...accessClaims };

        token.merchantId = extractMerchantId(allClaims);
        token.roles = extractRoles(allClaims);
      }

      return token;
    },
    async session({ session, token }) {
      session.accessToken = typeof token.accessToken === 'string' ? token.accessToken : undefined;
      session.idToken = typeof token.idToken === 'string' ? token.idToken : undefined;
      session.expiresAt = typeof token.expiresAt === 'number' ? token.expiresAt : undefined;
      session.merchantId = typeof token.merchantId === 'string' ? token.merchantId : undefined;
      session.roles = Array.isArray(token.roles) ? token.roles : [];
      return session;
    },
  },
  secret: process.env.NEXTAUTH_SECRET,
  debug: true,
};

const handler = NextAuth(authOptions);

export { handler as GET, handler as POST };
