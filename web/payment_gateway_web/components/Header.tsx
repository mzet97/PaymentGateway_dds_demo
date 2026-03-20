'use client';

import { useSession, signOut } from 'next-auth/react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

const NAV_LINKS = [
  { href: '/', label: 'Painel' },
  { href: '/payments', label: 'Pagamentos' },
  { href: '/merchants', label: 'Merchants' },
  { href: '/transactions', label: 'Transacoes' },
  { href: '/analytics', label: 'Analytics' },
  { href: '/webhooks', label: 'Webhooks' },
  { href: '/settings', label: 'Configurações' },
];

export function Header() {
  const { data: session, status } = useSession();
  const pathname = usePathname();

  if (pathname?.startsWith('/auth/')) {
    return null;
  }

  const isActive = (href: string) => {
    if (href === '/') return pathname === '/';
    return pathname?.startsWith(href);
  };

  return (
    <header className="bg-white border-b border-gray-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex justify-between items-center h-16">
          <div className="flex items-center gap-8">
            <Link href="/" className="text-xl font-bold text-gray-900">
              Payment Gateway
            </Link>
            <nav className="hidden md:flex gap-1">
              {NAV_LINKS.map((link) => (
                <Link
                  key={link.href}
                  href={link.href}
                  className={`px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                    isActive(link.href)
                      ? 'bg-blue-50 text-blue-700'
                      : 'text-gray-600 hover:text-gray-900 hover:bg-gray-50'
                  }`}
                >
                  {link.label}
                </Link>
              ))}
            </nav>
          </div>
          <div className="flex items-center gap-4">
            {status === 'loading' ? (
              <span className="text-sm text-gray-500">Carregando...</span>
            ) : session ? (
              <div className="flex items-center gap-4">
                <div className="text-right hidden sm:block">
                  <p className="text-sm font-medium text-gray-700">
                    {session.user?.name || session.user?.email}
                  </p>
                  {session.merchantId && (
                    <p className="text-xs text-gray-500 truncate max-w-[160px]" title={session.merchantId}>
                      Merchant: {session.merchantId.substring(0, 8)}...
                    </p>
                  )}
                </div>
                <button
                  onClick={() => signOut({ callbackUrl: '/auth/signin' })}
                  className="text-sm text-gray-600 hover:text-gray-900 px-3 py-1.5 rounded-md hover:bg-gray-50 transition-colors"
                >
                  Sair
                </button>
              </div>
            ) : (
              <Link
                href="/auth/signin"
                className="text-sm text-white bg-blue-600 hover:bg-blue-700 px-4 py-2 rounded-lg transition-colors"
              >
                Entrar
              </Link>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
