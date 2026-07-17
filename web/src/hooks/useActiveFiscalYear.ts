import { useEffect, useState } from 'react';
import { apiGet } from '../api/client';

interface FiscalYearDto {
  id: number;
  title: string;
  startDate: string;
  endDate: string;
  isClosed: boolean;
  isActive: boolean;
}

/** سالِ مالیِ فعالِ شرکت — از `/api/accounting/dimensions/fiscal-years` (هم‌الگو با POSِ سرور،
 * به‌جایِ هاردکدِ ۱ در کلاینت). تا لود شدن null است؛ در نبودِ سالِ فعال، ۱ (سازگاریِ عقب‌رو). */
export function useActiveFiscalYear(): number | null {
  const [id, setId] = useState<number | null>(null);

  useEffect(() => {
    apiGet<FiscalYearDto[]>('/api/accounting/dimensions/fiscal-years')
      .then((years) => setId(years.find((y) => y.isActive)?.id ?? 1))
      .catch(() => setId(1));
  }, []);

  return id;
}
