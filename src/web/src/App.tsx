import { useState } from 'react';
import { NavLink, Route, Routes, Navigate } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import { CustomersPage } from './pages/CustomersPage';
import { OrdersPage } from './pages/OrdersPage';
import { OrderDetailsPage } from './pages/OrderDetailsPage';
import { ErrorBanner } from './components/Feedback';

export default function App() {
  const { isAuthenticated, signInDev } = useAuth();
  const [authError, setAuthError] = useState<string | null>(null);
  const [signingIn, setSigningIn] = useState(false);

  async function onSignIn() {
    setSigningIn(true);
    setAuthError(null);
    try {
      await signInDev();
    } catch (err: unknown) {
      setAuthError(
        err instanceof Error
          ? `${err.message} (is the API running with a Development dev-token endpoint?)`
          : 'Sign-in failed.',
      );
    } finally {
      setSigningIn(false);
    }
  }

  return (
    <div className="app">
      <header>
        <h1>SADC Order Management</h1>
        <nav>
          <NavLink to="/customers">Customers</NavLink>
          <NavLink to="/orders">Orders</NavLink>
        </nav>
        <div className="auth">
          {isAuthenticated ? (
            <span className="auth-status">Signed in (dev token)</span>
          ) : (
            <button onClick={onSignIn} disabled={signingIn}>
              {signingIn ? 'Signing in…' : 'Sign in (dev token)'}
            </button>
          )}
        </div>
      </header>

      {authError && <ErrorBanner message={authError} />}

      <main>
        {isAuthenticated ? (
          <Routes>
            <Route path="/" element={<Navigate to="/customers" replace />} />
            <Route path="/customers" element={<CustomersPage />} />
            <Route path="/orders" element={<OrdersPage />} />
            <Route path="/orders/:id" element={<OrderDetailsPage />} />
            <Route path="*" element={<Navigate to="/customers" replace />} />
          </Routes>
        ) : (
          <p className="muted">Sign in to manage customers and orders.</p>
        )}
      </main>
    </div>
  );
}
