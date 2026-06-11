import * as React from 'react';

export interface InputProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'size'> {
  label?: React.ReactNode;
  hint?: React.ReactNode;
  /** Error message — also flips the field to the error style. */
  error?: React.ReactNode;
  required?: boolean;
  /** Icon rendered inside the input, leading edge. */
  leadingIcon?: React.ReactNode;
  size?: 'sm' | 'md';
}
/** Labelled text input. */
export function Input(props: InputProps): React.ReactElement;

export interface SelectProps extends Omit<React.SelectHTMLAttributes<HTMLSelectElement>, 'size'> {
  label?: React.ReactNode;
  hint?: React.ReactNode;
  required?: boolean;
  size?: 'sm' | 'md';
  children?: React.ReactNode;
}
/** Styled native select. */
export function Select(props: SelectProps): React.ReactElement;

export default Input;
