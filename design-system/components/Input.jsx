import React from 'react';

/** Labelled text input with optional leading icon, hint and error state. */
export function Input({
  label, hint, error, required = false, leadingIcon,
  size = 'md', className = '', id, ...rest
}) {
  const inputId = id || `inp-${Math.random().toString(36).slice(2, 8)}`;
  const input = (
    <input
      id={inputId}
      className={['input', size === 'sm' ? 'input-sm' : '', error ? 'is-error' : '', className].filter(Boolean).join(' ')}
      aria-invalid={error ? true : undefined}
      {...rest}
    />
  );
  return (
    <div className="field">
      {label && (
        <label className="label" htmlFor={inputId}>
          {label}{required && <span className="req">*</span>}
        </label>
      )}
      {leadingIcon ? (
        <div className="input-group">
          <span className="ig-icon">{leadingIcon}</span>
          {input}
        </div>
      ) : input}
      {error ? <span className="hint" style={{ color: 'var(--danger-500)' }}>{error}</span>
        : hint ? <span className="hint">{hint}</span> : null}
    </div>
  );
}

/** Native select styled to match the system. */
export function Select({ label, hint, required = false, size = 'md', className = '', id, children, ...rest }) {
  const selId = id || `sel-${Math.random().toString(36).slice(2, 8)}`;
  return (
    <div className="field">
      {label && <label className="label" htmlFor={selId}>{label}{required && <span className="req">*</span>}</label>}
      <select id={selId} className={['select', size === 'sm' ? 'select-sm' : '', className].filter(Boolean).join(' ')} {...rest}>
        {children}
      </select>
      {hint && <span className="hint">{hint}</span>}
    </div>
  );
}

export default Input;
