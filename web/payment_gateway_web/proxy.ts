import { NextRequest, NextResponse } from 'next/server';

export default async function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Allow auth routes, API routes, and static assets
  if (
    pathname.startsWith('/auth/') ||
    pathname.startsWith('/api/') ||
    pathname.startsWith('/_next') ||
    pathname === '/favicon.ico' ||
    pathname === '/health'
  ) {
    return NextResponse.next();
  }

  // Debug: log all cookies
  const allCookies = request.cookies.getAll();
  console.log(`[proxy] ${pathname} - cookies: ${allCookies.map(c => c.name).join(', ') || 'NONE'}`);

  // Check for next-auth session token (handles chunked cookies)
  const hasSession =
    request.cookies.has('next-auth.session-token') ||
    request.cookies.has('next-auth.session-token.0') ||
    request.cookies.has('__Secure-next-auth.session-token') ||
    request.cookies.has('__Secure-next-auth.session-token.0');

  console.log(`[proxy] ${pathname} - hasSession: ${hasSession}`);

  if (!hasSession) {
    const signInUrl = new URL('/auth/signin', request.url);
    signInUrl.searchParams.set('callbackUrl', pathname);
    return NextResponse.redirect(signInUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    '/((?!_next/static|_next/image|favicon.ico).*)',
  ],
};
