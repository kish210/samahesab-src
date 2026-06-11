import * as React from 'react';

export type BadgeTone = 'neutral' | 'blue' | 'green' | 'amber' | 'red' | 'gold' | 'solid';

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  /** Semantic color. Map: green=success, amber=pending, red=error/overdue. */
  tone?: BadgeTone;
  /** Show a leading status dot. */
  dot?: boolean;
  children?: React.ReactNode;
}
/** Compact status pill. */
export function Badge(props: BadgeProps): React.ReactElement;
export default Badge;
