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

/** جدولِ عمومیِ فهرست‌ها — پورتِ کلاس‌هایِ `.dgrid`/`.dgrid-wrap`ِ design-system
 * (سرستونِ چسبان، ردیف‌هایِ زوج/فرد، هاورِ آبی، اعدادِ tabular) به‌جایِ استایلِ inlineِ عمومیِ SaaS. */
export function DataTable<T>({ columns, rows, rowKey, emptyText = 'رکوردی یافت نشد.' }: DataTableProps<T>) {
  return (
    <div className="dgrid-wrap">
      <table className="dgrid">
        <thead>
          <tr>
            {columns.map((c) => (
              <th key={c.key} className={c.numeric || c.align === 'end' ? 'num' : undefined}>
                {c.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, i) => (
            <tr key={rowKey(row, i)}>
              {columns.map((c) => (
                <td key={c.key} className={c.numeric || c.align === 'end' ? 'num' : undefined}>
                  {c.render(row)}
                </td>
              ))}
            </tr>
          ))}
          {rows.length === 0 && (
            <tr>
              <td colSpan={columns.length} style={{ height: 'auto', padding: 'var(--space-6)', textAlign: 'center', color: 'var(--text-muted)', whiteSpace: 'normal' }}>
                {emptyText}
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
