import { Navigate, Route, BrowserRouter, Routes } from 'react-router-dom';
import type { ReactNode } from 'react';
import { AuthProvider, useAuth } from './auth/AuthContext';
import { Shell } from './components/Shell';
import { LoginPage } from './pages/LoginPage';
import { SetupWizardPage } from './pages/SetupWizardPage';
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
import { CreateVoucherPage } from './pages/CreateVoucherPage';
import { TrialBalancePage } from './pages/TrialBalancePage';
import { GeneralLedgerPage } from './pages/GeneralLedgerPage';
import { BalanceSheetPage } from './pages/BalanceSheetPage';
import { IncomeStatementPage } from './pages/IncomeStatementPage';
import { BranchSummaryPage } from './pages/BranchSummaryPage';
import { AccountsPage } from './pages/AccountsPage';
import { SalesInvoicesPage } from './pages/SalesInvoicesPage';
import { SalesInvoiceDetailPage } from './pages/SalesInvoiceDetailPage';
import { CreateSalesInvoicePage } from './pages/CreateSalesInvoicePage';
import { PurchaseInvoicesPage } from './pages/PurchaseInvoicesPage';
import { PurchaseInvoiceDetailPage } from './pages/PurchaseInvoiceDetailPage';
import { CreatePurchaseInvoicePage } from './pages/CreatePurchaseInvoicePage';
import { ModulesPage } from './pages/ModulesPage';
import { SettingsPage } from './pages/SettingsPage';
import { MigrationPage } from './pages/MigrationPage';
import { TaxInvoicingPage } from './pages/TaxInvoicingPage';
import { TourismPage } from './pages/TourismPage';
import { PartyEditPage } from './pages/PartyEditPage';
import { ProductEditPage } from './pages/ProductEditPage';
import { PosPage } from './pages/PosPage';
import { RestaurantHallsPage } from './pages/RestaurantHallsPage';
import { RestaurantKitchenPage } from './pages/RestaurantKitchenPage';
import { ReturnFormPage } from './pages/ReturnFormPage';
import { EmployeesPage } from './pages/EmployeesPage';
import { EmployeeEditPage } from './pages/EmployeeEditPage';
import { PayrollPage } from './pages/PayrollPage';
import { HotelPage } from './pages/HotelPage';
import { AttendancePage } from './pages/AttendancePage';
import { ContractingPage } from './pages/ContractingPage';
import { SupportPage } from './pages/SupportPage';
import { StockCountPage } from './pages/StockCountPage';
import { ReportsCenterPage } from './pages/ReportsCenterPage';
import { BranchesPage } from './pages/BranchesPage';
import { ShiftPage } from './pages/ShiftPage';
import { SecurityPage } from './pages/SecurityPage';
import { AnalyticsPage } from './pages/AnalyticsPage';

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
        <Route path="purchase" element={<PurchaseInvoicesPage />} />
        <Route path="purchase/new" element={<CreatePurchaseInvoicePage />} />
        <Route path="purchase/return" element={<ReturnFormPage mode="purchase" />} />
        <Route path="purchase/invoices/:id" element={<PurchaseInvoiceDetailPage />} />

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
