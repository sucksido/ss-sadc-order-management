// Types mirroring the API DTOs. Kept in one place so the rest of the app shares a single,
// strongly-typed contract with the backend.

export type OrderStatus = 'Pending' | 'Paid' | 'Fulfilled' | 'Cancelled';

export interface Customer {
  id: string;
  name: string;
  email: string;
  countryCode: string;
  createdAt: string;
}

export interface CreateCustomerRequest {
  name: string;
  email: string;
  countryCode: string;
}

export interface OrderLineItem {
  id: string;
  productSku: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: string;
  customerId: string;
  status: OrderStatus;
  createdAt: string;
  currencyCode: string;
  totalAmount: number;
  lines: OrderLineItem[];
}

export interface CreateOrderLineItemRequest {
  productSku: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateOrderRequest {
  customerId: string;
  currencyCode: string;
  lines: CreateOrderLineItemRequest[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}
