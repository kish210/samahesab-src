/** آیکون‌هایِ خطی — عیناً کپی از مسیرهایِ SVGِ design-system/screens/erp-shell.js برایِ همسانیِ بصری. */
type IconName =
  | 'home' | 'accounting' | 'sales' | 'purchasing' | 'inventory' | 'treasury'
  | 'cheque' | 'pos' | 'people' | 'reports' | 'settings' | 'search' | 'bell'
  | 'modules' | 'restaurant' | 'calc';

const PATHS: Record<IconName, string> = {
  home: '<path d="M3 10.5 12 3l9 7.5"/><path d="M5 9.5V21h14V9.5"/>',
  accounting: '<path d="M5 4h11l3 3v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1z"/><path d="M8 9h7M8 13h7M8 17h4"/>',
  sales: '<circle cx="9" cy="20" r="1.4"/><circle cx="18" cy="20" r="1.4"/><path d="M2 3h3l2.4 12.3a1 1 0 0 0 1 .8h8.7a1 1 0 0 0 1-.78L21 7H6"/>',
  purchasing: '<path d="M3 6h11v9H3z"/><path d="M14 9h4l3 3v3h-7z"/><circle cx="7" cy="18" r="1.6"/><circle cx="17" cy="18" r="1.6"/>',
  inventory: '<path d="M3.3 7.3 12 3l8.7 4.3v9.4L12 21l-8.7-4.3z"/><path d="M3.3 7.3 12 11.6l8.7-4.3M12 21v-9.4"/>',
  treasury: '<rect x="2.5" y="6" width="19" height="12" rx="2"/><circle cx="12" cy="12" r="2.6"/><path d="M6 9.5v5M18 9.5v5"/>',
  cheque: '<rect x="2.5" y="5" width="19" height="14" rx="2"/><path d="M6 9h6M6 12.5h4"/><path d="m14 13.5 2 2 4-4"/>',
  pos: '<rect x="3" y="3" width="18" height="13" rx="1.6"/><path d="M8 20h8M12 16v4"/>',
  people: '<circle cx="9" cy="8" r="3.2"/><path d="M3.5 20a5.5 5.5 0 0 1 11 0"/><path d="M16 6.2a3 3 0 0 1 0 5.6M21 20a5 5 0 0 0-3.5-4.8"/>',
  reports: '<path d="M4 20V4"/><path d="M4 20h16"/><rect x="7" y="11" width="3" height="6"/><rect x="12" y="7" width="3" height="10"/><rect x="17" y="13" width="3" height="4"/>',
  settings: '<circle cx="12" cy="12" r="3"/><path d="M12 2.5v2.2M12 19.3v2.2M21.5 12h-2.2M4.7 12H2.5M18.4 5.6l-1.6 1.6M7.2 16.8l-1.6 1.6M18.4 18.4l-1.6-1.6M7.2 7.2 5.6 5.6"/>',
  search: '<circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/>',
  bell: '<path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/>',
  modules: '<rect x="3" y="3" width="7" height="7" rx="1.2"/><rect x="14" y="3" width="7" height="7" rx="1.2"/><rect x="3" y="14" width="7" height="7" rx="1.2"/><rect x="14" y="14" width="7" height="7" rx="1.2"/>',
  restaurant: '<path d="M5 3v7a3 3 0 0 0 6 0V3M8 3v18M19 3c-1.5 1-2.5 3-2.5 6 0 2 1 3 2.5 3v9"/>',
  calc: '<rect x="5" y="2.5" width="14" height="19" rx="2"/><path d="M8.5 6h7"/><path d="M8.5 11h.01M12 11h.01M15.5 11h.01M8.5 14.5h.01M12 14.5h.01M15.5 14.5h.01M8.5 18h.01M12 18h.01M15.5 18h.01"/>',
};

export function ErpIcon({ name }: { name: IconName }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.7} strokeLinecap="round" strokeLinejoin="round"
      dangerouslySetInnerHTML={{ __html: PATHS[name] }} />
  );
}

export type { IconName };
