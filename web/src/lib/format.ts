export const numberFormat = new Intl.NumberFormat('fa-IR');

export function money(n: number): string {
  return numberFormat.format(Math.round(n));
}
