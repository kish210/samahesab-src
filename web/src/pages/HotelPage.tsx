import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { DataTable, type Column } from '../components/DataTable';
import { SearchSelect, type SearchSelectOption } from '../components/SearchSelect';
import { JalaliDateInput } from '../components/JalaliDateInput';
import { todayJalaliString } from '../lib/jalali';
import { money } from '../lib/format';

interface RoomTypeDto { id: number; code: string; name: string; baseCapacity: number; extraBedAllowed: boolean; active: boolean }
interface RoomDto { id: number; roomTypeId: number; roomTypeName: string; number: string; floor: string | null; status: number; active: boolean }
interface ReservationRoomDto { id: number; roomTypeId: number; roomTypeName: string; roomId: number | null; roomNumber: string | null; ratePerNight: number; extraBeds: number }
interface ReservationDto {
  id: number; guestName: string; checkInDate: string; checkOutDate: string; nights: number;
  adults: number; children: number; status: number; source: number; notes: string | null; rooms: ReservationRoomDto[];
}
interface FolioChargeDto { id: number; type: number; amount: number; description: string; date: string }
interface FolioPaymentDto { id: number; method: number; amount: number; description: string; date: string }
interface FolioDto {
  id: number; reservationId: number; openDate: string; closeDate: string | null; status: number;
  totalCharges: number; totalPayments: number; appliedDeposit: number; balance: number;
  charges: FolioChargeDto[]; payments: FolioPaymentDto[];
}
interface CustomerRow { id: number; name: string }

const ROOM_STATUS = ['خالیِ تمیز', 'خالیِ کثیف', 'اشغالِ تمیز', 'اشغالِ کثیف', 'بازرسی‌شده', 'خارجِ سرویس', 'مسدود'];
const ROOM_STATUS_BADGE = ['badge-green', 'badge-yellow', 'badge-blue', 'badge-red', 'badge-green', 'badge-gray', 'badge-gray'];
const RES_STATUS = ['هولد', 'تأییدشده', 'تضمین‌شده', 'ورودزده', 'خروجزده', 'لغوشده', 'عدمِ حضور'];
const CHARGE_TYPES = ['اتاق', 'عوارضِ اتاق', 'تختِ اضافه', 'رستوران', 'مینی‌بار', 'لباسشویی', 'تلفن', 'خسارت', 'متفرقه', 'تخفیف'];
const PAYMENT_METHODS = ['نقد', 'کارت', 'انتقال', 'چک', 'صورتحسابِ آژانس'];

/** U-WEB-HOTEL — ماژولِ هتل/اقامتگاه (PMS). ماژول قبلاً فقط Domain داشت؛ CQRS+API+این صفحه نو اضافه شدند.
 * ⚠️ محدودیتِ صادقانه: نرخ‌نامه/ودیعه/هاوس‌کیپینگ/تعمیرات/شب‌حسابرسی هنوز UI ندارند (خارج از حدودِ این پورت). */
