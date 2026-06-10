import { request } from './client';
import type { Order, CreateOrderRequest, OrderStatus, PagedResult } from './types';

export function createOrder(input: CreateOrderRequest, token: string | null): Promise<Order> {
  return request<Order>('/api/orders', { method: 'POST', body: input, token });
}

export function getOrder(id: string, token: string | null, signal?: AbortSignal): Promise<Order> {
  return request<Order>(`/api/orders/${id}`, { token, signal });
}

export function listOrders(
  params: { customerId?: string; status?: OrderStatus; page?: number; pageSize?: number; sort?: string },
  token: string | null,
  signal?: AbortSignal,
): Promise<PagedResult<Order>> {
  const query = new URLSearchParams();
  if (params.customerId) query.set('customerId', params.customerId);
  if (params.status) query.set('status', params.status);
  if (params.page) query.set('page', String(params.page));
  if (params.pageSize) query.set('pageSize', String(params.pageSize));
  if (params.sort) query.set('sort', params.sort);

  const qs = query.toString();
  return request<PagedResult<Order>>(`/api/orders${qs ? `?${qs}` : ''}`, { token, signal });
}

export function updateOrderStatus(
  id: string,
  status: OrderStatus,
  idempotencyKey: string,
  token: string | null,
): Promise<Order> {
  return request<Order>(`/api/orders/${id}/status`, {
    method: 'PUT',
    body: { status },
    token,
    headers: { 'Idempotency-Key': idempotencyKey },
  });
}
