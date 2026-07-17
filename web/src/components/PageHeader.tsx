import type { ReactNode } from 'react';

export function PageHeader({ title, actions }: { title: string; actions?: ReactNode }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 'var(--space-4)' }}>
      <h1>{title}</h1>
      {actions && <div style={{ display: 'flex', gap: 'var(--space-2)' }}>{actions}</div>}
    </div>
  );
}

export function StatusMessage({ kind, children }: { kind: 'error' | 'muted' | 'success'; children: ReactNode }) {
  const color = kind === 'error' ? 'var(--danger-700)' : kind === 'success' ? 'var(--success-700)' : 'var(--text-muted)';
  return <div style={{ color }}>{children}</div>;
}
