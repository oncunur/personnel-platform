"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

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
      const local = new Date(`${localDate}T${localTime}:00`);
      const body = {
        companyId: selectedEmployee.companyId,
        employeeId: selectedEmployee.id,
        source: form.get("source"),
        direction: form.get("direction"),
        eventAt: local.toISOString(),
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

  return <main className="shell">
    <a className="back" href="/attendance">← Takvim & Vardiya</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 5 · PDKS</span><h1>Günlük Puantaj</h1><p>{message}</p></section>

    <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">SEÇİM</span><h2>Personel & Tarih</h2></div></div>
      <div className="inline-form">
        <label className="field-label">Personel<select value={employeeId} onChange={e => void selectEmployee(e.target.value)}><option value="">Seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label>
        <label className="field-label">Tarih<input type="date" value={date} onChange={e => void changeDate(e.target.value)}/></label>
        {permissions.has("attendance.daily.calculate") ? <button className="primary-button" disabled={busy || !employeeId} onClick={() => void calculate()}>Günlük Puantajı Hesapla</button> : null}
      </div>
    </section>

    {permissions.has("attendance.raw.ingest") && selectedEmployee ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">HAM PDKS</span><h2>Test / Manuel Olay Girişi</h2></div></div>
      <p>Bu kayıt eklendikten sonra değiştirilemez veya silinemez. Gerçek PDKS entegrasyonu aynı API'yi idempotent externalEventId ile kullanacaktır.</p>
      <form className="inline-form" onSubmit={ingest}>
        <label className="field-label">Kaynak<select name="source" defaultValue="MANUAL"><option value="MANUAL">MANUAL</option><option value="PDKS">PDKS</option><option value="IMPORT">IMPORT</option><option value="INTEGRATION">INTEGRATION</option></select></label>
        <label className="field-label">Hareket<select name="direction" defaultValue="IN"><option value="IN">IN</option><option value="OUT">OUT</option><option value="UNKNOWN">UNKNOWN</option></select></label>
        <label className="field-label">Tarih<input name="eventDate" type="date" defaultValue={date} required/></label>
        <label className="field-label">Saat<input name="eventTime" type="time" required/></label>
        <label className="field-label">Cihaz<input name="deviceCode" maxLength={100}/></label>
        <label className="field-label">External Event ID<input name="externalEventId" maxLength={200}/></label>
        <button className="primary-button" disabled={busy}>Ham Olay Ekle</button>
      </form>
    </section> : null}

    {permissions.has("attendance.raw.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">HAM KAYITLAR</span><h2>{date} PDKS Olayları</h2></div><strong>{rawEvents.length}</strong></div>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Yerel Saat</th><th>Yön</th><th>Kaynak</th><th>Cihaz</th><th>External ID</th><th>UTC Offset</th></tr></thead><tbody>{rawEvents.length === 0 ? <tr><td colSpan={6}>Ham PDKS olayı yok.</td></tr> : rawEvents.map(x => <tr key={x.id}><td>{x.localDate} {x.localTime}</td><td><strong>{x.direction}</strong></td><td>{x.source}</td><td>{x.deviceCode ?? "—"}</td><td>{x.externalEventId ?? "—"}</td><td>{x.utcOffsetMinutes} dk</td></tr>)}</tbody></table></div>
    </section> : null}

    {permissions.has("attendance.daily.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">GÜNLÜK PUANTAJ</span><h2>Aylık Sonuçlar</h2></div><strong>{dailyRows.length}</strong></div>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>Durum</th><th>İşlem</th><th>Plan</th><th>Çalışılan</th><th>İzin</th><th>Geç</th><th>Erken</th><th>Pot. FM</th><th>Kontrol</th></tr></thead><tbody>{dailyRows.length === 0 ? <tr><td colSpan={10}>Henüz hesaplanmış günlük puantaj yok.</td></tr> : dailyRows.map(x => <tr key={x.id}><td>{x.attendanceDate}</td><td><strong>{x.status}</strong></td><td><span className={`status-badge ${x.processingStatus === "REVIEW_REQUIRED" ? "danger" : "success"}`}>{x.processingStatus}</span></td><td>{x.plannedMinutes}</td><td>{x.workedMinutes}</td><td>{x.leaveMinutes}</td><td>{x.lateMinutes}</td><td>{x.earlyLeaveMinutes}</td><td>{x.overtimeCandidateMinutes}</td><td>{x.calculationMessage ?? "—"}</td></tr>)}</tbody></table></div>
    </section> : null}
  </main>;
}
