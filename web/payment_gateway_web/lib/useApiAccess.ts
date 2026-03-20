'use client';

import { useCallback, useEffect, useState } from 'react';
import {
  ApiAccessCredentials,
  clearApiAccess,
  onApiAccessUpdated,
  readApiAccess,
  writeApiAccess,
} from './apiAccess';

export function useApiAccess() {
  const [credentials, setCredentials] = useState<ApiAccessCredentials | null>(null);

  useEffect(() => {
    setCredentials(readApiAccess());

    const unsubscribe = onApiAccessUpdated(() => {
      setCredentials(readApiAccess());
    });

    return unsubscribe;
  }, []);

  const saveCredentials = useCallback((next: ApiAccessCredentials) => {
    writeApiAccess(next);
  }, []);

  const removeCredentials = useCallback(() => {
    clearApiAccess();
  }, []);

  return {
    credentials,
    hasCredentials: !!credentials,
    saveCredentials,
    removeCredentials,
  };
}
