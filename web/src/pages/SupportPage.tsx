import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { DataTable, type Column } from '../components/DataTable';

interface TicketMessage { author: string; text: string; fromSupport: boolean; sentAt: string }
interface TicketRow {
  id: number; subject: string; body: string; statusText: string; syncText: string;
  createdAt: string; lastActivityAt: string | null; messages: TicketMessage[];
}
interface ArticleRow {
  remoteId: string; title: string; category: number; summary: string | null;
  body: string | null; url: string | null; kind: string; publishedAt: string | null;
}
interface ReleaseRow {
  remoteId: string; version: string; highlights: string | null; bugFixes: string | null;
  knownIssues: string | null; publishedAt: string | null; isCurrent: boolean;
}

const CATEGORIES = ['حسابداری', 'خزانه', 'فروش', 'خرید', 'انبار', 'گزارش‌ها', 'امنیت', 'POS', 'رستوران', 'گردشگری', 'سیستم', 'سایر'];
const SEVERITIES = ['کم', 'متوسط', 'زیاد', 'بحرانی'];

/** U-WEB-SUPPORT — تیکت/گزارشِ باگ/مرکزِ راهنما/یادداشتِ نسخه. Application/Domain/DB از قبل
 * کامل بود؛ فقط SupportController نداشت. ⚠️ محدودیتِ صادقانه: پشتیبانیِ ریموت/تشخیصی نیامده
 * (نیازمندِ نشستِ زندهٔ دسکتاپ‌اند). مرکزِ راهنما/یادداشتِ نسخه در نبودِ کانفیگِ Support در
 * appsettings.json (اتصال به kishwifi.com) خالی می‌مانند — رجوع کن به ConfiguredSupportApiClient.cs. */