export function HotelPage() {
  const [roomTypes, setRoomTypes] = useState<RoomTypeDto[]>([]);
  const [rooms, setRooms] = useState<RoomDto[]>([]);
  const [reservations, setReservations] = useState<ReservationDto[]>([]);
  const [customers, setCustomers] = useState<CustomerRow[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [folio, setFolio] = useState<FolioDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [tab, setTab] = useState<'rooms' | 'reservations'>('reservations');
  const [showNewRes, setShowNewRes] = useState(false);
  const [showNewRoomType, setShowNewRoomType] = useState(false);
  const [showNewRoom, setShowNewRoom] = useState(false);

  function loadAll() {
    apiGet<RoomTypeDto[]>('/api/hotel/room-types').then(setRoomTypes).catch(() => {});
    apiGet<RoomDto[]>('/api/hotel/rooms').then(setRooms).catch(() => {});
    apiGet<ReservationDto[]>('/api/hotel/reservations').then(setReservations)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ رزروها.'));
    apiGet<CustomerRow[]>('/api/customers').then(setCustomers).catch(() => {});
  }
  useEffect(loadAll, []);

  useEffect(() => {
    if (selectedId == null) { setFolio(null); return; }
    apiGet<FolioDto>(`/api/hotel/reservations/${selectedId}/folio`).then(setFolio).catch(() => setFolio(null));
  }, [selectedId]);

  const selected = reservations.find((r) => r.id === selectedId) ?? null;
  const guestOptions: SearchSelectOption[] = customers.map((c) => ({ id: c.id, label: c.name }));

  // ── فرمِ رزروِ نو ──
  const [guestId, setGuestId] = useState<number | null>(null);
  const [checkIn, setCheckIn] = useState(todayJalaliString());
  const [checkOut, setCheckOut] = useState(todayJalaliString());
  const [nights, setNights] = useState('1');
  const [adults, setAdults] = useState('1');
  const [children, setChildren] = useState('0');
  const [resLines, setResLines] = useState<{ roomTypeId: number; rate: string; extraBeds: string }[]>([]);

  function addResLine() {
    if (roomTypes.length === 0) return;
    setResLines((prev) => [...prev, { roomTypeId: roomTypes[0].id, rate: '0', extraBeds: '0' }]);
  }

  async function submitReservation() {
    setError(null);
    if (!guestId) { setError('انتخابِ مهمان الزامی است.'); return; }
    if (resLines.length === 0) { setError('دستِ‌کم یک اتاق اضافه کنید.'); return; }
    try {
      await apiPost('/api/hotel/reservations', {
        guestPartyId: guestId, source: 0, checkInDate: checkIn, checkOutDate: checkOut, nights: Number(nights) || 1,
        adults: Number(adults) || 1, children: Number(children) || 0,
        rooms: resLines.map((l) => ({ roomTypeId: l.roomTypeId, ratePerNight: Number(l.rate) || 0, extraBeds: Number(l.extraBeds) || 0 })),
      });
      setShowNewRes(false); setGuestId(null); setResLines([]);
      setNotice('رزرو ثبت شد.');
      loadAll();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ رزرو ناموفق بود.'); }
  }

  async function checkIn_(res: ReservationDto) {
    // تخصیصِ ساده: اولین اتاقِ خالیِ همان نوع برایِ هر خط.
    const assignments: { reservationRoomId: number; roomId: number }[] = [];
    const usedRoomIds = new Set<number>();
    for (const line of res.rooms) {
      const free = rooms.find((r) => r.roomTypeId === line.roomTypeId && r.status === 0 && r.active && !usedRoomIds.has(r.id));
      if (!free) { setError(`اتاقِ خالیِ نوعِ «${line.roomTypeName}» یافت نشد.`); return; }
      usedRoomIds.add(free.id);
      assignments.push({ reservationRoomId: line.id, roomId: free.id });
    }
    try {
      await apiPost(`/api/hotel/reservations/${res.id}/check-in`, { assignments, date: todayJalaliString() });
      setNotice('ورودِ مهمان ثبت شد.');
      loadAll();
      if (selectedId === res.id) {
        apiGet<FolioDto>(`/api/hotel/reservations/${res.id}/folio`).then(setFolio).catch(() => setFolio(null));
      }
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ ورود ناموفق بود.'); }
  }

  async function checkOut_(res: ReservationDto) {
    if (!window.confirm('خروجِ مهمان ثبت شود؟')) return;
    try {
      await apiPost(`/api/hotel/reservations/${res.id}/check-out`, { date: todayJalaliString() });
      setNotice('خروجِ مهمان ثبت شد.');
      loadAll();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ خروج ناموفق بود.'); }
  }

  async function cancelRes(res: ReservationDto) {
    if (!window.confirm('رزرو لغو شود؟')) return;
    try {
      await apiPost(`/api/hotel/reservations/${res.id}/cancel`);
      setNotice('رزرو لغو شد.');
      loadAll();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'لغوِ رزرو ناموفق بود.'); }
  }

  // ── فولیو: افزودنِ شارژ/پرداخت ──
  const [chargeType, setChargeType] = useState('0');
  const [chargeAmount, setChargeAmount] = useState('0');
  const [chargeDesc, setChargeDesc] = useState('');
  const [paymentMethod, setPaymentMethod] = useState('0');
  const [paymentAmount, setPaymentAmount] = useState('0');

  async function addCharge() {
    if (!folio) return;
    try {
      await apiPost(`/api/hotel/folios/${folio.id}/charges`, {
        type: Number(chargeType), amount: Number(chargeAmount) || 0, description: chargeDesc || 'شارژ', date: todayJalaliString(),
      });
      setChargeAmount('0'); setChargeDesc('');
      apiGet<FolioDto>(`/api/hotel/reservations/${selectedId}/folio`).then(setFolio);
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ شارژ ناموفق بود.'); }
  }

  async function addPayment() {
    if (!folio) return;
    try {
      await apiPost(`/api/hotel/folios/${folio.id}/payments`, {
        method: Number(paymentMethod), amount: Number(paymentAmount) || 0, description: 'پرداخت', date: todayJalaliString(),
      });
      setPaymentAmount('0');
      apiGet<FolioDto>(`/api/hotel/reservations/${selectedId}/folio`).then(setFolio);
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ پرداخت ناموفق بود.'); }
  }

  const roomColumns: Column<RoomDto>[] = [
    { key: 'number', header: 'شماره', render: (r) => r.number },
    { key: 'floor', header: 'طبقه', render: (r) => r.floor ?? '—' },
    { key: 'type', header: 'نوعِ اتاق', render: (r) => r.roomTypeName },
    { key: 'status', header: 'وضعیت', render: (r) => <span className={`badge ${ROOM_STATUS_BADGE[r.status]}`}>{ROOM_STATUS[r.status]}</span> },
    { key: 'active', header: '', render: (r) => (r.active ? '' : <span className="badge badge-gray">غیرفعال</span>) },
  ];

  const resColumns: Column<ReservationDto>[] = [
    { key: 'guest', header: 'مهمان', render: (r) => r.guestName },
    { key: 'in', header: 'ورود', render: (r) => r.checkInDate },
    { key: 'out', header: 'خروج', render: (r) => r.checkOutDate },
    { key: 'nights', header: 'شب', numeric: true, render: (r) => r.nights },
    { key: 'rooms', header: 'اتاق‌ها', render: (r) => r.rooms.map((x) => x.roomNumber ?? x.roomTypeName).join('، ') },
    { key: 'status', header: 'وضعیت', render: (r) => <span className="badge badge-blue">{RES_STATUS[r.status]}</span> },
    {
      key: 'action', header: '',
      render: (r) => (
        <div style={{ display: 'flex', gap: 6 }}>
          {r.status <= 2 && <button type="button" className="btn btn-ghost btn-sm" onClick={() => checkIn_(r)}>ورود</button>}
          {r.status === 3 && <button type="button" className="btn btn-ghost btn-sm" onClick={() => checkOut_(r)}>خروج</button>}
          {r.status <= 2 && <button type="button" className="btn btn-ghost btn-sm" onClick={() => cancelRes(r)}>لغو</button>}
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title="هتل / اقامتگاه (PMS)" />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {notice && <StatusMessage kind="success">{notice}</StatusMessage>}

      <div className="minitabs" style={{ marginBottom: 'var(--space-4)' }}>
        <button type="button" className={tab === 'reservations' ? 'on' : ''} onClick={() => setTab('reservations')}>رزروها</button>
        <button type="button" className={tab === 'rooms' ? 'on' : ''} onClick={() => setTab('rooms')}>تابلویِ اتاق‌ها</button>
      </div>

      {tab === 'rooms' && (
        <div>
          <div style={{ display: 'flex', gap: 'var(--space-2)', marginBottom: 'var(--space-3)' }}>
            <button type="button" className="btn btn-secondary btn-sm" onClick={() => setShowNewRoomType((v) => !v)}>نوعِ اتاقِ نو</button>
            <button type="button" className="btn btn-secondary btn-sm" onClick={() => setShowNewRoom((v) => !v)}>اتاقِ نو</button>
          </div>
          {showNewRoomType && <NewRoomTypeForm onSaved={() => { setShowNewRoomType(false); loadAll(); }} />}
          {showNewRoom && <NewRoomForm roomTypes={roomTypes} onSaved={() => { setShowNewRoom(false); loadAll(); }} />}
          <DataTable columns={roomColumns} rows={rooms} rowKey={(r) => r.id} emptyText="اتاقی تعریف نشده." />
        </div>
      )}

      {tab === 'reservations' && (
        <div style={{ display: 'flex', gap: 'var(--space-4)' }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ marginBottom: 'var(--space-3)' }}>
              <button type="button" className="btn btn-primary btn-sm" onClick={() => setShowNewRes((v) => !v)}>رزروِ نو</button>
            </div>
            {showNewRes && (
              <div className="gbox" style={{ padding: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
                <div className="gh">رزروِ نو</div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 'var(--space-3)', marginTop: 'var(--space-2)' }}>
                  <div className="field" style={{ gridColumn: 'span 2' }}>
                    <label className="label">مهمان</label>
                    <SearchSelect options={guestOptions} value={guestId} onChange={setGuestId} placeholder="جست‌وجویِ مهمان…" />
                  </div>
                  <JalaliDateInput value={checkIn} onChange={setCheckIn} label="تاریخِ ورود" />
                  <JalaliDateInput value={checkOut} onChange={setCheckOut} label="تاریخِ خروج" />
                  <div className="field">
                    <label className="label">تعدادِ شب</label>
                    <input className="input" type="number" min="1" value={nights} onChange={(e) => setNights(e.target.value)} />
                  </div>
                  <div className="field">
                    <label className="label">بزرگسال</label>
                    <input className="input" type="number" min="1" value={adults} onChange={(e) => setAdults(e.target.value)} />
                  </div>
                  <div className="field">
                    <label className="label">کودک</label>
                    <input className="input" type="number" min="0" value={children} onChange={(e) => setChildren(e.target.value)} />
                  </div>
                </div>

                <div className="gh" style={{ marginTop: 'var(--space-3)' }}>اتاق‌ها</div>
                {resLines.map((line, i) => (
                  <div key={i} style={{ display: 'flex', gap: 'var(--space-2)', marginTop: 6, alignItems: 'center' }}>
                    <select className="select" value={line.roomTypeId}
                      onChange={(e) => setResLines((prev) => prev.map((l, idx) => idx === i ? { ...l, roomTypeId: Number(e.target.value) } : l))}>
                      {roomTypes.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
                    </select>
                    <input className="input" type="number" placeholder="نرخِ هرشب" value={line.rate}
                      onChange={(e) => setResLines((prev) => prev.map((l, idx) => idx === i ? { ...l, rate: e.target.value } : l))} />
                    <input className="input" type="number" placeholder="تختِ اضافه" style={{ maxWidth: 110 }} value={line.extraBeds}
                      onChange={(e) => setResLines((prev) => prev.map((l, idx) => idx === i ? { ...l, extraBeds: e.target.value } : l))} />
                    <button type="button" className="btn btn-ghost btn-sm" onClick={() => setResLines((prev) => prev.filter((_, idx) => idx !== i))}>حذف</button>
                  </div>
                ))}
                <button type="button" className="btn btn-ghost btn-sm" style={{ marginTop: 6 }} onClick={addResLine}>+ افزودنِ اتاق</button>

                <div style={{ marginTop: 'var(--space-3)' }}>
                  <button type="button" className="btn btn-primary btn-sm" onClick={submitReservation}>ثبتِ رزرو</button>
                </div>
              </div>
            )}
            <DataTable columns={resColumns} rows={reservations} rowKey={(r) => r.id}
              selectedKey={selectedId} onRowClick={(r) => setSelectedId(r.id)} emptyText="رزروی ثبت نشده." />
          </div>

          {selected && (
            <div style={{ width: 360, flexShrink: 0 }}>
              <div className="gbox" style={{ padding: 'var(--space-4)' }}>
                <div className="gh">فولیوِ رزروِ #{selected.id}</div>
                {!folio && <StatusMessage kind="muted">هنوز ورود ثبت نشده — فولیو ندارد.</StatusMessage>}
                {folio && (
                  <div>
                    <div className="sumbar" style={{ marginTop: 'var(--space-2)' }}>
                      <span>شارژها: {money(folio.totalCharges)}</span>
                      <span>پرداخت‌ها: {money(folio.totalPayments)}</span>
                      <span>مانده: <b>{money(folio.balance)}</b></span>
                    </div>

                    <div className="gh" style={{ marginTop: 'var(--space-3)' }}>افزودنِ شارژ</div>
                    <div style={{ display: 'flex', gap: 6, marginTop: 4 }}>
                      <select className="select" value={chargeType} onChange={(e) => setChargeType(e.target.value)}>
                        {CHARGE_TYPES.map((t, i) => <option key={i} value={i}>{t}</option>)}
                      </select>
                      <input className="input" type="number" value={chargeAmount} onChange={(e) => setChargeAmount(e.target.value)} />
                    </div>
                    <input className="input" placeholder="توضیح" style={{ marginTop: 4 }} value={chargeDesc} onChange={(e) => setChargeDesc(e.target.value)} />
                    <button type="button" className="btn btn-secondary btn-sm" style={{ marginTop: 6 }} onClick={addCharge}>ثبتِ شارژ</button>

                    <div className="gh" style={{ marginTop: 'var(--space-3)' }}>افزودنِ پرداخت</div>
                    <div style={{ display: 'flex', gap: 6, marginTop: 4 }}>
                      <select className="select" value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)}>
                        {PAYMENT_METHODS.map((m, i) => <option key={i} value={i}>{m}</option>)}
                      </select>
                      <input className="input" type="number" value={paymentAmount} onChange={(e) => setPaymentAmount(e.target.value)} />
                    </div>
                    <button type="button" className="btn btn-secondary btn-sm" style={{ marginTop: 6 }} onClick={addPayment}>ثبتِ پرداخت</button>

                    <div className="gh" style={{ marginTop: 'var(--space-3)' }}>ریزِ شارژها/پرداخت‌ها</div>
                    {folio.charges.map((c) => (
                      <div key={`c${c.id}`} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 'var(--text-sm)' }}>
                        <span>{CHARGE_TYPES[c.type]} — {c.description}</span><span>{money(c.amount)}</span>
                      </div>
                    ))}
                    {folio.payments.map((p) => (
                      <div key={`p${p.id}`} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 'var(--text-sm)', color: 'var(--success-700)' }}>
                        <span>{PAYMENT_METHODS[p.method]}</span><span>{money(p.amount)}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function NewRoomTypeForm({ onSaved }: { onSaved: () => void }) {
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [capacity, setCapacity] = useState('2');
  const [error, setError] = useState<string | null>(null);

  async function save() {
    try {
      await apiPost('/api/hotel/room-types', { id: 0, code, name, baseCapacity: Number(capacity) || 2, extraBedAllowed: false, active: true });
      onSaved();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ذخیره ناموفق بود.'); }
  }

  return (
    <div className="gbox" style={{ padding: 'var(--space-3)', marginBottom: 'var(--space-3)', display: 'flex', gap: 6, alignItems: 'flex-end' }}>
      <input className="input" placeholder="کد" value={code} onChange={(e) => setCode(e.target.value)} />
      <input className="input" placeholder="نام" value={name} onChange={(e) => setName(e.target.value)} />
      <input className="input" type="number" placeholder="ظرفیت" style={{ maxWidth: 100 }} value={capacity} onChange={(e) => setCapacity(e.target.value)} />
      <button type="button" className="btn btn-primary btn-sm" onClick={save}>ذخیره</button>
      {error && <span style={{ color: 'var(--danger-700)' }}>{error}</span>}
    </div>
  );
}

function NewRoomForm({ roomTypes, onSaved }: { roomTypes: RoomTypeDto[]; onSaved: () => void }) {
  const [roomTypeId, setRoomTypeId] = useState(roomTypes[0]?.id ?? 0);
  const [number, setNumber] = useState('');
  const [floor, setFloor] = useState('');
  const [error, setError] = useState<string | null>(null);

  async function save() {
    try {
      await apiPost('/api/hotel/rooms', { id: 0, roomTypeId, number, floor: floor || null, active: true });
      onSaved();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ذخیره ناموفق بود.'); }
  }

  return (
    <div className="gbox" style={{ padding: 'var(--space-3)', marginBottom: 'var(--space-3)', display: 'flex', gap: 6, alignItems: 'flex-end' }}>
      <select className="select" value={roomTypeId} onChange={(e) => setRoomTypeId(Number(e.target.value))}>
        {roomTypes.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
      </select>
      <input className="input" placeholder="شمارهٔ اتاق" value={number} onChange={(e) => setNumber(e.target.value)} />
      <input className="input" placeholder="طبقه" value={floor} onChange={(e) => setFloor(e.target.value)} />
      <button type="button" className="btn btn-primary btn-sm" onClick={save}>ذخیره</button>
      {error && <span style={{ color: 'var(--danger-700)' }}>{error}</span>}
    </div>
  );
}
