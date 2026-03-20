'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

interface UseWebSocketOptions {
  url: string;
  onMessage?: (data: unknown) => void;
  onConnect?: () => void;
  onDisconnect?: () => void;
  onError?: (error: Event) => void;
  reconnectAttempts?: number;
  reconnectInterval?: number;
}

interface UseWebSocketReturn {
  isConnected: boolean;
  lastMessage: unknown | null;
  sendMessage: (data: unknown) => void;
  reconnect: () => void;
}

interface PaymentSocketMessage {
  type?: string;
  payment?: {
    id?: string;
    paymentId?: string;
    merchantId?: string;
    [key: string]: unknown;
  };
}

export function useWebSocket({
  url,
  onMessage,
  onConnect,
  onDisconnect,
  onError,
  reconnectAttempts = 5,
  reconnectInterval = 3000,
}: UseWebSocketOptions): UseWebSocketReturn {
  const [isConnected, setIsConnected] = useState(false);
  const [lastMessage, setLastMessage] = useState<unknown | null>(null);
  const wsRef = useRef<WebSocket | null>(null);
  const attemptsRef = useRef(0);
  const reconnectTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const connectRef = useRef<() => void>(() => undefined);

  const disconnect = useCallback(() => {
    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }
    if (wsRef.current) {
      wsRef.current.close();
      wsRef.current = null;
    }
    setIsConnected(false);
  }, []);

  const connect = useCallback(() => {
    if (!url) {
      return;
    }

    if (
      wsRef.current?.readyState === WebSocket.OPEN ||
      wsRef.current?.readyState === WebSocket.CONNECTING
    ) {
      return;
    }

    const ws = new WebSocket(url);

    ws.onopen = () => {
      setIsConnected(true);
      attemptsRef.current = 0;
      onConnect?.();
    };

    ws.onmessage = (event) => {
      let parsed: unknown = event.data;
      if (typeof event.data === 'string') {
        try {
          parsed = JSON.parse(event.data);
        } catch {
          parsed = event.data;
        }
      }

      setLastMessage(parsed);
      onMessage?.(parsed);
    };

    ws.onclose = () => {
      setIsConnected(false);
      onDisconnect?.();

      if (attemptsRef.current < reconnectAttempts) {
        attemptsRef.current += 1;
        reconnectTimeoutRef.current = setTimeout(() => {
          connectRef.current();
        }, reconnectInterval);
      }
    };

    ws.onerror = (error) => {
      onError?.(error);
    };

    wsRef.current = ws;
  }, [onConnect, onDisconnect, onError, onMessage, reconnectAttempts, reconnectInterval, url]);

  const sendMessage = useCallback((data: unknown) => {
    if (wsRef.current?.readyState === WebSocket.OPEN) {
      wsRef.current.send(JSON.stringify(data));
    }
  }, []);

  const reconnect = useCallback(() => {
    attemptsRef.current = 0;
    disconnect();
    connectRef.current();
  }, [disconnect]);

  useEffect(() => {
    connectRef.current = connect;
  }, [connect]);

  useEffect(() => {
    connect();

    return () => {
      disconnect();
    };
  }, [connect, disconnect]);

  return {
    isConnected,
    lastMessage,
    sendMessage,
    reconnect,
  };
}

export function usePaymentUpdates(
  apiKey: string | undefined,
  onPaymentUpdate?: (payment: PaymentSocketMessage['payment']) => void
) {
  const wsUrl =
    typeof window !== 'undefined' && apiKey
      ? `${process.env.NEXT_PUBLIC_WS_URL || 'ws://localhost:5000'}/ws/payments?apiKey=${encodeURIComponent(apiKey)}`
      : '';

  return useWebSocket({
    url: wsUrl,
    onMessage: (data) => {
      const message = data as PaymentSocketMessage;
      if (message.type?.startsWith('payment.')) {
        onPaymentUpdate?.(message.payment);
      }
    },
    reconnectAttempts: 10,
  });
}
