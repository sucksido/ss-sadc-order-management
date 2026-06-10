import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { createOrder, listOrders } from '../api/orders';
import { listCustomers } from '../api/customers';
import { ApiError } from '../api/client';
import type { Customer, CreateOrderLineItemRequest, Order, OrderStatus, PagedResult } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Spinner, ErrorBanner, Empty } from '../components/Feedback';

const PAGE_SIZE = 10;
const STATUSES: OrderStatus[] = ['Pending', 'Paid', 'Fulfilled', 'Cancelled'];

type DraftLine = CreateOrderLineItemRequest;

export function OrdersPage() {
  const { token } = useAuth();

  const [customers, setCustomers] = useState<Customer[]>([]);
  const [data, setData] = useState<PagedResult<Order> | null>(null);
  const [statusFilter, setStatusFilter] = useState<OrderStatus | ''>('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [customerId, setCustomerId] = useState('');
  const [currency, setCurrency] = useState('ZAR');
  const [lines, setLines] = useState<DraftLine[]>([{ productSku: '', quantity: 1, unitPrice: 0 }]);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    listCustomers({ pageSize: 100 }, token, controller.signal)
      .then((result) => setCustomers(result.items))
      .catch(() => undefined);
    return () => controller.abort();
  }, [token]);

  const load = useCallback(
    (signal?: AbortSignal) => {
      setLoading(true);
      setError(null);
      listOrders(
        { status: statusFilter || undefined, page, pageSize: PAGE_SIZE, sort: 'createdAt_desc' },
        token,
        signal,
      )
        .then(setData)
        .catch((err: unknown) => {
          if (err instanceof DOMException && err.name === 'AbortError') return;
          setError(err instanceof Error ? err.message : 'Failed to load orders.');
        })
        .finally(() => setLoading(false));
    },
    [statusFilter, page, token],
  );

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, [load]);

  const draftTotal = useMemo(
    () => lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0),
    [lines],
  );

  function updateLine(index: number, patch: Partial<DraftLine>) {
    setLines((prev) => prev.map((line, i) => (i === index ? { ...line, ...patch } : line)));
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setFormError(null);
    try {
      await createOrder({ customerId, currencyCode: currency, lines }, token);
      setLines([{ productSku: '', quantity: 1, unitPrice: 0 }]);
      setPage(1);
      load();
    } catch (err: unknown) {
      if (err instanceof ApiError && err.details) {
        setFormError(Object.values(err.details).flat().join(' ') || err.message);
      } else {
        setFormError(err instanceof Error ? err.message : 'Failed to create order.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section>
      <h2>Orders</h2>

      <form onSubmit={onSubmit} className="card">
        <h3>New order</h3>
        <label>
          Customer
          <select value={customerId} onChange={(e) => setCustomerId(e.target.value)} required>
            <option value="" disabled>
              Select a customer
            </option>
            {customers.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name} ({c.countryCode})
              </option>
            ))}
          </select>
        </label>
        <label>
          Currency (ISO 4217)
          <input value={currency} maxLength={3} onChange={(e) => setCurrency(e.target.value.toUpperCase())} required />
        </label>

        <fieldset>
          <legend>Line items</legend>
          {lines.map((line, index) => (
            <div key={index} className="line-row">
              <input
                placeholder="Product SKU"
                value={line.productSku}
                onChange={(e) => updateLine(index, { productSku: e.target.value })}
                required
              />
              <input
                type="number"
                min={1}
                value={line.quantity}
                onChange={(e) => updateLine(index, { quantity: Number(e.target.value) })}
                required
              />
              <input
                type="number"
                min={0}
                step="0.01"
                value={line.unitPrice}
                onChange={(e) => updateLine(index, { unitPrice: Number(e.target.value) })}
                required
              />
              <button
                type="button"
                onClick={() => setLines((prev) => prev.filter((_, i) => i !== index))}
                disabled={lines.length === 1}
              >
                Remove
              </button>
            </div>
          ))}
          <button
            type="button"
            onClick={() => setLines((prev) => [...prev, { productSku: '', quantity: 1, unitPrice: 0 }])}
          >
            Add line
          </button>
          <p className="muted">Draft total: {draftTotal.toFixed(2)} {currency}</p>
        </fieldset>

        <button type="submit" disabled={submitting || !customerId}>
          {submitting ? 'Saving…' : 'Create order'}
        </button>
        {formError && <ErrorBanner message={formError} />}
      </form>

      <div className="toolbar">
        <label>
          Status
          <select
            value={statusFilter}
            onChange={(e) => {
              setPage(1);
              setStatusFilter(e.target.value as OrderStatus | '');
            }}
          >
            <option value="">All</option>
            {STATUSES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </label>
      </div>

      {loading && <Spinner />}
      {error && <ErrorBanner message={error} />}

      {!loading && !error && data && (
        <>
          {data.items.length === 0 ? (
            <Empty message="No orders found." />
          ) : (
            <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Order</th>
                  <th>Status</th>
                  <th>Currency</th>
                  <th>Total</th>
                  <th>Created</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((o) => (
                  <tr key={o.id}>
                    <td>
                      <Link to={`/orders/${o.id}`}>{o.id.slice(0, 8)}…</Link>
                    </td>
                    <td><span className={`badge badge--${o.status.toLowerCase()}`}>{o.status}</span></td>
                    <td>{o.currencyCode}</td>
                    <td>{o.totalAmount.toFixed(2)}</td>
                    <td>{new Date(o.createdAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            </div>
          )}

          <div className="pagination">
            <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={data.page <= 1}>
              Previous
            </button>
            <span>
              Page {data.page} of {Math.max(data.totalPages, 1)}
            </span>
            <button onClick={() => setPage((p) => p + 1)} disabled={data.page >= data.totalPages}>
              Next
            </button>
          </div>
        </>
      )}
    </section>
  );
}
