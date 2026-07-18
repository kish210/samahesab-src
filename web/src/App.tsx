import { Navigate, Route, BrowserRouter, Routes } from 'react-router-dom';
import type { ReactNode } from 'react';
import { AuthProvider, useAuth } from './auth/AuthContext';
import { Shell } from './components/Shell';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { CustomersPage } from './pages/CustomersPage';
import { CustomerCardPage } from './pages/CustomerCardPage';
import { SuppliersPage } from './pages/SuppliersPage';
import { ProductsPage } from './pages/ProductsPage';
import { ProductCardPage } from './pages/ProductCardPage';
import { WarehousePage } from './pages/WarehousePage';
import { TreasuryPage } from './pages/TreasuryPage';
import { ChequesPage } from './pages/ChequesPage';
import { VouchersPage } from './pages/VouchersPage';
import { TrialBalancePage } from './pages/TrialBalancePage';
import { GeneralLedgerPage } from './pages/GeneralLedgerPage';
import { SalesInvoicesPage } from './pages/SalesInvoicesPage';
import { CreateSalesInvoicePage } from './pages/CreateSalesInvoicePage';
import { PurchaseInvoicesPage } from './pages/PurchaseInvoicesPage';
import { CreatePurchaseInvoicePage } from './pages/CreatePurchaseInvoicePage';
import { ModulesPage } from './pages/ModulesPage';
import { PartyEditPage } from './pages/PartyEditPage';
import { ProductEditPage } from './pages/ProductEditPage';

function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, isReady } = useAuth();
  if (!isReady) return null;
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <ProtectedRoute>
            <Shell />
          </ProtectedRoute>
        }
      >
        <Route index element={<DashboardPage />} />

        <Route path="customers" element={<CustomersPage />} />
        <Route path="parties/new" element={<PartyEditPage />} />
        <Route path="parties/:id/edit" element={<PartyEditPage />} />
        <Route path="customers/:id" element={<CustomerCardPage />} />
        <Route path="parties/:id" element={<CustomerCardPage />} />
        <Route path="suppliers" element={<SuppliersPage />} />

        <Route path="products" element={<ProductsPage />} />
        <Route path="products/new" element={<ProductEditPage />} />
        <Route path="products/:id/edit" element={<ProductEditPage />} />
        <Route path="products/:id" element={<ProductCardPage />} />
        <Route path="warehouse" element={<WarehousePage />} />

        <Route path="sales" element={<SalesInvoicesPage />} />
        <Route path="sales/new" element={<CreateSalesInvoicePage />} />
        <Route path="purchase" element={<PurchaseInvoicesPage />} />
        <Route path="purchase/new" element={<CreatePurchaseInvoicePage />} />

        <Route path="treasury" element={<TreasuryPage />} />
        <Route path="cheques" element={<ChequesPage />} />

        <Route path="vouchers" element={<VouchersPage />} />
        <Route path="trial-balance" element={<TrialBalancePage />} />
        <Route path="general-ledger" element={<GeneralLedgerPage />} />

        <Route path="modules" element={<ModulesPage />} />
      </Route>
    </Routes>
  );
}

export default function App() {
  return (
    // basename از BASE_URLِ Vite (=/web/) خوانده می‌شود تا با base پیکربندی هم‌گام بماند —
    // سرورِ API کلاینت را زیرِ /web/ سرو می‌کند.
    <BrowserRouter basename={import.meta.env.BASE_URL}>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  );
}
