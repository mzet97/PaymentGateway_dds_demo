import type { NextConfig } from "next";

// Allow self-signed certificates for Authentik OIDC in development
if (process.env.NODE_TLS_REJECT_UNAUTHORIZED === '0') {
  process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
}

const API_URL = process.env.API_URL || "http://localhost:5000";

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        source: "/api/v1/:path*",
        destination: `${API_URL}/api/v1/:path*`,
      },
      {
        source: "/health",
        destination: `${API_URL}/health`,
      },
    ];
  },
};

export default nextConfig;
