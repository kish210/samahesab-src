import * as React from 'react';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'gold' | 'danger';
export type ButtonSize = 'sm' | 'md' | 'lg';

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  /** Visual style. `gold` is a rare premium accent — use sparingly. */
  variant?: ButtonVariant;
  /** Control height. Default `md` (34px). */
  size?: ButtonSize;
  /** Icon node placed before the label. */
  iconLeading?: React.ReactNode;
  /** Icon node placed after the label. */
  iconTrailing?: React.ReactNode;
  /** Square icon-only button. */
  iconOnly?: boolean;
  disabled?: boolean;
  className?: string;
  children?: React.ReactNode;
}

/** SamaHesab action button. */
export function Button(props: ButtonProps): React.ReactElement;
export default Button;
