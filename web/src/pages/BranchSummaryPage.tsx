import { useEffect, useState } from 'react';
import { apiGet, ApiError } from '../api/client';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface BranchSummaryRow {
  branchId: number | null;
  branchName: string;
  customerCount: number;
  supplierCount: number;
  productCount: number;
  warehouseCount: number;
  employeeCount: number;
}

const columns: Column<BranchSummaryRow>[] = [
  { key: 'name', header: 'شعبه', render: (r) => r.branchName },
  { key: 'customers', header: 'مشتری', numeric: true, render: (r) => r.customerCount },
  { key: 'suppliers', header: 'تأمین‌کننده', numeric: true, render: (r) => r.supplierCount },
  { key: 'products', header: 'کالا', numeric: true, render: (r) => r.productCount },
  { key: 'warehouses', header: 'انبار', numeric: true, render: (r) => r.warehouseCount },
  { key: 'employees', header: 'کارمند', numeric: true, render: (r) => r.employeeCount },
];

/** گزارشِ per-branch (U-BRANCH-BASEDATA) — تعدادِ دادهٔ پایهٔ اختصاصیِ هر شعبه، رویِ
 * GetBranchSummaryQueryِ ازقبل‌موجود که تا این‌جا هیچ UIای صدایش نمی‌زد. */
export function BranchSummaryPage() {
  const [rows, setRows] = useState<BranchSummaryRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiGet<BranchSummaryRow[]>('/api/reports/branch-summary')
      .then(setRows)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ گزارشِ شعب.'));
  }, []);

  return (
    <div>
      <PageHeader title="خلاصهٔ شعب" />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {!rows && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {rows && !error && (
        <DataTable columns={columns} rows={rows} rowKey={(r) => r.branchId ?? -1} emptyText="شعبه‌ای ثبت نشده." />
      )}
    </div>
  );
}
