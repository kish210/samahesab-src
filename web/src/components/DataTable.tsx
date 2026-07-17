import type { ReactNode } from 'react';

export interface Column<T> {
  key: string;
  header: string;
  align?: 'start' | 'end';
  numeric?: boolean;
  render: (row: T) => ReactNode;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T, index: number) => string | number;
  emptyText?: string;
}

/** جدولِ عمومیِ سبک — برایِ فهرست‌هایِ ساده (بدونِ صفحه‌بندی/مرتب‌سازیِ سمتِ کلاینت). */
export function DataTable<T>({ columns, rows, rowKey, emptyText = 'رکوردی یافت نشد.' }: DataTableProps<T>) {
  return (
    <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', overflow: 'auto' }}>
      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          <tr style={{ background: 'var(--gray-50)', borderBottom: '1px solid var(--border)' }}>
            {columns.map((c) => (
              <th
                key={c.key}
                className={c.numeric ? 'num' : undefined}
                style={{ padding: '10px 12px', textAlign: c.align === 'end' || c.numeric ? 'end' : 'start', fontSize: 'var(--text-sm)', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}
              >
                {c.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, i) => (
            <tr key={rowKey(row, i)} style={{ borderBottom: '1px solid var(--gray-100)' }}>
              {columns.map((c) => (
                <td
                  key={c.key}
                  className={c.numeric ? 'num' : undefined}
                  style={{ padding: '10px 12px', textAlign: c.align === 'end' || c.numeric ? 'end' : 'start' }}
                >
                  {c.render(row)}
                </td>
              ))}
            </tr>
          ))}
          {rows.length === 0 && (
            <tr>
              <td colSpan={columns.length} style={{ padding: 'var(--space-6)', textAlign: 'center', color: 'var(--text-muted)' }}>
                {emptyText}
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
