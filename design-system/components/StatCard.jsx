import React from 'react';

/** KPI tile for dashboards: label, large value, optional delta + sparkline slot. */
export function StatCard({ label, value, unit, delta, deltaTone = 'auto', icon, footer, accent = false, className = '', ...rest }) {
  let tone = deltaTone;
  if (delta != null && deltaTone === 'auto') {
    const up = String(delta).trim().startsWith('+') || (typeof delta === 'number' && delta >= 0);
    tone = up ? 'green' : 'red';
  }
  const deltaColor = tone === 'green' ? 'var(--success-500)' : tone === 'red' ? 'var(--danger-500)' : 'var(--text-muted)';
  return (
    <div className={['card', 'card-pad', className].filter(Boolean).join(' ')} style={accent ? { borderTop: '3px solid var(--gold-500)' } : undefined} {...rest}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
        <span style={{ fontSize: 'var(--text-sm)', color: 'var(--text-muted)', fontWeight: 500 }}>{label}</span>
        {icon && <span style={{ color: 'var(--blue-500)', display: 'inline-flex' }}>{icon}</span>}
      </div>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: 6 }}>
        <span style={{ fontSize: 'var(--text-3xl)', lineHeight: 1, fontWeight: 700, color: 'var(--text-strong)', fontVariantNumeric: 'tabular-nums' }}>{value}</span>
        {unit && <span style={{ fontSize: 'var(--text-base)', color: 'var(--text-muted)', fontWeight: 500 }}>{unit}</span>}
      </div>
      {(delta != null || footer) && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 10 }}>
          {delta != null && <span style={{ fontSize: 'var(--text-sm)', fontWeight: 600, color: deltaColor }}>{delta}</span>}
          {footer && <span style={{ fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>{footer}</span>}
        </div>
      )}
    </div>
  );
}

export default StatCard;
