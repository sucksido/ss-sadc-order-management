// Thin typed fetch wrapper. Centralises base URL, bearer auth, JSON handling and error
// normalisation so callers get either typed data or a typed ApiError.

const BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '';

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    public readonly details?: Record<string, string[]>,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  token?: string | null;
  headers?: Record<string, string>;
  signal?: AbortSignal;
}

interface ProblemResponse {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, token, headers = {}, signal } = options;

  const init: RequestInit = {
    method,
    headers: {
      Accept: 'application/json',
      ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...headers,
    },
    ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
    ...(signal ? { signal } : {}),
  };

  const response = await fetch(`${BASE_URL}${path}`, init);

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const payload = text ? (JSON.parse(text) as unknown) : undefined;

  if (!response.ok) {
    const problem = (payload ?? {}) as ProblemResponse;
    const message = problem.detail ?? problem.title ?? `Request failed with status ${response.status}`;
    throw new ApiError(response.status, message, problem.errors);
  }

  return payload as T;
}
