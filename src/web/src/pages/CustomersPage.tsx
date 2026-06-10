import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { createCustomer, listCustomers } from '../api/customers';
import { ApiError } from '../api/client';
import type { Customer, PagedResult } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Spinner, ErrorBanner, Empty } from '../components/Feedback';

const PAGE_SIZE = 10;

export function CustomersPage() {
  const { token } = useAuth();

  const [data, setData] = useState<PagedResult<Customer> | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [form, setForm] = useState({ name: '', email: '', countryCode: 'ZA' });
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(
    (signal?: AbortSignal) => {
      setLoading(true);
      setError(null);
      listCustomers({ search, page, pageSize: PAGE_SIZE }, token, signal)
        .then(setData)
        .catch((err: unknown) => {
          if (err instanceof DOMException && err.name === 'AbortError') return;
          setError(err instanceof Error ? err.message : 'Failed to load customers.');
        })
        .finally(() => setLoading(false));
    },
    [search, page, token],
  );

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, [load]);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setFormError(null);
    try {
      await createCustomer(form, token);
      setForm({ name: '', email: '', countryCode: 'ZA' });
      setPage(1);
      load();
    } catch (err: unknown) {
      if (err instanceof ApiError && err.details) {
        const messages = Object.values(err.details).flat().join(' ');
        setFormError(messages || err.message);
      } else {
        setFormError(err instanceof Error ? err.message : 'Failed to create customer.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section>
      <h2>Customers</h2>

      <form onSubmit={onSubmit} className="card">
        <h3>New customer</h3>
        <label>
          Name
          <input
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            required
          />
        </label>
        <label>
          Email
          <input
            type="email"
            value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
            required
          />
        </label>
        <label>
          Country code (SADC, ISO 3166-1 alpha-2)
          <input
            value={form.countryCode}
            maxLength={2}
            onChange={(e) => setForm({ ...form, countryCode: e.target.value.toUpperCase() })}
            required
          />
        </label>
        <button type="submit" disabled={submitting}>
          {submitting ? 'Saving…' : 'Create customer'}
        </button>
        {formError && <ErrorBanner message={formError} />}
      </form>

      <div className="toolbar">
        <input
          placeholder="Search by name or email"
          value={search}
          onChange={(e) => {
            setPage(1);
            setSearch(e.target.value);
          }}
        />
      </div>

      {loading && <Spinner />}
      {error && <ErrorBanner message={error} />}

      {!loading && !error && data && (
        <>
          {data.items.length === 0 ? (
            <Empty message="No customers found." />
          ) : (
            <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Email</th>
                  <th>Country</th>
                  <th>Created</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((c) => (
                  <tr key={c.id}>
                    <td>{c.name}</td>
                    <td>{c.email}</td>
                    <td>{c.countryCode}</td>
                    <td>{new Date(c.createdAt).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            </div>
          )}

          <Pagination
            page={data.page}
            totalPages={data.totalPages}
            onPrev={() => setPage((p) => Math.max(1, p - 1))}
            onNext={() => setPage((p) => p + 1)}
          />
        </>
      )}
    </section>
  );
}

function Pagination({
  page,
  totalPages,
  onPrev,
  onNext,
}: {
  page: number;
  totalPages: number;
  onPrev: () => void;
  onNext: () => void;
}) {
  return (
    <div className="pagination">
      <button onClick={onPrev} disabled={page <= 1}>
        Previous
      </button>
      <span>
        Page {page} of {Math.max(totalPages, 1)}
      </span>
      <button onClick={onNext} disabled={page >= totalPages}>
        Next
      </button>
    </div>
  );
}
