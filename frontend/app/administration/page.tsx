"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { Icon } from "../components/Icon";
import { PageHeader } from "../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type Permission = { code: string };
type Me = { userId: string; username: string; permissions: Permission[] };
type Company = { id: string; code: string; name: string };
type Task = { id: string; companyId: string; code: string; title: string; description: string | null; responsibleUsername: string; dueDate: string; recurrenceUnit: string; recurrenceInterval: number; reminderDaysBefore: number; status: string; completionCount: number; lastCompletedAt: string | null; version: number };
type Contract = { id: string; companyId: string; contractNo: string; title: string; counterparty: string; responsibleUsername: string; startDate: string; endDate: string; reminderDaysBefore: number; autoRenewal: boolean; contractValue: number | null; currency: string | null; storedStatus: string; effectiveStatus: string; note: string | null; version: number };
type Reminder = { id: string; eventType: string; sourceType: string; dueDate: string | null; severity: string; message: string; createdAt: string };
type ReminderRun = { candidates: number; created: number; duplicates: number };
type AuthResponse = { accessToken: string };

const today = () => new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 10);
const formatDate = (value: string | null) => value ? new Date(`${value}T00:00:00`).toLocaleDateString("tr-TR") : "—";
const taskStatus = (value: string) => value === "OPEN" ? "Açık" : value === "PAUSED" ? "Duraklatıldı" : value === "CLOSED" ? "Kapatıldı" : value;
const contractStatus = (value: string) => value === "ACTIVE" ? "Aktif" : value === "EXPIRING" ? "Süresi yaklaşıyor" : value === "EXPIRED" ? "Süresi doldu" : value === "CLOSED" ? "Kapatıldı" : value;
const recurrenceLabel = (value: string) => value === "NONE" ? "Tek sefer" : value === "DAILY" ? "Günlük" : value === "WEEKLY" ? "Haftalık" : value === "MONTHLY" ? "Aylık" : value === "YEARLY" ? "Yıllık" : value;

