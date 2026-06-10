import { request } from './client';
import type { Customer, CreateCustomerRequest, PagedResult } from './types';

export function createCustomer(input: CreateCustomerRequest, token: string | null): Promise<Customer> {
  return request<Customer>('/api/customers', { method: 'POST', body: input, token });
}

export function getCustomer(id: string, token: string | null): Promise<Customer> {
  return request<Customer>(`/api/customers/${id}`, { token });
}

export function listCustomers(
  params: { search?: string; page?: number; pageSize?: number },
  token: string | null,
  signal?: AbortSignal,
): Promise<PagedResult<Customer>> {
  const query = new URLSearchParams();
  if (params.search) query.set('search', params.search);
  if (params.page) query.set('page', String(params.page));
  if (params.pageSize) query.set('pageSize', String(params.pageSize));

  const qs = query.toString();
  return request<PagedResult<Customer>>(`/api/customers${qs ? `?${qs}` : ''}`, { token, signal });
}
