import * as React from 'react';

export interface StatCardProps extends React.HTMLAttributes<HTMLDivElement> {
  label: React.ReactNode;
  value: React.ReactNode;
  /** Trailing unit, e.g. a currency suffix. */
  unit?: React.ReactNode;
  /** Delta string e.g. "+12.4%". Sign drives auto color. */
  delta?: React.ReactNode;
  /** Force delta color, or 'auto' to derive from sign. */
  deltaTone?: 'auto' | 'green' | 'red' | 'neutral';
  icon?: React.ReactNode;
  footer?: React.ReactNode;
  /** Gold top accent for a highlighted KPI. */
  accent?: boolean;
}
/** Dashboard KPI tile. */
export function StatCard(props: StatCardProps): React.ReactElement;
export default StatCard;
