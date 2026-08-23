"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type Permission = { code: string };
type Me = { permissions: Permission[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type Named = { id: string; code: string; name: string };
type Employee = { id: string; employeeNo: string; firstName: string; lastName: string; companyId: string; status: string };
type EmployeePage = { items: Employee[]; totalCount: number };
type WorkCalendar = { id: string; companyId: string; code: string; name: string; isDefault: boolean; isActive: boolean; version: number };
type CalendarDay = { id: string; workCalendarId: string; date: string; dayType: string; plannedMinutes: number; isPaid: boolean; description: string | null; version: number };
type Shift = { id: string; companyId: string; code: string; name: string; startTime: string; endTime: string; breakMinutes: number; plannedMinutes: number; graceInMinutes: number; graceOutMinutes: number; crossesMidnight: boolean; isActive: boolean; version: number };
type Assignment = { id: string; employeeId: string; shiftId: string; shiftCode: string; shiftName: string; workCalendarId: string; calendarCode: string; calendarName: string; validFrom: string; validUntil: string | null; note: string | null; crossesMidnight: boolean; startTime: string; endTime: string; plannedMinutes: number; version: number };

export default function AttendancePage() {
  const [me, setMe] = useState<Me | null>(null);
  const [companies, setCompanies] = useState<Named[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [companyId, setCompanyId] = useState("");
  const [calendars, setCalendars] = useState<WorkCalendar[]>([]);
  const [shifts, setShifts] = useState<Shift[]>([]);
  const [calendarId, setCalendarId] = useState("");
  const [calendarDays, setCalendarDays] = useState<CalendarDay[]>([]);
  const [employeeId, setEmployeeId] = useState("");
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [message, setMessage] = useState("Puantaj altyapısı yükleniyor…");
  const [busy, setBusy] = useState(false);

  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const companyEmployees = useMemo(() => employees.filter(x => x.companyId === companyId), [employees, companyId]);

  useEffect(() => { void initialize(); }, []);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const [companyRows, employeeRows] = await Promise.all([
      json<Named[]>("/api/v1/organization/companies"),
      current.permissions.some(x => x.code === "personnel.view") ? json<EmployeePage>("/api/v1/personnel/employees?status=ACTIVE&pageSize=100") : Promise.resolve(null),
    ]);
    setCompanies(companyRows ?? []);
    setEmployees(employeeRows?.items ?? []);
    const firstCompanyId = companyRows?.[0]?.id ?? "";
    setCompanyId(firstCompanyId);
    if (firstCompanyId) await loadCompany(firstCompanyId, current);
    setMessage("Çalışma takvimi ve vardiya merkezi güncel.");
  }

  async function loadCompany(id: string, current = me) {
    setCompanyId(id); setCalendarId(""); setCalendarDays([]); setEmployeeId(""); setAssignments([]);
    if (!id || !current) { setCalendars([]); setShifts([]); return; }
    const codes = new Set(current.permissions.map(x => x.code));
    const [calendarRows, shiftRows] = await Promise.all([
      codes.has("attendance.calendar.view") ? json<WorkCalendar[]>(`/api/v1/attendance/calendars?companyId=${id}`) : Promise.resolve(null),
      codes.has("attendance.shift.view") ? json<Shift[]>(`/api/v1/attendance/shifts?companyId=${id}`) : Promise.resolve(null),
    ]);
    setCalendars(calendarRows ?? []); setShifts(shiftRows ?? []);
  }

  async function createCalendar(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!companyId) return; setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const response = await authFetch("/api/v1/attendance/calendars", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ companyId, code: form.get("code"), name: form.get("name"), isDefault: form.get("isDefault") === "on" }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Çalışma takvimi oluşturulamadı.")); return; }
      const saved = await response.json() as WorkCalendar;
      setCalendars(current => [...current, saved].sort((a, b) => a.code.localeCompare(b.code)));
      event.currentTarget.reset(); setMessage("Çalışma takvimi oluşturuldu.");
    } finally { setBusy(false); }
  }

  async function selectCalendar(id: string) {
    setCalendarId(id); setCalendarDays([]);
    if (!id || !permissions.has("attendance.calendar.view")) return;
    const year = new Date().getFullYear();
    setCalendarDays((await json<CalendarDay[]>(`/api/v1/attendance/calendars/${id}/days?from=${year}-01-01&to=${year}-12-31`)) ?? []);
  }

  async function upsertCalendarDay(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!calendarId) return; setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const dayType = String(form.get("dayType"));
      const body = { date: form.get("date"), dayType, plannedMinutes: dayType === "WORKDAY" ? Number(form.get("plannedMinutes") || 0) : 0, isPaid: form.get("isPaid") === "on", description: form.get("description") || null };
      const response = await authFetch(`/api/v1/attendance/calendars/${calendarId}/days`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Takvim günü kaydedilemedi.")); return; }
      const saved = await response.json() as CalendarDay;
      setCalendarDays(current => [...current.filter(x => x.id !== saved.id), saved].sort((a, b) => a.date.localeCompare(b.date))));
      setMessage("Takvim günü güncellendi.");
    } finally { setBusy(false); }
  }

  async function createShift(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!companyId) return; setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const body = { companyId, code: form.get("code"), name: form.get("name"), startTime: form.get("startTime"), endTime: form.get("endTime"), breakMinutes: Number(form.get("breakMinutes") || 0), graceInMinutes: Number(form.get("graceInMinutes") || 0), graceOutMinutes: Number(form.get("graceOutMinutes") || 0) };
      const response = await authFetch("/api/v1/attendance/shifts", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Vardiya oluşturulamadı.")); return; }
      const saved = await response.json() as Shift;
      setShifts(current => [...current, saved].sort((a, b) => a.code.localeCompare(b.code)));
      event.currentTarget.reset(); setMessage(saved.crossesMidnight ? "Geceye taşan vardiya oluşturuldu." : "Vardiya oluşturuldu.");
    } finally { setBusy(false); }
  }

  async function selectEmployee(id: string) {
    setEmployeeId(id); setAssignments([]);
    if (!id || !permissions.has("attendance.assignment.view")) return;
    setAssignments((await json<Assignment[]>(`/api/v1/attendance/employees/${id}/shift-assignments`)) ?? []);
  }

  async function assignShift(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!employeeId) return; setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const body = { shiftId: form.get("shiftId"), workCalendarId: form.get("workCalendarId"), validFrom: form.get("validFrom"), validUntil: form.get("validUntil") || null, note: form.get("note") || null };
      const response = await authFetch(`/api/v1/attendance/employees/${employeeId}/shift-assignments`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Vardiya ataması oluşturulamadı.")); return; }
      const saved = await response.json() as Assignment;
      setAssignments(current => [saved, ...current]);
      event.currentTarget.reset(); setMessage("Personel vardiya ataması oluşturuldu.");
    } finally { setBusy(false); }
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
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 5 · PUANTAJ / VARDİYA</span><h1>Çalışma Takvimi & Vardiya Merkezi</h1><p>{message}</p></section>

    <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">KAPSAM</span><h2>Şirket</h2></div></div><label className="field-label">Şirket<select value={companyId} onChange={event => void loadCompany(event.target.value)}><option value="">Seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label></section>

    {permissions.has("attendance.calendar.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">TAKVİM</span><h2>Çalışma Takvimleri</h2></div><strong>{calendars.length}</strong></div>
      {permissions.has("attendance.calendar.manage") ? <form className="inline-form" onSubmit={createCalendar}><label className="field-label">Kod<input name="code" required maxLength={80}/></label><label className="field-label">Ad<input name="name" required maxLength={150}/></label><label className="field-label">Varsayılan<input name="isDefault" type="checkbox"/></label><button className="primary-button" disabled={busy || !companyId}>Takvim Oluştur</button></form> : null}
      <label className="field-label">Takvim<select value={calendarId} onChange={event => void selectCalendar(event.target.value)}><option value="">Seçin</option>{calendars.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}{x.isDefault ? " · Varsayılan" : ""}</option>)}</select></label>
      {calendarId && permissions.has("attendance.calendar.manage") ? <form className="inline-form" onSubmit={upsertCalendarDay}><label className="field-label">Tarih<input name="date" type="date" required/></label><label className="field-label">Gün Tipi<select name="dayType" defaultValue="HOLIDAY"><option value="WORKDAY">Çalışma Günü</option><option value="WEEKEND">Hafta Sonu</option><option value="HOLIDAY">Tatil</option><option value="OFF_DAY">İzinli/Off</option></select></label><label className="field-label">Planlanan Dakika<input name="plannedMinutes" type="number" min={0} max={1440} defaultValue={480}/></label><label className="field-label">Ücretli<input name="isPaid" type="checkbox" defaultChecked/></label><label className="field-label">Açıklama<input name="description" maxLength={500}/></label><button className="primary-button" disabled={busy}>Günü Kaydet</button></form> : null}
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>Tip</th><th>Planlanan</th><th>Ücretli</th><th>Açıklama</th></tr></thead><tbody>{calendarDays.length === 0 ? <tr><td colSpan={5}>Seçili yıl için özel takvim günü yok.</td></tr> : calendarDays.map(x => <tr key={x.id}><td>{x.date}</td><td>{x.dayType}</td><td>{x.plannedMinutes} dk</td><td>{x.isPaid ? "Evet" : "Hayır"}</td><td>{x.description ?? "—"}</td></tr>)}</tbody></table></div>
    </section> : null}

    {permissions.has("attendance.shift.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">VARDİYA</span><h2>Vardiya Tanımları</h2></div><strong>{shifts.length}</strong></div>
      {permissions.has("attendance.shift.manage") ? <form className="inline-form" onSubmit={createShift}><label className="field-label">Kod<input name="code" required maxLength={80}/></label><label className="field-label">Ad<input name="name" required maxLength={150}/></label><label className="field-label">Başlangıç<input name="startTime" type="time" required/></label><label className="field-label">Bitiş<input name="endTime" type="time" required/></label><label className="field-label">Mola dk<input name="breakMinutes" type="number" min={0} defaultValue={60}/></label><label className="field-label">Giriş Tolerans dk<input name="graceInMinutes" type="number" min={0} max={240} defaultValue={0}/></label><label className="field-label">Çıkış Tolerans dk<input name="graceOutMinutes" type="number" min={0} max={240} defaultValue={0}/></label><button className="primary-button" disabled={busy || !companyId}>Vardiya Oluştur</button></form> : null}
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Vardiya</th><th>Saat</th><th>Mola</th><th>Planlanan</th><th>Tip</th></tr></thead><tbody>{shifts.map(x => <tr key={x.id}><td><strong>{x.name}</strong><small>{x.code}</small></td><td>{x.startTime} → {x.endTime}</td><td>{x.breakMinutes} dk</td><td>{x.plannedMinutes} dk</td><td>{x.crossesMidnight ? "Gece / Ertesi gün" : "Aynı gün"}</td></tr>)}</tbody></table></div>
    </section> : null}

    {permissions.has("attendance.assignment.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">ATAMA</span><h2>Personel Vardiya Atamaları</h2></div><strong>{assignments.length}</strong></div>
      <label className="field-label">Personel<select value={employeeId} onChange={event => void selectEmployee(event.target.value)}><option value="">Seçin</option>{companyEmployees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label>
      {permissions.has("attendance.assignment.manage") && employeeId ? <form className="inline-form" onSubmit={assignShift}><label className="field-label">Vardiya<select name="shiftId" required><option value="">Seçin</option>{shifts.filter(x => x.isActive).map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Takvim<select name="workCalendarId" required><option value="">Seçin</option>{calendars.filter(x => x.isActive).map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Başlangıç<input name="validFrom" type="date" required/></label><label className="field-label">Bitiş<input name="validUntil" type="date"/></label><label className="field-label">Not<input name="note" maxLength={1000}/></label><button className="primary-button" disabled={busy}>Vardiya Ata</button></form> : null}
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Vardiya</th><th>Takvim</th><th>Geçerlilik</th><th>Saat</th><th>Planlanan</th></tr></thead><tbody>{assignments.length === 0 ? <tr><td colSpan={5}>Vardiya ataması bulunmuyor.</td></tr> : assignments.map(x => <tr key={x.id}><td><strong>{x.shiftName}</strong><small>{x.shiftCode}{x.crossesMidnight ? " · GECE" : ""}</small></td><td>{x.calendarName}<small>{x.calendarCode}</small></td><td>{x.validFrom} → {x.validUntil ?? "Devam"}</td><td>{x.startTime} → {x.endTime}</td><td>{x.plannedMinutes} dk</td></tr>)}</tbody></table></div>
    </section> : null}
  </main>;
}