export function SupportPage() {
  const [tab, setTab] = useState<'tickets' | 'bug' | 'kb' | 'releases'>('tickets');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  // ── تیکت‌ها ──
  const [tickets, setTickets] = useState<TicketRow[]>([]);
  const [selectedTicket, setSelectedTicket] = useState<TicketRow | null>(null);
  const [showNewTicket, setShowNewTicket] = useState(false);
  const [tSubject, setTSubject] = useState('');
  const [tBody, setTBody] = useState('');
  const [tCategory, setTCategory] = useState(0);
  const [replyText, setReplyText] = useState('');

  function loadTickets() {
    apiGet<TicketRow[]>('/api/support/tickets').then((rows) => {
      setTickets(rows);
      if (selectedTicket) {
        const fresh = rows.find((r) => r.id === selectedTicket.id);
        setSelectedTicket(fresh ?? null);
      }
    }).catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ تیکت‌ها.'));
  }
  useEffect(loadTickets, []);

  async function submitTicket() {
    if (!tSubject.trim() || !tBody.trim()) { setError('موضوع و متنِ تیکت الزامی است.'); return; }
    try {
      await apiPost('/api/support/tickets', { subject: tSubject, body: tBody, category: tCategory });
      setNotice('تیکت ثبت شد.');
      setShowNewTicket(false); setTSubject(''); setTBody('');
      loadTickets();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ تیکت ناموفق بود.'); }
  }

  async function sendReply() {
    if (!selectedTicket || !replyText.trim()) return;
    try {
      await apiPost(`/api/support/tickets/${selectedTicket.id}/messages`, { text: replyText });
      setReplyText('');
      loadTickets();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ارسالِ پیام ناموفق بود.'); }
  }

  // ── گزارشِ باگ ──
  const [bTitle, setBTitle] = useState('');
  const [bDescription, setBDescription] = useState('');
  const [bSeverity, setBSeverity] = useState(1);
  const [bCategory, setBCategory] = useState(0);
  const [bExpected, setBExpected] = useState('');
  const [bActual, setBActual] = useState('');
  const [bSteps, setBSteps] = useState('');

  async function submitBugReport() {
    if (!bTitle.trim() || !bDescription.trim()) { setError('عنوان و شرحِ مشکل الزامی است.'); return; }
    try {
      const r = await apiPost<{ message: string }>('/api/support/bug-reports', {
        title: bTitle, description: bDescription, severity: bSeverity, category: bCategory,
        expectedResult: bExpected || null, actualResult: bActual || null, stepsToReproduce: bSteps || null,
      });
      setNotice(r.message);
      setBTitle(''); setBDescription(''); setBExpected(''); setBActual(''); setBSteps('');
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ گزارش ناموفق بود.'); }
  }

  // ── مرکزِ راهنما ──
  const [articles, setArticles] = useState<ArticleRow[]>([]);
  const [kbSearch, setKbSearch] = useState('');
  function loadArticles() {
    const qs = kbSearch.trim() ? `?search=${encodeURIComponent(kbSearch.trim())}` : '';
    apiGet<ArticleRow[]>(`/api/support/knowledge-base${qs}`).then(setArticles).catch(() => {});
  }
  useEffect(() => { if (tab === 'kb') loadArticles(); }, [tab]);

  // ── یادداشتِ نسخه ──
  const [releases, setReleases] = useState<ReleaseRow[]>([]);
  useEffect(() => {
    if (tab === 'releases') apiGet<ReleaseRow[]>('/api/support/release-notes').then(setReleases).catch(() => {});
  }, [tab]);

  const ticketColumns: Column<TicketRow>[] = [
    { key: 'subject', header: 'موضوع', render: (r) => <a onClick={() => setSelectedTicket(r)} style={{ cursor: 'pointer' }}>{r.subject}</a> },
    { key: 'status', header: 'وضعیت', render: (r) => <span className="badge badge-gray">{r.statusText}</span> },
    { key: 'sync', header: 'همگام‌سازی', render: (r) => r.syncText },
    { key: 'created', header: 'تاریخِ ایجاد', render: (r) => new Date(r.createdAt).toLocaleDateString('fa-IR') },
  ];

  return (
    <div>
      <PageHeader title="پشتیبانی" />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {notice && <StatusMessage kind="success">{notice}</StatusMessage>}

      <div className="minitabs" style={{ marginBottom: 'var(--space-4)' }}>
        <button type="button" className={tab === 'tickets' ? 'on' : ''} onClick={() => setTab('tickets')}>تیکت‌ها</button>
        <button type="button" className={tab === 'bug' ? 'on' : ''} onClick={() => setTab('bug')}>گزارشِ باگ</button>
        <button type="button" className={tab === 'kb' ? 'on' : ''} onClick={() => setTab('kb')}>مرکزِ راهنما</button>
        <button type="button" className={tab === 'releases' ? 'on' : ''} onClick={() => setTab('releases')}>یادداشتِ نسخه</button>
      </div>

      {tab === 'tickets' && (
        <div>
          <div style={{ marginBottom: 'var(--space-3)' }}>
            <button type="button" className="btn btn-primary btn-sm" onClick={() => setShowNewTicket((v) => !v)}>تیکتِ نو</button>
          </div>
          {showNewTicket && (
            <div className="gbox" style={{ padding: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-3)' }}>
                <div className="field"><label className="label">موضوع</label><input className="input" value={tSubject} onChange={(e) => setTSubject(e.target.value)} /></div>
                <div className="field">
                  <label className="label">دسته</label>
                  <select className="select" value={tCategory} onChange={(e) => setTCategory(Number(e.target.value))}>
                    {CATEGORIES.map((c, i) => <option key={c} value={i}>{c}</option>)}
                  </select>
                </div>
              </div>
              <div className="field" style={{ marginTop: 'var(--space-3)' }}>
                <label className="label">متن</label>
                <textarea className="input" rows={4} value={tBody} onChange={(e) => setTBody(e.target.value)} />
              </div>
              <div style={{ marginTop: 'var(--space-3)' }}>
                <button type="button" className="btn btn-primary btn-sm" onClick={submitTicket}>ثبتِ تیکت</button>
              </div>
            </div>
          )}
          {!selectedTicket && (
            <DataTable columns={ticketColumns} rows={tickets} rowKey={(r) => r.id} emptyText="تیکتی ثبت نشده." />
          )}
          {selectedTicket && (
            <div className="gbox" style={{ padding: 'var(--space-4)' }}>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => setSelectedTicket(null)}>← بازگشت به فهرست</button>
              <div className="gh" style={{ marginTop: 'var(--space-2)' }}>{selectedTicket.subject} <span className="badge badge-gray">{selectedTicket.statusText}</span></div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)', margin: 'var(--space-3) 0', maxHeight: 360, overflowY: 'auto' }}>
                {selectedTicket.messages.map((m, i) => (
                  <div key={i} style={{
                    alignSelf: m.fromSupport ? 'flex-start' : 'flex-end', maxWidth: '70%',
                    background: m.fromSupport ? 'var(--gray-50)' : 'var(--primary-50, #eef4ff)',
                    border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 'var(--space-3)',
                  }}>
                    <div style={{ fontSize: 'var(--text-xs)', color: 'var(--text-muted)', marginBottom: 4 }}>
                      {m.author} — {new Date(m.sentAt).toLocaleString('fa-IR')}
                    </div>
                    <div>{m.text}</div>
                  </div>
                ))}
              </div>
              <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
                <input className="input" style={{ flex: 1 }} placeholder="پاسخ…" value={replyText}
                  onChange={(e) => setReplyText(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && sendReply()} />
                <button type="button" className="btn btn-primary btn-sm" onClick={sendReply}>ارسال</button>
              </div>
            </div>
          )}
        </div>
      )}

      {tab === 'bug' && (
        <div className="gbox" style={{ padding: 'var(--space-4)', maxWidth: 720 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-3)' }}>
            <div className="field"><label className="label">عنوان</label><input className="input" value={bTitle} onChange={(e) => setBTitle(e.target.value)} /></div>
            <div className="field">
              <label className="label">شدت</label>
              <select className="select" value={bSeverity} onChange={(e) => setBSeverity(Number(e.target.value))}>
                {SEVERITIES.map((s, i) => <option key={s} value={i}>{s}</option>)}
              </select>
            </div>
            <div className="field" style={{ gridColumn: 'span 2' }}>
              <label className="label">دسته</label>
              <select className="select" value={bCategory} onChange={(e) => setBCategory(Number(e.target.value))}>
                {CATEGORIES.map((c, i) => <option key={c} value={i}>{c}</option>)}
              </select>
            </div>
          </div>
          <div className="field" style={{ marginTop: 'var(--space-3)' }}>
            <label className="label">شرحِ مشکل</label>
            <textarea className="input" rows={3} value={bDescription} onChange={(e) => setBDescription(e.target.value)} />
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-3)', marginTop: 'var(--space-3)' }}>
            <div className="field"><label className="label">نتیجهٔ موردِ‌انتظار</label><input className="input" value={bExpected} onChange={(e) => setBExpected(e.target.value)} /></div>
            <div className="field"><label className="label">نتیجهٔ واقعی</label><input className="input" value={bActual} onChange={(e) => setBActual(e.target.value)} /></div>
          </div>
          <div className="field" style={{ marginTop: 'var(--space-3)' }}>
            <label className="label">مراحلِ تکرار</label>
            <textarea className="input" rows={2} value={bSteps} onChange={(e) => setBSteps(e.target.value)} />
          </div>
          <div style={{ marginTop: 'var(--space-3)' }}>
            <button type="button" className="btn btn-primary btn-sm" onClick={submitBugReport}>ارسالِ گزارش</button>
          </div>
        </div>
      )}

      {tab === 'kb' && (
        <div>
          <div style={{ display: 'flex', gap: 'var(--space-2)', marginBottom: 'var(--space-4)', maxWidth: 400 }}>
            <input className="input" placeholder="جست‌وجو در راهنما…" value={kbSearch}
              onChange={(e) => setKbSearch(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && loadArticles()} />
            <button type="button" className="btn btn-secondary btn-sm" onClick={loadArticles}>جست‌وجو</button>
          </div>
          {articles.length === 0 && <div style={{ color: 'var(--text-muted)' }}>مقاله‌ای یافت نشد.</div>}
          <div style={{ display: 'grid', gap: 'var(--space-3)' }}>
            {articles.map((a) => (
              <div key={a.remoteId} className="gbox" style={{ padding: 'var(--space-3)' }}>
                <div className="gh">{a.title}</div>
                {a.summary && <div style={{ color: 'var(--text-muted)', marginTop: 4 }}>{a.summary}</div>}
                {a.url && <a href={a.url} target="_blank" rel="noopener noreferrer" style={{ display: 'inline-block', marginTop: 8 }}>مشاهدهٔ کامل ↗</a>}
              </div>
            ))}
          </div>
        </div>
      )}

      {tab === 'releases' && (
        <div style={{ display: 'grid', gap: 'var(--space-3)' }}>
          {releases.length === 0 && <div style={{ color: 'var(--text-muted)' }}>یادداشتِ نسخه‌ای موجود نیست.</div>}
          {releases.map((r) => (
            <div key={r.remoteId} className="gbox" style={{ padding: 'var(--space-3)' }}>
              <div className="gh">نسخهٔ {r.version} {r.isCurrent && <span className="badge badge-green">فعلی</span>}</div>
              {r.highlights && <div style={{ marginTop: 6 }}><b>ویژگی‌های جدید:</b> {r.highlights}</div>}
              {r.bugFixes && <div style={{ marginTop: 6 }}><b>رفعِ اشکال:</b> {r.bugFixes}</div>}
              {r.knownIssues && <div style={{ marginTop: 6 }}><b>مشکلاتِ شناخته‌شده:</b> {r.knownIssues}</div>}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
