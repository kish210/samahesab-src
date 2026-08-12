import { lazy, Suspense } from 'react';
import { Navigate, Route, BrowserRouter, Routes } from 'react-router-dom';
import type { ReactNode } from 'react';
import { AuthProvider, useAuth } from './auth/AuthContext';
import { Shell } from './components/Shell';
import { LoginPage } from './pages/LoginPage';

// U-WEB-PERF-SPLIT — هر صفحه lazy است تا کاربر فقط کدِ همان صفحه‌ای که بازش می‌کند
// دانلود کند، نه کدِ کلِ ۶۶ صفحه را در یک باندلِ اولیه (قبلاً ~۶۰۰KB یک‌جا).
const SetupWizardPage = lazy(() => import('./pages/SetupWizardPage').then((m) => ({ default: m.SetupWizardPage })));
const DashboardPage = lazy(() => import('./pages/DashboardPage').then((m) => ({ default: m.DashboardPage })));
const CustomersPage = lazy(() => import('./pages/CustomersPage').then((m) => ({ default: m.CustomersPage })));
const CustomerCardPage = lazy(() => import('./pages/CustomerCardPage').then((m) => ({ default: m.CustomerCardPage })));
const SuppliersPage = lazy(() => import('./pages/SuppliersPage').then((m) => ({ default: m.SuppliersPage })));
const ProductsPage = lazy(() => import('./pages/ProductsPage').then((m) => ({ default: m.ProductsPage })));
const ProductCardPage = lazy(() => import('./pages/ProductCardPage').then((m) => ({ default: m.ProductCardPage })));
const WarehousePage = lazy(() => import('./pages/WarehousePage').then((m) => ({ default: m.WarehousePage })));
const TreasuryPage = lazy(() => import('./pages/TreasuryPage').then((m) => ({ default: m.TreasuryPage })));
const ChequesPage = lazy(() => import('./pages/ChequesPage').then((m) => ({ default: m.ChequesPage })));
const VouchersPage = lazy(() => import('./pages/VouchersPage').then((m) => ({ default: m.VouchersPage })));
const CreateVoucherPage = lazy(() => import('./pages/CreateVoucherPage').then((m) => ({ default: m.CreateVoucherPage })));
const TrialBalancePage = lazy(() => import('./pages/TrialBalancePage').then((m) => ({ default: m.TrialBalancePage })));
const GeneralLedgerPage = lazy(() => import('./pages/GeneralLedgerPage').then((m) => ({ default: m.GeneralLedgerPage })));
const BalanceSheetPage = lazy(() => import('./pages/BalanceSheetPage').then((m) => ({ default: m.BalanceSheetPage })));
const IncomeStatementPage = lazy(() => import('./pages/IncomeStatementPage').then((m) => ({ default: m.IncomeStatementPage })));
const BranchSummaryPage = lazy(() => import('./pages/BranchSummaryPage').then((m) => ({ default: m.BranchSummaryPage })));
const AccountsPage = lazy(() => import('./pages/AccountsPage').then((m) => ({ default: m.AccountsPage })));
const SalesInvoicesPage = lazy(() => import('./pages/SalesInvoicesPage').then((m) => ({ default: m.SalesInvoicesPage })));
const SalesInvoiceDetailPage = lazy(() => import('./pages/SalesInvoiceDetailPage').then((m) => ({ default: m.SalesInvoiceDetailPage })));
const CreateSalesInvoicePage = lazy(() => import('./pages/CreateSalesInvoicePage').then((m) => ({ default: m.CreateSalesInvoicePage })));
const PurchaseInvoicesPage = lazy(() => import('./pages/PurchaseInvoicesPage').then((m) => ({ default: m.PurchaseInvoicesPage })));
const PurchaseInvoiceDetailPage = lazy(() => import('./pages/PurchaseInvoiceDetailPage').then((m) => ({ default: m.PurchaseInvoiceDetailPage })));
const CreatePurchaseInvoicePage = lazy(() => import('./pages/CreatePurchaseInvoicePage').then((m) => ({ default: m.CreatePurchaseInvoicePage })));
const ModulesPage = lazy(() => import('./pages/ModulesPage').then((m) => ({ default: m.ModulesPage })));
const SettingsPage = lazy(() => import('./pages/SettingsPage').then((m) => ({ default: m.SettingsPage })));
const MigrationPage = lazy(() => import('./pages/MigrationPage').then((m) => ({ default: m.MigrationPage })));
const TemplatesPage = lazy(() => import('./pages/TemplatesPage').then((m) => ({ default: m.TemplatesPage })));
const TaxInvoicingPage = lazy(() => import('./pages/TaxInvoicingPage').then((m) => ({ default: m.TaxInvoicingPage })));
const TourismPage = lazy(() => import('./pages/TourismPage').then((m) => ({ default: m.TourismPage })));
const PartyEditPage = lazy(() => import('./pages/PartyEditPage').then((m) => ({ default: m.PartyEditPage })));
const ProductEditPage = lazy(() => import('./pages/ProductEditPage').then((m) => ({ default: m.ProductEditPage })));
const PosPage = lazy(() => import('./pages/PosPage').then((m) => ({ default: m.PosPage })));
const RestaurantHallsPage = lazy(() => import('./pages/RestaurantHallsPage').then((m) => ({ default: m.RestaurantHallsPage })));
const RestaurantKitchenPage = lazy(() => import('./pages/RestaurantKitchenPage').then((m) => ({ default: m.RestaurantKitchenPage })));
const ReturnFormPage = lazy(() => import('./pages/ReturnFormPage').then((m) => ({ default: m.ReturnFormPage })));
const EmployeesPage = lazy(() => import('./pages/EmployeesPage').then((m) => ({ default: m.EmployeesPage })));
const EmployeeEditPage = lazy(() => import('./pages/EmployeeEditPage').then((m) => ({ default: m.EmployeeEditPage })));
const PayrollPage = lazy(() => import('./pages/PayrollPage').then((m) => ({ default: m.PayrollPage })));
const HotelPage = lazy(() => import('./pages/HotelPage').then((m) => ({ default: m.HotelPage })));
const AttendancePage = lazy(() => import('./pages/AttendancePage').then((m) => ({ default: m.AttendancePage })));
const ContractingPage = lazy(() => import('./pages/ContractingPage').then((m) => ({ default: m.ContractingPage })));
const SupportPage = lazy(() => import('./pages/SupportPage').then((m) => ({ default: m.SupportPage })));
const StockCountPage = lazy(() => import('./pages/StockCountPage').then((m) => ({ default: m.StockCountPage })));
const ReportsCenterPage = lazy(() => import('./pages/ReportsCenterPage').then((m) => ({ default: m.ReportsCenterPage })));
const BranchesPage = lazy(() => import('./pages/BranchesPage').then((m) => ({ default: m.BranchesPage })));
const ShiftPage = lazy(() => import('./pages/ShiftPage').then((m) => ({ default: m.ShiftPage })));
const SecurityPage = lazy(() => import('./pages/SecurityPage').then((m) => ({ default: m.SecurityPage })));
const AnalyticsPage = lazy(() => import('./pages/AnalyticsPage').then((m) => ({ default: m.AnalyticsPage })));

