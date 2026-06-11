import React from 'react';

const TONE = {
  neutral: 'badge-gray', blue: 'badge-blue', green: 'badge-green',
  amber: 'badge-amber', red: 'badge-red', gold: 'badge-gold', solid: 'badge-solid',
};

/** Compact status pill. Use a dot for live/state semantics. */
export function Badge({ tone = 'neutral', dot = false, className = '', children, ...rest }) {
  return (
    <span className={['badge', TONE[tone] || TONE.neutral, className].filter(Boolean).join(' ')} {...rest}>
      {dot && <span className="dot" />}
      {children}
    </span>
  );
}

export default Badge;
