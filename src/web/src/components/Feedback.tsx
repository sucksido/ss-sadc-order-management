export function Spinner({ label = 'Loading…' }: { label?: string }) {
  return (
    <p role="status" aria-live="polite" className="muted">
      {label}
    </p>
  );
}

export function ErrorBanner({ message }: { message: string }) {
  return (
    <p role="alert" className="error">
      {message}
    </p>
  );
}

export function Empty({ message }: { message: string }) {
  return <p className="muted">{message}</p>;
}
