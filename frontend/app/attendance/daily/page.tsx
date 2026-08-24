"use client";

import Link from "next/link";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { Icon } from "../../components/Icon";
import { PageHeader } from "../../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type Permission = { code: string };
type Me = { permissions: Permission[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type Employee = { id: string; employeeNo: string; firstName: string; lastName: string; companyId: string; status: string };
type EmployeePage = { items: Employee[]; totalCount: number };
type RawEvent = { id: string; companyId: string; employeeId: string; source: string; direction: string; eventAt: string; localDate: string; localTime: string; utcOffsetMinutes: number; deviceCode: string | null; externalEventId: string | null; receivedAt: string };
type DailyAttendance = { id: string; companyId: string; employeeId: string; attendanceDate: string; status: string; processingStatus: string; plannedMinutes: number; leaveMinutes: number; workedMinutes: number; lateMinutes: number; earlyLeaveMinutes: number; overtimeCandidateMinutes: number; firstInAt: string | null; lastOutAt: string | null; calculationMessage: string | null; calculatedAt: string; version: number };

function todayLocal() {
  const now = new Date();
  const offset = now.getTimezoneOffset() * 60000;
  return new Date(now.getTime() - offset).toISOString().slice(0, 10);
}

function localDateTimeWithOffset(localDate: string, localTime: string) {
  const local = new Date(`${localDate}T${localTime}:00`);
  const minutesEastOfUtc = -local.getTimezoneOffset();
  const sign = minutesEastOfUtc >= 0 ? "+" : "-";
  const absolute = Math.abs(minutesEastOfUtc);
  const hours = String(Math.floor(absolute / 60)).padStart(2, "0");
  const minutes = String(absolute % 60).padStart(2, "0");
  return `${localDate}T${localTime}:00${sign}${hours}:${minutes}`;
}

export default function DailyAttendancePage() {
  const [me, setMe] = useState<Me | null>(null);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [employeeId, setEmployeeId] = useState("");
  const [date, setDate] = useState(todayLocal());
  const [rawEvents, setRawEvents] = useState<RawEvent[]>([]);
  const [dailyRows, setDailyRows] = useState<DailyAttendance[]>([]);
  const [message, setMessage] = useState("Günlük puantaj yükleniyor…");
  const [busy, setBusy] = useState(false);

  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const selectedEmployee = useMemo(() => employees.find(x => x.id === employeeId) ?? null, [employees, employeeId]);

  useEffect(() => { void initialize(); }, []);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    if (!current.permissions.some(x => x.code === "personnel.view")) {
      setMessage("Personel görüntüleme yetkisi olmadan günlük puantaj ekranı kullanılamaz.");
      return;
    }
    const page = await json<EmployeePage>("/api/v1/personnel/employees?status=ACTIVE&pageSize=100");
    setEmployees(page?.items ?? []);
    setMessage("Personel seçerek ham PDKS olaylarını ve günlük puantajı görüntüleyin.");
  }

  async function selectEmployee(id: string) {
    setEmployeeId(id);
    setRawEvents([]);
    setDailyRows([]);
    if (!id) return;
    await reload(id, date);
  }

  async function changeDate(nextDate: string) {
    setDate(nextDate);
    if (employeeId) await reload(employeeId, nextDate);
  }

  async function reload(id: string, attendanceDate: string) {
    const monthStart = `${attendanceDate.slice(0, 7)}-01`;
    const rawPromise = permissions.has("attendance.raw.view")
      ? json<RawEvent[]>(`/api/v1/attendance/employees/${id}/raw-events?from=${attendanceDate}&to=${attendanceDate}`)
      : Promise.resolve(null);
    const dailyPromise = permissions.has("attendance.daily.view")
      ? json<DailyAttendance[]>(`/api/v1/attendance/employees/${id}/daily?from=${monthStart}&to=${attendanceDate}`)
      : Promise.resolve(null);
    const [raw, daily] = await Promise.all([rawPromise, dailyPromise]);
    setRawEvents(raw ?? []);
    setDailyRows((daily ?? []).sort((a, b) => b.attendanceDate.localeCompare(a.attendanceDate)));
    setMessage("Günlük puantaj verileri güncel.");
  }

  async function ingest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedEmployee) return;
    setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const localDate = String(form.get("eventDate"));
      const localTime = String(form.get("eventTime"));
      const body = {
        companyId: selectedEmployee.companyId,
        employeeId: selectedEmployee.id,
        source: form.get("source"),
        direction: form.get("direction"),
        eventAt: localDateTimeWithOffset(localDate, localTime),
        deviceCode: form.get("deviceCode") || null,
        externalEventId: form.get("externalEventId") || null,
        rawPayloadJson: null,
      };
      const response = await authFetch("/api/v1/attendance/raw-events", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!response?.ok) { setMessage(await errorMessage(response, "PDKS olayı kaydedilemedi.")); return; }
      event.currentTarget.reset();
      setMessage("Ham PDKS olayı değiştirilemez kayıt olarak eklendi.");
      await reload(selectedEmployee.id, date);
    } finally { setBusy(false); }
  }

  async function calculate() {
    if (!employeeId || !date) return;
    setBusy(true);
    try {
      const response = await authFetch("/api/v1/attendance/daily/calculate", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ employeeId, attendanceDate: date }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Günlük puantaj hesaplanamadı.")); return; }
      const row = await response.json() as DailyAttendance;
      setMessage(row.processingStatus === "REVIEW_REQUIRED" ? `Hesaplandı; kontrol gerekli: ${row.calculationMessage ?? "kayıtları inceleyin."}` : "Günlük puantaj hesaplandı.");
      await reload(employeeId, date);
    } finally { setBusy(false); }
  }

  async function json<T>(path: string): Promise<T | null> { const response = await authFetch(path); return response?.ok ? await response.json() as T : null; }
  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh();
    if (!token) return null;
    let response = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status !== 401) return response;
    token = await refresh(); if (!token) return response;
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> { try { const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!response.ok) return null; const body = await response.json() as AuthResponse; sessionStorage.setItem("pp_access_token", body.accessToken); return body.accessToken; } catch { return null; } }
  async function errorMessage(response: Response | null, fallback: string) { if (!response) return fallback; const body = await response.json().catch(() => null) as { error?: { message?: string } } | null; return body?.error?.message ?? fallback; }

  const reviewCount = dailyRows.filter(row => row.processingStatus === "REVIEW_REQUIRED").length;
  const overtimeMinutes = dailyRows.reduce((total, row) => total + row.overtimeCandidateMinutes, 0);
  const workedMinutes = dailyRows.reduce((total, row) => total + row.workedMinutes, 0);

  return <main className="page-shell">
    <PageHeader eyebrow="Puantaj ve Vardiya" title="Günlük Puantaj" description="Ham giriş-çıkış olaylarını ve hesaplanmış günlük çalışma sonuçlarını personel bazında inceleyin." status={message} actions={<Link className="secondary-button" href="/attendance/overtime">Fazla mesai merkezi <Icon name="arrow" size={15}/></Link>}/>

    <section className="stat-grid" aria-label="Günlük puantaj göstergeleri">
      <article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{employees.length}</strong><span>Aktif personel</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{rawEvents.length}</strong><span>Seçili gün olayı</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="settings"/></span><span className="stat-copy"><strong className={reviewCount ? "review-count" : ""}>{reviewCount}</strong><span>Kontrol gereken</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="calendar"/></span><span className="stat-copy"><strong>{Math.round(workedMinutes / 60)} sa</strong><span>Aylık çalışma</span></span></article>
    </section>

    <section className="panel workspace-panel">
      <div className="workspace-copy"><span className="page-eyebrow">Çalışma kapsamı</span><h2>{selectedEmployee ? `${selectedEmployee.firstName} ${selectedEmployee.lastName}` : "Personel seçin"}</h2><p>Ham olaylar seçili güne, hesaplanan sonuçlar ay başından seçili güne kadar getirilir.</p></div>
      <div className="inline-form workspace-select">
        <label className="field-label">Personel<select value={employeeId} onChange={e => void selectEmployee(e.target.value)}><option value="">Personel seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label>
        <label className="field-label">Tarih<input type="date" value={date} onChange={e => void changeDate(e.target.value)}/></label>
        {permissions.has("attendance.daily.calculate") ? <button className="primary-button" disabled={busy || !employeeId} onClick={() => void calculate()}>{busy ? "Hesaplanıyor…" : "Puantajı hesapla"}</button> : null}
      </div>
    </section>

    <div className="content-stack">
    {permissions.has("attendance.raw.ingest") && selectedEmployee ? <section className="panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Manuel olay</span><h2>Ham PDKS olayı ekleyin</h2><p>Bu kayıt eklendikten sonra değiştirilemez veya silinemez.</p></div></div>
      <div className="notice"><Icon name="settings" size={17}/><span>Gerçek PDKS ve entegrasyon kayıtlarında tekrarları engellemek için benzersiz dış olay kimliği kullanılır.</span></div>
      <form className="inline-form" onSubmit={ingest}>
        <label className="field-label">Kaynak<select name="source" defaultValue="MANUAL"><option value="MANUAL">Manuel</option><option value="PDKS">PDKS</option><option value="IMPORT">İçe aktarım</option><option value="INTEGRATION">Entegrasyon</option></select></label>
        <label className="field-label">Hareket<select name="direction" defaultValue="IN"><option value="IN">Giriş</option><option value="OUT">Çıkış</option><option value="UNKNOWN">Bilinmiyor</option></select></label>
        <label className="field-label">Tarih<input name="eventDate" type="date" defaultValue={date} required/></label>
        <label className="field-label">Saat<input name="eventTime" type="time" required/></label>
        <label className="field-label">Cihaz<input name="deviceCode" maxLength={100}/></label>
        <label className="field-label">External Event ID<input name="externalEventId" maxLength={200}/></label>
        <button className="primary-button" disabled={busy}>{busy ? "Kaydediliyor…" : "Ham olay ekle"}</button>
      </form>
    </section> : null}

    {permissions.has("attendance.raw.view") ? <section className="panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Ham kayıtlar</span><h2>{formatDate(date)} PDKS olayları</h2><p>Seçili günün değiştirilemez giriş ve çıkış olayları.</p></div><strong>{rawEvents.length}</strong></div>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Yerel saat</th><th>Yön</th><th>Kaynak</th><th>Cihaz</th><th>Dış olay kimliği</th><th>UTC farkı</th></tr></thead><tbody>{rawEvents.map(x => <tr key={x.id}><td><strong>{x.localTime}</strong><small>{formatDate(x.localDate)}</small></td><td><span className={`status-badge ${x.direction === "IN" ? "success" : x.direction === "OUT" ? "warning" : ""}`}>{directionLabel(x.direction)}</span></td><td>{sourceLabel(x.source)}</td><td>{x.deviceCode ?? "—"}</td><td>{x.externalEventId ?? "—"}</td><td>{x.utcOffsetMinutes} dk</td></tr>)}{rawEvents.length === 0 ? <tr><td className="empty-row" colSpan={6}>{employeeId ? "Seçili gün için ham PDKS olayı yok." : "Kayıtları görüntülemek için personel seçin."}</td></tr> : null}</tbody></table></div>
    </section> : null}

    {permissions.has("attendance.daily.view") ? <section className={`panel attention-panel ${reviewCount ? "danger" : "success"}`}>
      <div className="panel-heading"><div><span className="page-eyebrow">Hesaplanan puantaj</span><h2>Aylık günlük sonuçlar</h2><p>Ay başından seçili tarihe kadar hesaplanan çalışma özeti.</p></div><strong>{dailyRows.length}</strong></div>
      <div className="selected-summary"><div className="selected-summary-copy"><strong>{Math.round(workedMinutes / 60)} saat çalışılan · {overtimeMinutes} dk fazla mesai adayı</strong><small>{reviewCount ? `${reviewCount} gün manuel kontrol gerektiriyor.` : "Hesaplanan günlerde kontrol gerektiren kayıt yok."}</small></div><span className={`status-badge ${reviewCount ? "danger" : "success"}`}>{reviewCount ? "Kontrol gerekli" : "Güncel"}</span></div>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>Durum</th><th>İşlem</th><th>Plan</th><th>Çalışılan</th><th>İzin</th><th>Geç</th><th>Erken</th><th>FM adayı</th><th>Kontrol</th></tr></thead><tbody>{dailyRows.map(x => <tr key={x.id}><td>{formatDate(x.attendanceDate)}</td><td><strong>{attendanceStatusLabel(x.status)}</strong></td><td><span className={`status-badge ${x.processingStatus === "REVIEW_REQUIRED" ? "danger" : "success"}`}>{processingLabel(x.processingStatus)}</span></td><td>{x.plannedMinutes} dk</td><td>{x.workedMinutes} dk</td><td>{x.leaveMinutes} dk</td><td>{x.lateMinutes} dk</td><td>{x.earlyLeaveMinutes} dk</td><td><strong>{x.overtimeCandidateMinutes} dk</strong></td><td>{x.calculationMessage ?? "—"}</td></tr>)}{dailyRows.length === 0 ? <tr><td className="empty-row" colSpan={10}>{employeeId ? "Henüz hesaplanmış günlük puantaj yok." : "Sonuçları görüntülemek için personel seçin."}</td></tr> : null}</tbody></table></div>
    </section> : null}
    </div>
  </main>;
}

function formatDate(value: string) { return new Intl.DateTimeFormat("tr-TR", { day: "2-digit", month: "short", year: "numeric" }).format(new Date(`${value}T00:00:00`)); }
function directionLabel(value: string) { return value === "IN" ? "Giriş" : value === "OUT" ? "Çıkış" : "Bilinmiyor"; }
function sourceLabel(value: string) { return value === "MANUAL" ? "Manuel" : value === "IMPORT" ? "İçe aktarım" : value === "INTEGRATION" ? "Entegrasyon" : value; }
function processingLabel(value: string) { return value === "REVIEW_REQUIRED" ? "Kontrol gerekli" : value === "CALCULATED" ? "Hesaplandı" : value; }
function attendanceStatusLabel(value: string) { return value === "PRESENT" ? "Çalıştı" : value === "ABSENT" ? "Devamsız" : value === "LEAVE" ? "İzinli" : value === "OFF_DAY" ? "Çalışma dışı" : value; }
