"use client";

import { useEffect, useMemo, useState } from "react";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type Permission = { code: string };
type Me = { permissions: Permission[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type Employee = { id: string; employeeNo: string; firstName: string; lastName: string; companyId: string; status: string };
type EmployeePage = { items: Employee[]; totalCount: number };
type DailyAttendance = { id: string; employeeId: string; attendanceDate: string; processingStatus: string; overtimeCandidateMinutes: number; version: number };
type Overtime = { id: string; companyId: string; employeeId: string; employeeNo: string; employeeName: string; dailyAttendanceId: string; sourceDailyVersion: number; attendanceDate: string; candidateMinutes: number; requestedMinutes: number; approvedMinutes: number; status: string; reason: string | null; submittedAt: string; decisionNote: string | null; version: number };
type OvertimePage = { items: Overtime[]; page: number; pageSize: number; totalCount: number };
type Inbox = { id: string; companyId: string; employeeId: string; employeeNo: string; employeeName: string; attendanceDate: string; candidateMinutes: number; requestedMinutes: number; status: string; canDecide: boolean; version: number };

function monthRange() {
  const now = new Date();
  const offset = now.getTimezoneOffset() * 60000;
  const local = new Date(now.getTime() - offset).toISOString().slice(0, 10);
  return { from: `${local.slice(0, 7)}-01`, to: local };
}

export default function OvertimePage() {
  const [me, setMe] = useState<Me | null>(null);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [employeeId, setEmployeeId] = useState("");
  const [dailyRows, setDailyRows] = useState<DailyAttendance[]>([]);
  const [rows, setRows] = useState<Overtime[]>([]);
  const [inbox, setInbox] = useState<Inbox[]>([]);
  const [message, setMessage] = useState("Fazla mesai merkezi yükleniyor…");
  const [busy, setBusy] = useState(false);
  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);

  useEffect(() => { void initialize(); }, []);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const codes = new Set(current.permissions.map(x => x.code));
    const [employeePage, overtimePage, pending] = await Promise.all([
      codes.has("personnel.view") ? json<EmployeePage>("/api/v1/personnel/employees?status=ACTIVE&pageSize=100") : Promise.resolve(null),
      codes.has("attendance.overtime.view") ? json<OvertimePage>("/api/v1/attendance/overtime?pageSize=100") : Promise.resolve(null),
      codes.has("attendance.overtime.manager.approve") || codes.has("attendance.overtime.hr.approve") ? json<Inbox[]>("/api/v1/attendance/overtime/inbox") : Promise.resolve(null),
    ]);
    setEmployees(employeePage?.items ?? []);
    setRows(overtimePage?.items ?? []);
    setInbox(pending ?? []);
    setMessage("Fazla mesai merkezi güncel.");
  }

  async function selectEmployee(id: string) {
    setEmployeeId(id);
    setDailyRows([]);
    if (!id || !permissions.has("attendance.daily.view")) return;
    const range = monthRange();
    const daily = await json<DailyAttendance[]>(`/api/v1/attendance/employees/${id}/daily?from=${range.from}&to=${range.to}`);
    setDailyRows((daily ?? []).filter(x => x.overtimeCandidateMinutes > 0));
  }

  async function createRequest(daily: DailyAttendance) {
    const raw = window.prompt(`Aday fazla mesai: ${daily.overtimeCandidateMinutes} dk. Talep edilecek dakika:`, String(daily.overtimeCandidateMinutes));
    if (raw === null) return;
    const requestedMinutes = Number(raw);
    if (!Number.isInteger(requestedMinutes) || requestedMinutes <= 0 || requestedMinutes > daily.overtimeCandidateMinutes) {
      setMessage("Talep dakikası sıfırdan büyük ve aday dakikadan küçük/eşit olmalıdır.");
      return;
    }
    const reason = window.prompt("Fazla mesai gerekçesi (isteğe bağlı):", "") ?? null;
    setBusy(true);
    try {
      const response = await authFetch("/api/v1/attendance/overtime", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ dailyAttendanceId: daily.id, requestedMinutes, reason }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Fazla mesai talebi oluşturulamadı.")); return; }
      setMessage("Fazla mesai talebi yönetici onayına gönderildi.");
      await refreshQueues();
    } finally { setBusy(false); }
  }

  async function decide(item: Inbox, approve: boolean) {
    let approvedMinutes: number | null = null;
    if (approve && item.status === "PENDING_HR") {
      const raw = window.prompt(`Talep: ${item.requestedMinutes} dk. Nihai onaylanacak dakika:`, String(item.requestedMinutes));
      if (raw === null) return;
      approvedMinutes = Number(raw);
      if (!Number.isInteger(approvedMinutes) || approvedMinutes <= 0 || approvedMinutes > item.requestedMinutes) {
        setMessage("Onaylanan dakika sıfırdan büyük ve talep dakikasından küçük/eşit olmalıdır.");
        return;
      }
    }
    const note = window.prompt(approve ? "Onay notu (isteğe bağlı):" : "Red nedeni (önerilir):", "") ?? null;
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/attendance/overtime/${item.id}/decision`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ approve, approvedMinutes, note, version: item.version }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Karar kaydedilemedi.")); return; }
      setMessage(approve ? (item.status === "PENDING_MANAGER" ? "Yönetici onayı tamamlandı; talep HR onayına geçti." : "Fazla mesai nihai olarak onaylandı.") : "Fazla mesai talebi reddedildi.");
      await refreshQueues();
    } finally { setBusy(false); }
  }

  async function cancel(row: Overtime) {
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/attendance/overtime/${row.id}/cancel`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: row.version }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Talep iptal edilemedi.")); return; }
      setMessage("Fazla mesai talebi iptal edildi.");
      await refreshQueues();
    } finally { setBusy(false); }
  }

  async function refreshQueues() {
    if (permissions.has("attendance.overtime.view")) {
      const page = await json<OvertimePage>("/api/v1/attendance/overtime?pageSize=100");
      setRows(page?.items ?? []);
    }
    if (permissions.has("attendance.overtime.manager.approve") || permissions.has("attendance.overtime.hr.approve")) {
      setInbox((await json<Inbox[]>("/api/v1/attendance/overtime/inbox")) ?? []);
    }
    if (employeeId && permissions.has("attendance.daily.view")) await selectEmployee(employeeId);
  }

  async function json<T>(path: string): Promise<T | null> { const response = await authFetch(path); return response?.ok ? await response.json() as T : null; }
  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh(); if (!token) return null;
    let response = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status !== 401) return response;
    token = await refresh(); if (!token) return response;
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> { try { const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!response.ok) return null; const body = await response.json() as AuthResponse; sessionStorage.setItem("pp_access_token", body.accessToken); return body.accessToken; } catch { return null; } }
  async function errorMessage(response: Response | null, fallback: string) { if (!response) return fallback; const body = await response.json().catch(() => null) as { error?: { message?: string } } | null; return body?.error?.message ?? fallback; }

  return <main className="shell">
    <a className="back" href="/attendance/daily">← Günlük Puantaj</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 5 · FAZLA MESAİ</span><h1>Fazla Mesai Merkezi</h1><p>{message}</p></section>

    {permissions.has("attendance.overtime.request") && permissions.has("attendance.daily.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">ADAY → TALEP</span><h2>Puantaj Fazla Mesai Adayları</h2></div><strong>{dailyRows.length}</strong></div>
      <label className="field-label">Personel<select value={employeeId} onChange={e => void selectEmployee(e.target.value)}><option value="">Seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>Aday FM</th><th>Puantaj Durumu</th><th></th></tr></thead><tbody>{dailyRows.length === 0 ? <tr><td colSpan={4}>Seçili personelin bu ay için FM adayı bulunmuyor.</td></tr> : dailyRows.map(x => <tr key={x.id}><td>{x.attendanceDate}</td><td><strong>{x.overtimeCandidateMinutes} dk</strong></td><td>{x.processingStatus}</td><td><button className="table-button" disabled={busy} onClick={() => void createRequest(x)}>Talep Oluştur</button></td></tr>)}</tbody></table></div>
    </section> : null}

    {(permissions.has("attendance.overtime.manager.approve") || permissions.has("attendance.overtime.hr.approve")) ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">ONAY KUTUSU</span><h2>Bekleyen Fazla Mesailer</h2></div><strong>{inbox.length}</strong></div>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Personel</th><th>Tarih</th><th>Aday</th><th>Talep</th><th>Adım</th><th></th></tr></thead><tbody>{inbox.length === 0 ? <tr><td colSpan={6}>Bekleyen onay yok.</td></tr> : inbox.map(x => <tr key={x.id}><td><strong>{x.employeeName}</strong><small>{x.employeeNo}</small></td><td>{x.attendanceDate}</td><td>{x.candidateMinutes} dk</td><td>{x.requestedMinutes} dk</td><td>{x.status === "PENDING_MANAGER" ? "Yönetici" : "HR"}</td><td><div className="actions action-row"><button className="table-button" disabled={busy} onClick={() => void decide(x, true)}>Onayla</button><button className="table-button" disabled={busy} onClick={() => void decide(x, false)}>Reddet</button></div></td></tr>)}</tbody></table></div>
    </section> : null}

    {permissions.has("attendance.overtime.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">KAYITLAR</span><h2>Fazla Mesai Talepleri</h2></div><strong>{rows.length}</strong></div>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Personel</th><th>Tarih</th><th>Kaynak</th><th>Aday</th><th>Talep</th><th>Onaylanan</th><th>Durum</th><th></th></tr></thead><tbody>{rows.length === 0 ? <tr><td colSpan={8}>Fazla mesai talebi bulunmuyor.</td></tr> : rows.map(x => <tr key={x.id}><td><strong>{x.employeeName}</strong><small>{x.employeeNo}</small></td><td>{x.attendanceDate}</td><td>Daily v{x.sourceDailyVersion}</td><td>{x.candidateMinutes} dk</td><td>{x.requestedMinutes} dk</td><td><strong>{x.approvedMinutes} dk</strong></td><td><span className={`status-badge ${x.status === "APPROVED" ? "success" : x.status === "REJECTED" ? "danger" : ""}`}>{x.status}</span></td><td>{permissions.has("attendance.overtime.request") && x.status === "PENDING_MANAGER" ? <button className="table-button" disabled={busy} onClick={() => void cancel(x)}>İptal</button> : null}</td></tr>)}</tbody></table></div>
    </section> : null}
  </main>;
}