function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, isReady } = useAuth();
  if (!isReady) return null;
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

function RouteFallback() {
  return (
    <div style={{ padding: 'var(--space-6)', color: 'var(--text-muted)' }}>در حالِ بارگیری…</div>
  );
}

function AppRoutes() {
  return (
    <Suspense fallback={<RouteFallback />}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/setup" element={<ProtectedRoute><SetupWizardPage /></ProtectedRoute>} />
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
          <Route path="stock-count" element={<StockCountPage />} />

          <Route path="pos" element={<PosPage />} />
          <Route path="pos/shift" element={<ShiftPage />} />
          <Route path="restaurant" element={<RestaurantHallsPage />} />
          <Route path="restaurant/kitchen" element={<RestaurantKitchenPage />} />
          <Route path="sales" element={<SalesInvoicesPage />} />
          <Route path="sales/new" element={<CreateSalesInvoicePage />} />
          <Route path="sales/return" element={<ReturnFormPage mode="sales" />} />
          <Route path="sales/invoices/:id" element={<SalesInvoiceDetailPage />} />
          <Route path="sales/invoices/:id/edit" element={<CreateSalesInvoicePage />} />
          <Route path="purchase" element={<PurchaseInvoicesPage />} />
          <Route path="purchase/new" element={<CreatePurchaseInvoicePage />} />
          <Route path="purchase/return" element={<ReturnFormPage mode="purchase" />} />
          <Route path="purchase/invoices/:id" element={<PurchaseInvoiceDetailPage />} />
          <Route path="purchase/invoices/:id/edit" element={<CreatePurchaseInvoicePage />} />

          <Route path="treasury" element={<TreasuryPage />} />
          <Route path="cheques" element={<ChequesPage />} />

          <Route path="vouchers" element={<VouchersPage />} />
          <Route path="vouchers/new" element={<CreateVoucherPage />} />
          <Route path="trial-balance" element={<TrialBalancePage />} />
          <Route path="general-ledger" element={<GeneralLedgerPage />} />
          <Route path="balance-sheet" element={<BalanceSheetPage />} />
          <Route path="income-statement" element={<IncomeStatementPage />} />
          <Route path="branch-summary" element={<BranchSummaryPage />} />
          <Route path="accounts" element={<AccountsPage />} />
          <Route path="reports-center" element={<ReportsCenterPage />} />
          <Route path="analytics" element={<AnalyticsPage />} />

          <Route path="branches" element={<BranchesPage />} />
          <Route path="security" element={<SecurityPage />} />
          <Route path="modules" element={<ModulesPage />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="migration" element={<MigrationPage />} />
          <Route path="templates" element={<TemplatesPage />} />
          <Route path="tax-invoicing" element={<TaxInvoicingPage />} />
          <Route path="tourism" element={<TourismPage />} />

          <Route path="hr/employees" element={<EmployeesPage />} />
          <Route path="hr/employees/new" element={<EmployeeEditPage />} />
          <Route path="hr/employees/:id/edit" element={<EmployeeEditPage />} />
          <Route path="hr/payroll" element={<PayrollPage />} />
          <Route path="hotel" element={<HotelPage />} />
          <Route path="attendance" element={<AttendancePage />} />
          <Route path="contracting" element={<ContractingPage />} />
          <Route path="support" element={<SupportPage />} />
        </Route>
      </Routes>
    </Suspense>
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
