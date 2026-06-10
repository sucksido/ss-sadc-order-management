import { useCallback, useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getOrder, updateOrderStatus } from '../api/orders';
import type { Order, OrderStatus } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Spinner, ErrorBanner } from '../components/Feedback';

// Allowed next states mirror the server-side state machine; the UI only offers legal moves.
const NEXT_STATES: Record<OrderStatus, OrderStatus[]> = {
  Pending: ['Paid', 'Cancelled'],
  Paid: ['Fulfilled', 'Cancelled'],
  Fulfilled: [],
  Cancelled: [],
};

export function OrderDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const { token } = useAuth();

  const [order, setOrder] = useState<Order | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [updating, setUpdating] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const load = useCallback(
    (signal?: AbortSignal) => {
      if (!id) return;
      setLoading(true);
      setError(null);
      getOrder(id, token, signal)
        .then(setOrder)
        .catch((err: unknown) => {
          if (err instanceof DOMException && err.name === 'AbortError') return;
          setError(err instanceof Error ? err.message : 'Failed to load order.');
        })
        .finally(() => setLoading(false));
    },
    [id, token],
  );

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, [load]);

  async function transition(next: OrderStatus) {
    if (!order) return;
    setUpdating(true);
    setActionError(null);
    try {
      // A fresh idempotency key per user-initiated action; retries of THIS click reuse it.
      const key = crypto.randomUUID();
      const updated = await updateOrderStatus(order.id, next, key, token);
      setOrder(updated);
    } catch (err: unknown) {
      setActionError(err instanceof Error ? err.message : 'Failed to update status.');
    } finally {
      setUpdating(false);
    }
  }

  if (loading) return <Spinner />;
  if (error) return <ErrorBanner message={error} />;
  if (!order) return null;

  const nextStates = NEXT_STATES[order.status];

  return (
    <section>
      <p>
        <Link to="/orders">← Back to orders</Link>
      </p>
      <h2>Order {order.id}</h2>

      <dl className="details">
        <dt>Status</dt>
        <dd>{order.status}</dd>
        <dt>Currency</dt>
        <dd>{order.currencyCode}</dd>
        <dt>Total</dt>
        <dd>{order.totalAmount.toFixed(2)} {order.currencyCode}</dd>
        <dt>Created</dt>
        <dd>{new Date(order.createdAt).toLocaleString()}</dd>
      </dl>

      <h3>Line items</h3>
      <table>
        <thead>
          <tr>
            <th>SKU</th>
            <th>Qty</th>
            <th>Unit price</th>
            <th>Line total</th>
          </tr>
        </thead>
        <tbody>
          {order.lines.map((l) => (
            <tr key={l.id}>
              <td>{l.productSku}</td>
              <td>{l.quantity}</td>
              <td>{l.unitPrice.toFixed(2)}</td>
              <td>{l.lineTotal.toFixed(2)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <h3>Change status</h3>
      {nextStates.length === 0 ? (
        <p className="muted">This order is in a terminal state.</p>
      ) : (
        <div className="actions">
          {nextStates.map((s) => (
            <button key={s} onClick={() => transition(s)} disabled={updating}>
              Mark as {s}
            </button>
          ))}
        </div>
      )}
      {actionError && <ErrorBanner message={actionError} />}
    </section>
  );
}
