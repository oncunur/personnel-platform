"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type Permission = { code: string };
type Me = { permissions: Permission[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type Employee = { id: string; employeeNo: string; firstName: string; lastName: string; status: string; companyId: string };
type EmployeePage = { items: Employee[]; totalCount: number };
type LeaveType = { id: string; code: string; name: string; isPaid: boolean; balanceRequired: boolean; allowHalfDay: boolean; attachmentRequired: boolean };
type LeaveRow = { id: string; employeeId: string; employeeNo: string; employeeName: string; leaveTypeId: string; leaveTypeCode: string; leaveTypeName: string; startDate: string; endDate: string; startDayPart: string; endDayPart: string; requestedDays: number; reason: string | null; status: string; version: number };
type LeavePage = { items: LeaveRow[]; totalCount: number };
type Balance = { id: string; employeeId: string; leaveTypeId: string; leaveTypeCode: string; leaveTypeName: string; periodStart: string; periodEnd: string; entitledDays: number; carryOverDays: number; adjustmentDays: number; reservedDays: number; usedDays: number; availableDays: number; version: number };

export default function LeavePage() {
  const [me, setMe] = useState<Me | null>(null);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [types, setTypes] = useState<LeaveType[]>([]);
  const [rows, setRows] = useState<LeaveRow[]>([]);
  const [selectedEmployeeId, setSelectedEmployeeId] = useState("");
  const [balances, setBalances] = useState<Balance[]>([]);
  const [message, setMessage] = useState("İzin merkezi yükleniyor…");
  const [busy, setBusy] = useState(false);

  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);

  useEffect(() => { void initialize(); }, []);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const codes = new Set(current.permissions.map(x => x.code));
    const [typeRows, employeeRows, leaveRows] = await Promise.all([
      codes.has("leave.type.view") ? json<LeaveType[]>("/api/v1/leave/types") : Promise.resolve(null),
      codes.has("personnel.view") ? json<EmployeePage>("/api/v1/personnel/employees?status=ACTIVE&pageSize=100") : Promise.resolve(null),
      codes.has("leave.view") ? json<LeavePage>("/api/v1/leave/requests?pageSize=100") : Promise.resolve(null),
    ]);
    setTypes(typeRows ?? []);
    setEmployees(employeeRows?.items ?? []);
    setRows(leaveRows?.items ?? []);
    setMessage("İzin merkezi güncel.");
  }

  async function selectEmployee(employeeId: string) {
    setSelectedEmployeeId(employeeId);
    if (!employeeId || !permissions.has("leave.balance.view")) { setBalances([]); return; }
    setBalances((await json<Balance[]>(`/api/v1/leave/employees/${employeeId}/balances`)) ?? []);
  }

  async function createLeave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const body = {
        employeeId: form.get("employeeId"),
        leaveTypeId: form.get("leaveTypeId"),
        startDate: form.get("startDate"),
        endDate: form.get("endDate"),
        startDayPart: form.get("startDayPart"),
        endDayPart: form.get("endDayPart"),
        reason: form.get("reason") || null,
      };
      const response = await authFetch("/api/v1/leave/requests", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!response?.ok) { setMessage(await errorMessage(response, "İzin taslağı oluşturulamadı.")); return; }
      const created = await response.json() as LeaveRow;
      setRows(current => [created, ...current]);
      setMessage("İzin taslağı oluşturuldu. Gönder butonu ile bakiye ve çakışma kontrolleri çalıştırılır.");
      event.currentTarget.reset();
    } finally { setBusy(false); }
  }

  async function createEntitlement(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selectedEmployeeId) { setMessage("Önce personel seçin."); return; }
    setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const body = {
        leaveTypeId: form.get("leaveTypeId"),
        periodStart: form.get("periodStart"),
        periodEnd: form.get("periodEnd"),
        entitledDays: Number(form.get("entitledDays") || 0),
        carryOverDays: Number(form.get("carryOverDays") || 0),
        adjustmentDays: Number(form.get("adjustmentDays") || 0),
        note: form.get("note") || null,
      };
      const response = await authFetch(`/api/v1/leave/employees/${selectedEmployeeId}/entitlements`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Hakediş kaydedilemedi.")); return; }
      const saved = await response.json() as Balance;
      setBalances(current => [saved, ...current.filter(x => x.id !== saved.id)]);
      setMessage("Hakediş ve bakiye güncellendi.");
    } finally { setBusy(false); }
  }

  async function act(row: LeaveRow, action: "submit" | "withdraw") {
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/leave/requests/${row.id}/${action}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: row.version }) });
      if (!response?.ok) { setMessage(await errorMessage(response, action === "submit" ? "İzin gönderilemedi." : "İzin geri çekilemedi.")); return; }
      const updated = await response.json() as LeaveRow;
      setRows(current => current.map(x => x.id === updated.id ? updated : x));
      setMessage(action === "submit" ? "İzin talebi gönderildi; gerekli bakiye rezerve edildi." : "İzin talebi geri çekildi; rezervasyon serbest bırakıldı.");
      if (selectedEmployeeId === updated.employeeId && permissions.has("leave.balance.view")) await selectEmployee(updated.employeeId);
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
  async function errorMessage(response: Response, fallback: string) { const body = await response.json().catch(() => null) as { error?: { message?: string } } | null; return body?.error?.message ?? fallback; }

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 4 · İZİN YÖNETİMİ</span><h1>İzin Merkezi</h1><p>{message}</p></section>

    {permissions.has("leave.create") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">YENİ TALEP</span><h2>İzin Taslağı</h2></div></div>
      <form className="inline-form" onSubmit={createLeave}>
        <label className="field-label">Personel<select name="employeeId" required><option value="">Seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label>
        <label className="field-label">İzin Türü<select name="leaveTypeId" required><option value="">Seçin</option>{types.filter(x => x.id).map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
        <label className="field-label">Başlangıç<input name="startDate" type="date" required/></label>
        <label className="field-label">Başlangıç Bölümü<select name="startDayPart" defaultValue="FULL_DAY"><option value="FULL_DAY">Tam Gün</option><option value="FIRST_HALF">İlk Yarım</option><option value="SECOND_HALF">İkinci Yarım</option></select></label>
        <label className="field-label">Bitiş<input name="endDate" type="date" required/></label>
        <label className="field-label">Bitiş Bölümü<select name="endDayPart" defaultValue="FULL_DAY"><option value="FULL_DAY">Tam Gün</option><option value="FIRST_HALF">İlk Yarım</option><option value="SECOND_HALF">İkinci Yarım</option></select></label>
        <label className="field-label">Açıklama<input name="reason" maxLength={2000}/></label>
        <button className="primary-button" disabled={busy}>Taslak Oluştur</button>
      </form>
    </section> : null}

    {permissions.has("leave.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">TALEPLER</span><h2>İzin Kayıtları</h2></div><strong>{rows.length}</strong></div>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Personel</th><th>İzin</th><th>Tarih</th><th>Gün</th><th>Durum</th><th></th></tr></thead><tbody>{rows.map(row => <tr key={row.id}><td><strong>{row.employeeName}</strong><small>{row.employeeNo}</small></td><td>{row.leaveTypeName}<small>{row.leaveTypeCode}</small></td><td>{row.startDate} → {row.endDate}<small>{row.startDayPart} / {row.endDayPart}</small></td><td>{row.requestedDays}</td><td>{row.status}</td><td><div className="actions action-row">{permissions.has("leave.submit") && row.status === "DRAFT" ? <button className="table-button" disabled={busy} onClick={() => void act(row, "submit")}>Gönder</button> : null}{permissions.has("leave.submit") && ["DRAFT","SUBMITTED","PENDING_APPROVAL"].includes(row.status) ? <button className="table-button" disabled={busy} onClick={() => void act(row, "withdraw")}>Geri Çek</button> : null}</div></td></tr>)}</tbody></table></div>
    </section> : null}

    {permissions.has("leave.balance.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">BAKİYE</span><h2>Personel İzin Bakiyesi</h2></div><strong>{balances.length}</strong></div>
      <label className="field-label">Personel<select value={selectedEmployeeId} onChange={e => void selectEmployee(e.target.value)}><option value="">Seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label>
      {permissions.has("leave.balance.manage") && selectedEmployeeId ? <form className="inline-form" onSubmit={createEntitlement}>
        <label className="field-label">Bakiye Takipli İzin<select name="leaveTypeId" required><option value="">Seçin</option>{types.filter(x => x.balanceRequired).map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        <label className="field-label">Dönem Başlangıç<input name="periodStart" type="date" required/></label>
        <label className="field-label">Dönem Bitiş<input name="periodEnd" type="date" required/></label>
        <label className="field-label">Hakediş<input name="entitledDays" type="number" min={0} step="0.5" required/></label>
        <label className="field-label">Devir<input name="carryOverDays" type="number" min={0} step="0.5" defaultValue={0}/></label>
        <label className="field-label">Düzeltme<input name="adjustmentDays" type="number" step="0.5" defaultValue={0}/></label>
        <label className="field-label">Not<input name="note" maxLength={1000}/></label>
        <button className="primary-button" disabled={busy}>Hakedişi Kaydet</button>
      </form> : null}
      <div className="table-wrap"><table className="data-table"><thead><tr><th>İzin Türü</th><th>Dönem</th><th>Hakediş</th><th>Devir</th><th>Rezerve</th><th>Kullanılan</th><th>Kullanılabilir</th></tr></thead><tbody>{balances.map(x => <tr key={x.id}><td>{x.leaveTypeName}</td><td>{x.periodStart} → {x.periodEnd}</td><td>{x.entitledDays}</td><td>{x.carryOverDays}</td><td>{x.reservedDays}</td><td>{x.usedDays}</td><td><strong>{x.availableDays}</strong></td></tr>)}</tbody></table></div>
    </section> : null}
  </main>;
}
