import * as React from 'react';

export interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
  /** Optional header title. */
  title?: React.ReactNode;
  /** Header action node(s), right/left aligned per direction. */
  actions?: React.ReactNode;
  /** Apply default body padding. */
  padded?: boolean;
  children?: React.ReactNode;
}
/** Elevated surface container. */
export function Card(props: CardProps): React.ReactElement;
export default Card;
