"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

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

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 10 · İDARİ İŞLER</span><h1>İdari İşler Merkezi</h1><p>{message}</p>{me ? <div className="session-summary"><strong>Sorumlu varsayılanı: {me.username}</strong></div> : null}</section>

    <section className="panel audit-panel"><label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label></section>

    {permissions.has("administration.task.manage") && me ? <section className="panel audit-panel"><h2>Tekrar eden idari görev</h2><form className="inline-form" onSubmit={e => submit(e, "/api/v1/administration/affairs/tasks", fd => { const unit = String(fd.get("unit")); return { companyId, code: fd.get("code"), title: fd.get("title"), description: fd.get("description") || null, responsibleUserId: me.userId, dueDate: fd.get("dueDate"), recurrenceUnit: unit, recurrenceInterval: unit === "NONE" ? 0 : Number(fd.get("interval")), reminderDaysBefore: Number(fd.get("reminder")) }; }, "İdari görev oluşturuldu.")}><input name="code" placeholder="Görev kodu" required/><input name="title" placeholder="Başlık" required/><input name="description" placeholder="Açıklama"/><input name="dueDate" type="date" defaultValue={today()} required/><select name="unit" defaultValue="MONTHLY"><option value="NONE">Tek sefer</option><option value="DAILY">Günlük</option><option value="WEEKLY">Haftalık</option><option value="MONTHLY">Aylık</option><option value="YEARLY">Yıllık</option></select><input name="interval" type="number" min="1" max="365" defaultValue="1"/><input name="reminder" type="number" min="0" max="365" defaultValue="7"/><button className="primary-button" disabled={busy || !companyId}>Görev ekle</button></form></section> : null}

    {permissions.has("administration.task.view") ? <section className="panel audit-panel"><div className="panel-heading"><h2>Görev takibi</h2><strong>{tasks.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Kod / Başlık</th><th>Sorumlu</th><th>Son tarih</th><th>Tekrar</th><th>Durum</th><th>Tamamlanma</th><th></th></tr></thead><tbody>{tasks.length === 0 ? <tr><td colSpan={7}>Görev yok.</td></tr> : tasks.map(x => <tr key={x.id}><td>{x.code} · {x.title}</td><td>{x.responsibleUsername}</td><td>{x.dueDate}</td><td>{x.recurrenceUnit}{x.recurrenceInterval ? ` / ${x.recurrenceInterval}` : ""}</td><td>{x.status}</td><td>{x.completionCount}</td><td>{permissions.has("administration.task.manage") ? <div className="actions action-row">{x.status === "OPEN" ? <><button className="secondary-button" onClick={() => void taskAction(x,"complete")}>Tamamla</button><button className="secondary-button" onClick={() => void taskAction(x,"pause")}>Duraklat</button></> : null}{x.status === "PAUSED" ? <button className="secondary-button" onClick={() => void taskAction(x,"resume")}>Devam</button> : null}{["OPEN","PAUSED"].includes(x.status) ? <button className="secondary-button" onClick={() => void taskAction(x,"close")}>Kapat</button> : null}</div> : null}</td></tr>)}</tbody></table></div></section> : null}

    {permissions.has("administration.contract.manage") && me ? <section className="panel audit-panel"><h2>Kontrat takibi</h2><form className="inline-form" onSubmit={e => submit(e, "/api/v1/administration/affairs/contracts", fd => { const value = String(fd.get("value") ?? ""); return { companyId, contractNo: fd.get("contractNo"), title: fd.get("title"), counterparty: fd.get("counterparty"), responsibleUserId: me.userId, startDate: fd.get("startDate"), endDate: fd.get("endDate"), reminderDaysBefore: Number(fd.get("reminder")), autoRenewal: fd.get("autoRenewal") === "on", contractValue: value ? Number(value) : null, currency: value ? fd.get("currency") : null, note: fd.get("note") || null }; }, "Kontrat oluşturuldu.")}><input name="contractNo" placeholder="Kontrat no" required/><input name="title" placeholder="Başlık" required/><input name="counterparty" placeholder="Karşı taraf" required/><input name="startDate" type="date" defaultValue={today()} required/><input name="endDate" type="date" required/><input name="reminder" type="number" min="0" max="730" defaultValue="30"/><input name="value" type="number" min="0" step="0.01" placeholder="Kontrat değeri"/><input name="currency" defaultValue="TRY" maxLength={3}/><label><input name="autoRenewal" type="checkbox"/> Otomatik yenileme</label><input name="note" placeholder="Not"/><button className="primary-button" disabled={busy || !companyId}>Kontrat ekle</button></form></section> : null}

    {permissions.has("administration.contract.view") ? <section className="panel audit-panel"><div className="panel-heading"><h2>Kontratlar</h2><strong>{contracts.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>No</th><th>Kontrat</th><th>Karşı taraf</th><th>Sorumlu</th><th>Bitiş</th><th>Durum</th><th>Değer</th><th></th></tr></thead><tbody>{contracts.length === 0 ? <tr><td colSpan={8}>Kontrat yok.</td></tr> : contracts.map(x => <tr key={x.id}><td>{x.contractNo}</td><td>{x.title}</td><td>{x.counterparty}</td><td>{x.responsibleUsername}</td><td>{x.endDate}</td><td><strong>{x.effectiveStatus}</strong></td><td>{x.contractValue == null ? "—" : `${x.contractValue.toFixed(2)} ${x.currency}`}</td><td>{x.storedStatus === "ACTIVE" && permissions.has("administration.contract.manage") ? <button className="secondary-button" onClick={() => void closeContract(x)}>Kapat</button> : null}</td></tr>)}</tbody></table></div></section> : null}

    {permissions.has("administration.reminder.view") ? <section className="panel audit-panel"><div className="panel-heading"><div><h2>Reminder Eventleri</h2><p>Vehicle / görev / kontrat eventleri Sprint 12 Notification Center için hazır kaynak oluşturur.</p></div><div>{permissions.has("administration.reminder.process") ? <button className="primary-button" disabled={busy} onClick={() => void processReminders()}>Şimdi tara</button> : null}</div></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Oluşma</th><th>Önem</th><th>Tür</th><th>Due</th><th>Mesaj</th></tr></thead><tbody>{reminders.length === 0 ? <tr><td colSpan={5}>Henüz reminder event yok.</td></tr> : reminders.map(x => <tr key={x.id}><td>{new Date(x.createdAt).toLocaleString()}</td><td>{x.severity}</td><td>{x.eventType}</td><td>{x.dueDate ?? "—"}</td><td>{x.message}</td></tr>)}</tbody></table></div></section> : null}
  </main>;
}
