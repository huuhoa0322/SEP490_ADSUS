/* eslint-disable @typescript-eslint/no-explicit-any */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { apiClient, ACCESS_TOKEN_KEY } from '@/lib/api-client';

describe('apiClient interceptors', () => {
  let originalLocation: Location;

  beforeEach(() => {
    const store: Record<string, string> = {};
    const mockLocalStorage = {
      getItem: vi.fn((key: string) => store[key] || null),
      setItem: vi.fn((key: string, value: string) => { store[key] = value.toString(); }),
      removeItem: vi.fn((key: string) => { delete store[key]; }),
    };
    Object.defineProperty(window, 'localStorage', { value: mockLocalStorage, writable: true });

    originalLocation = window.location;
    delete (window as any).location;
    window.location = { ...originalLocation, href: '/', pathname: '/' } as any;
  });

  afterEach(() => {
    (window as any).location = originalLocation;
    vi.restoreAllMocks();
  });

  it('should logout if refresh token is missing', async () => {
    window.localStorage.setItem(ACCESS_TOKEN_KEY, 'expired-token');

    // Call response interceptor directly to avoid axios adapter complexity
    const responseInterceptor = (apiClient.interceptors.response as any).handlers[0].rejected;
    
    const error = new AxiosError('401 Error', '401', { headers: new AxiosHeaders({ Authorization: 'Bearer x' }) } as any, {}, { status: 401 } as any);

    await expect(responseInterceptor(error)).rejects.toThrow();
    expect(window.location.href).toContain('/login?expired=1');
  });
});
