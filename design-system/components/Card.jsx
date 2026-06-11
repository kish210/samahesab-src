import React from 'react';

/** Surface container. Optional header with title + actions. */
export function Card({ title, actions, padded = false, className = '', children, ...rest }) {
  return (
    <div className={['card', className].filter(Boolean).join(' ')} {...rest}>
      {(title || actions) && (
        <div className="card-head">
          <div className="card-title">{title}</div>
          {actions && <div style={{ display: 'flex', gap: 'var(--space-2)' }}>{actions}</div>}
        </div>
      )}
      {padded ? <div className="card-pad">{children}</div> : children}
    </div>
  );
}

export default Card;
