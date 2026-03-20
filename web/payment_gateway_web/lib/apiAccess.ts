export interface ApiAccessCredentials {
  merchantId: string;
  apiKey: string;
}

const STORAGE_KEY = 'pg_api_access';
const UPDATE_EVENT = 'pg_api_access_updated';

function isBrowser(): boolean {
  return typeof window !== 'undefined';
}

export function readApiAccess(): ApiAccessCredentials | null {
  if (!isBrowser()) {
    return null;
  }

  const raw = window.localStorage.getItem(STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as Partial<ApiAccessCredentials>;
    const merchantId = parsed.merchantId?.trim() ?? '';
    const apiKey = parsed.apiKey?.trim() ?? '';

    if (!merchantId || !apiKey) {
      return null;
    }

    return { merchantId, apiKey };
  } catch {
    return null;
  }
}

export function writeApiAccess(credentials: ApiAccessCredentials): void {
  if (!isBrowser()) {
    return;
  }

  window.localStorage.setItem(
    STORAGE_KEY,
    JSON.stringify({
      merchantId: credentials.merchantId.trim(),
      apiKey: credentials.apiKey.trim(),
    })
  );

  window.dispatchEvent(new Event(UPDATE_EVENT));
}

export function clearApiAccess(): void {
  if (!isBrowser()) {
    return;
  }

  window.localStorage.removeItem(STORAGE_KEY);
  window.dispatchEvent(new Event(UPDATE_EVENT));
}

export function onApiAccessUpdated(listener: () => void): () => void {
  if (!isBrowser()) {
    return () => undefined;
  }

  const handleStorage = (event: StorageEvent) => {
    if (event.key === STORAGE_KEY) {
      listener();
    }
  };

  window.addEventListener('storage', handleStorage);
  window.addEventListener(UPDATE_EVENT, listener);

  return () => {
    window.removeEventListener('storage', handleStorage);
    window.removeEventListener(UPDATE_EVENT, listener);
  };
}

