import React from 'react';

/**
 * SamaHesab primary action button.
 * Variants map to the brand system; gold is reserved for rare premium emphasis.
 */
export function Button({
  variant = 'primary',
  size = 'md',
  iconLeading,
  iconTrailing,
  iconOnly = false,
  disabled = false,
  className = '',
  children,
  ...rest
}) {
  const cls = [
    'btn',
    `btn-${variant}`,
    size === 'sm' ? 'btn-sm' : size === 'lg' ? 'btn-lg' : '',
    iconOnly ? 'btn-icon' : '',
    className,
  ].filter(Boolean).join(' ');

  return (
    <button className={cls} disabled={disabled} aria-disabled={disabled || undefined} {...rest}>
      {iconLeading}
      {!iconOnly && children}
      {iconOnly && children}
      {iconTrailing}
    </button>
  );
}

export default Button;