export default function AdministrationPage() {
  const [me, setMe] = useState<Me | null>(null);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [companyId, setCompanyId] = useState("");
  const [tasks, setTasks] = useState<Task[]>([]);
  const [contracts, setContracts] = useState<Contract[]>([]);
  const [reminders, setReminders] = useState<Reminder[]>([]);
  const [message, setMessage] = useState("İdari takip verileri yükleniyor…");
  const [busy, setBusy] = useState(false);
  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const openTasks = tasks.filter(x => x.status === "OPEN");
  const expiringContracts = contracts.filter(x => ["EXPIRING", "EXPIRED"].includes(x.effectiveStatus));
  const importantReminders = reminders.filter(x => ["HIGH", "CRITICAL"].includes(x.severity));

  useEffect(() => { void initialize(); }, []);
  useEffect(() => { if (companyId) void reload(companyId); }, [companyId]);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const companyRows = current.permissions.some(x => x.code === "organization.company.view") ? await json<Company[]>("/api/v1/organization/companies") : [];
    setCompanies(companyRows ?? []);
    if (companyRows?.length) setCompanyId(companyRows[0].id);
    setMessage("Tekrar eden görev, kontrat ve reminder eventleri hazır.");
  }

  async function reload(cid = companyId) {
    const [taskRows, contractRows, reminderRows] = await Promise.all([
      permissions.has("administration.task.view") ? json<Task[]>(`/api/v1/administration/affairs/tasks?companyId=${cid}`) : Promise.resolve(null),
      permissions.has("administration.contract.view") ? json<Contract[]>(`/api/v1/administration/affairs/contracts?companyId=${cid}`) : Promise.resolve(null),
      permissions.has("administration.reminder.view") ? json<Reminder[]>(`/api/v1/administration/affairs/reminders?companyId=${cid}&take=100`) : Promise.resolve(null),
    ]);
    setTasks(taskRows ?? []); setContracts(contractRows ?? []); setReminders(reminderRows ?? []);
  }

  async function submit(event: FormEvent<HTMLFormElement>, path: string, body: (fd: FormData) => unknown, success: string) {
    event.preventDefault(); setBusy(true);
    try {
      const form = event.currentTarget; const fd = new FormData(form);
      const response = await authFetch(path, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body(fd)) });
      if (!response?.ok) { setMessage(await errorMessage(response, "İşlem tamamlanamadı.")); return; }
      form.reset(); setMessage(success); await reload();
    } finally { setBusy(false); }
  }

  async function taskAction(row: Task, action: "complete" | "pause" | "resume" | "close") {
    const note = action === "complete" ? window.prompt("Tamamlama notu (opsiyonel)", "") : null;
    const response = await authFetch(`/api/v1/administration/affairs/tasks/${row.id}/${action}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: row.version, note }) });
    setMessage(response?.ok ? `Görev işlemi tamamlandı: ${action}.` : await errorMessage(response, "Görev işlemi tamamlanamadı.")); await reload();
  }

  async function closeContract(row: Contract) {
    if (!window.confirm(`${row.contractNo} kontratı kapatılsın mı?`)) return;
    const response = await authFetch(`/api/v1/administration/affairs/contracts/${row.id}/close`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: row.version }) });
    setMessage(response?.ok ? "Kontrat kapatıldı." : await errorMessage(response, "Kontrat kapatılamadı.")); await reload();
  }

  async function processReminders() {
    setBusy(true);
    try {
      const response = await authFetch("/api/v1/administration/affairs/reminders/process", { method: "POST" });
      if (!response?.ok) { setMessage(await errorMessage(response, "Reminder işleme başarısız.")); return; }
      const result = await response.json() as ReminderRun;
      setMessage(`Reminder taraması: ${result.candidates} aday, ${result.created} yeni, ${result.duplicates} mevcut.`); await reload();
    } finally { setBusy(false); }
  }

  async function json<T>(path: string): Promise<T | null> { const r = await authFetch(path); return r?.ok ? await r.json() as T : null; }
  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh(); if (!token) return null;
    let r = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (r.status !== 401) return r; token = await refresh(); if (!token) return r;
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> { try { const r = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!r.ok) return null; const b = await r.json() as AuthResponse; sessionStorage.setItem("pp_access_token", b.accessToken); return b.accessToken; } catch { return null; } }
  async function errorMessage(response: Response | null, fallback: string) { if (!response) return fallback; const body = await response.json().catch(() => null) as { error?: { code?: string; message?: string } } | null; return body?.error?.code ? `${body.error.code}: ${body.error.message ?? fallback}` : body?.error?.message ?? fallback; }

  return <main className="page-shell">
    <PageHeader eyebrow="İdari işler" title="Görev ve kontrat takibi" description="Tekrarlanan sorumlulukları, kontrat vadelerini ve yaklaşan hatırlatmaları şirket bazında izleyin." status={message} actions={me?<span className="status-badge">Varsayılan sorumlu: {me.username}</span>:null}/>

    <section className="stat-grid" aria-label="İdari işler özeti">
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{openTasks.length}</strong><span>Açık görev</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="box"/></span><span className="stat-copy"><strong>{contracts.length}</strong><span>Toplam kontrat</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="calendar"/></span><span className="stat-copy"><strong>{expiringContracts.length}</strong><span>Vadesi yaklaşan / dolan</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="bell"/></span><span className="stat-copy"><strong>{importantReminders.length}</strong><span>Yüksek öncelikli uyarı</span></span></article>
    </section>

    <section className="panel workspace-panel"><div className="workspace-copy"><span className="eyebrow dark">Çalışma kapsamı</span><h2>Şirket seçimi</h2><p>Görevler, kontratlar ve hatırlatmalar seçili şirkete göre güncellenir.</p></div><label className="field-label workspace-select">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Şirket seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label></section>

    <div className="content-stack">
      {me&&(permissions.has("administration.task.manage")||permissions.has("administration.contract.manage"))?<section className="organization-grid">
        {permissions.has("administration.task.manage")?<article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Yeni görev</span><h2>İdari sorumluluk oluşturun</h2><p>Tek seferlik veya tekrarlanan görev planlayın.</p></div></div><div className="form-surface"><form className="stack" onSubmit={e => submit(e, "/api/v1/administration/affairs/tasks", fd => { const unit = String(fd.get("unit")); return { companyId, code: fd.get("code"), title: fd.get("title"), description: fd.get("description") || null, responsibleUserId: me.userId, dueDate: fd.get("dueDate"), recurrenceUnit: unit, recurrenceInterval: unit === "NONE" ? 0 : Number(fd.get("interval")), reminderDaysBefore: Number(fd.get("reminder")) }; }, "İdari görev oluşturuldu.")}><label className="field-label">Görev kodu<input name="code" required/></label><label className="field-label">Başlık<input name="title" required/></label><label className="field-label">Açıklama<input name="description"/></label><label className="field-label">İlk son tarih<input name="dueDate" type="date" defaultValue={today()} required/></label><label className="field-label">Tekrar sıklığı<select name="unit" defaultValue="MONTHLY"><option value="NONE">Tek sefer</option><option value="DAILY">Günlük</option><option value="WEEKLY">Haftalık</option><option value="MONTHLY">Aylık</option><option value="YEARLY">Yıllık</option></select></label><label className="field-label">Tekrar aralığı<input name="interval" type="number" min="1" max="365" defaultValue="1"/></label><label className="field-label">Kaç gün önce hatırlatılsın?<input name="reminder" type="number" min="0" max="365" defaultValue="7"/></label><button className="primary-button" disabled={busy || !companyId}>Görevi kaydet</button></form></div></article>:null}
        {permissions.has("administration.contract.manage")?<article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Yeni kontrat</span><h2>Vade takibini başlatın</h2><p>Taraf, dönem, tutar ve yenileme bilgisini kaydedin.</p></div></div><div className="form-surface"><form className="stack" onSubmit={e => submit(e, "/api/v1/administration/affairs/contracts", fd => { const value = String(fd.get("value") ?? ""); return { companyId, contractNo: fd.get("contractNo"), title: fd.get("title"), counterparty: fd.get("counterparty"), responsibleUserId: me.userId, startDate: fd.get("startDate"), endDate: fd.get("endDate"), reminderDaysBefore: Number(fd.get("reminder")), autoRenewal: fd.get("autoRenewal") === "on", contractValue: value ? Number(value) : null, currency: value ? fd.get("currency") : null, note: fd.get("note") || null }; }, "Kontrat oluşturuldu.")}><label className="field-label">Kontrat numarası<input name="contractNo" required/></label><label className="field-label">Başlık<input name="title" required/></label><label className="field-label">Karşı taraf<input name="counterparty" required/></label><label className="field-label">Başlangıç<input name="startDate" type="date" defaultValue={today()} required/></label><label className="field-label">Bitiş<input name="endDate" type="date" required/></label><label className="field-label">Kaç gün önce hatırlatılsın?<input name="reminder" type="number" min="0" max="730" defaultValue="30"/></label><label className="field-label">Kontrat değeri<input name="value" type="number" min="0" step="0.01"/></label><label className="field-label">Para birimi<input name="currency" defaultValue="TRY" maxLength={3}/></label><label className="check-label"><input name="autoRenewal" type="checkbox"/> Otomatik yenilensin</label><label className="field-label">Not<input name="note"/></label><button className="primary-button" disabled={busy || !companyId}>Kontratı kaydet</button></form></div></article>:null}
      </section>:null}

      {permissions.has("administration.task.view") ? <section className={`panel attention-panel ${openTasks.length?"":"success"}`}><div className="panel-heading"><div><span className="eyebrow dark">Görev takibi</span><h2>İdari sorumluluklar</h2><p>Açık görevi tamamlayın, duraklatın veya kalıcı olarak kapatın.</p></div><strong>{tasks.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Görev</th><th>Sorumlu</th><th>Son tarih</th><th>Tekrar</th><th>Durum</th><th>Tamamlanma</th><th>İşlem</th></tr></thead><tbody>{tasks.length === 0 ? <tr><td className="empty-row" colSpan={7}>Henüz görev yok.</td></tr> : tasks.map(x => <tr key={x.id}><td><strong>{x.title}</strong><small>{x.code}</small></td><td>{x.responsibleUsername}</td><td>{formatDate(x.dueDate)}</td><td>{recurrenceLabel(x.recurrenceUnit)}{x.recurrenceInterval>1?` · Her ${x.recurrenceInterval} dönem`:""}</td><td><span className={`status-badge ${x.status==="OPEN"?"success":x.status==="PAUSED"?"warning":""}`}>{taskStatus(x.status)}</span></td><td>{x.completionCount}</td><td>{permissions.has("administration.task.manage") ? <div className="action-row">{x.status === "OPEN" ? <><button className="secondary-button button-success" onClick={() => void taskAction(x,"complete")}>Tamamla</button><button className="secondary-button" onClick={() => void taskAction(x,"pause")}>Duraklat</button></> : null}{x.status === "PAUSED" ? <button className="secondary-button button-success" onClick={() => void taskAction(x,"resume")}>Devam ettir</button> : null}{["OPEN","PAUSED"].includes(x.status) ? <button className="secondary-button button-danger" onClick={() => void taskAction(x,"close")}>Kapat</button> : null}</div> : "—"}</td></tr>)}</tbody></table></div></section> : null}

      {permissions.has("administration.contract.view") ? <section className={`panel attention-panel ${expiringContracts.length?"warning":"success"}`}><div className="panel-heading"><div><span className="eyebrow dark">Kontrat takibi</span><h2>Kontratlar ve vadeler</h2><p>{expiringContracts.length?`${expiringContracts.length} kontrat için vade dikkati gerekiyor.`:"Yaklaşan veya geçmiş kontrat vadesi yok."}</p></div><strong>{contracts.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>No</th><th>Kontrat</th><th>Karşı taraf</th><th>Sorumlu</th><th>Bitiş</th><th>Durum</th><th>Değer</th><th>İşlem</th></tr></thead><tbody>{contracts.length === 0 ? <tr><td className="empty-row" colSpan={8}>Henüz kontrat yok.</td></tr> : contracts.map(x => <tr key={x.id}><td><strong>{x.contractNo}</strong></td><td>{x.title}<small>{x.autoRenewal?"Otomatik yenileme":"Manuel yenileme"}</small></td><td>{x.counterparty}</td><td>{x.responsibleUsername}</td><td>{formatDate(x.endDate)}</td><td><span className={`status-badge ${x.effectiveStatus==="ACTIVE"?"success":x.effectiveStatus==="EXPIRING"?"warning":x.effectiveStatus==="EXPIRED"?"danger":""}`}>{contractStatus(x.effectiveStatus)}</span></td><td>{x.contractValue == null ? "—" : `${x.contractValue.toLocaleString("tr-TR",{minimumFractionDigits:2})} ${x.currency}`}</td><td>{x.storedStatus === "ACTIVE" && permissions.has("administration.contract.manage") ? <button className="secondary-button button-danger" onClick={() => void closeContract(x)}>Kontratı kapat</button> : "—"}</td></tr>)}</tbody></table></div></section> : null}

      {permissions.has("administration.reminder.view") ? <section className={`panel attention-panel ${importantReminders.length?"warning":""}`}><div className="panel-heading"><div><span className="eyebrow dark">Hatırlatmalar</span><h2>Yaklaşan idari olaylar</h2><p>Araç belgeleri, görevler ve kontratlar için üretilen takip kayıtları.</p></div>{permissions.has("administration.reminder.process") ? <button className="primary-button" disabled={busy} onClick={() => void processReminders()}>Hatırlatmaları tara</button> : null}</div><div className="table-wrap"><table className="data-table"><thead><tr><th>Oluşma</th><th>Önem</th><th>Tür</th><th>Son tarih</th><th>Mesaj</th></tr></thead><tbody>{reminders.length === 0 ? <tr><td className="empty-row" colSpan={5}>Henüz hatırlatma yok.</td></tr> : reminders.map(x => <tr key={x.id}><td>{new Date(x.createdAt).toLocaleString("tr-TR")}</td><td><span className={`status-badge ${["HIGH","CRITICAL"].includes(x.severity)?"danger":x.severity==="MEDIUM"?"warning":""}`}>{x.severity==="CRITICAL"?"Kritik":x.severity==="HIGH"?"Yüksek":x.severity==="MEDIUM"?"Orta":"Bilgi"}</span></td><td>{x.eventType}</td><td>{formatDate(x.dueDate)}</td><td>{x.message}</td></tr>)}</tbody></table></div></section> : null}
    </div>
  </main>;
}
