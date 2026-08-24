"use client";

import Link from "next/link";
import { ChangeEvent, FormEvent, useEffect, useMemo, useState } from "react";
import { Icon } from "../components/Icon";
import { OperationDisclosure } from "../components/OperationDisclosure";
import { PageHeader } from "../components/PageHeader";

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
type LeaveAttachment = { id: string; leaveId: string; fileId: string; fileName: string; contentType: string; fileSizeBytes: number; description: string | null; uploadedAt: string; uploadedBy: string };

export default function LeavePage() {
  const [me, setMe] = useState<Me | null>(null);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [types, setTypes] = useState<LeaveType[]>([]);
  const [rows, setRows] = useState<LeaveRow[]>([]);
  const [selectedEmployeeId, setSelectedEmployeeId] = useState("");
  const [balances, setBalances] = useState<Balance[]>([]);
  const [attachmentsByLeave, setAttachmentsByLeave] = useState<Record<string, LeaveAttachment[]>>({});
  const [loadedAttachmentLeaves, setLoadedAttachmentLeaves] = useState<Set<string>>(new Set());
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
      const formElement = event.currentTarget;
      const form = new FormData(formElement);
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
      const type = types.find(x => x.id === created.leaveTypeId);
      setMessage(type?.attachmentRequired
        ? "İzin taslağı oluşturuldu. Bu izin türünde gönderimden önce destekleyici belge yüklemek zorunludur."
        : "İzin taslağı oluşturuldu. Gönder butonu ile bakiye ve çakışma kontrolleri çalıştırılır.");
      formElement.reset();
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
      setMessage(action === "submit" ? "İzin talebi onay akışına gönderildi; gerekli bakiye rezerve edildi." : "İzin talebi geri çekildi; rezervasyon serbest bırakıldı.");
      if (selectedEmployeeId === updated.employeeId && permissions.has("leave.balance.view")) await selectEmployee(updated.employeeId);
    } finally { setBusy(false); }
  }

  async function loadAttachments(leaveId: string) {
    if (!permissions.has("leave.attachment.view")) return;
    setBusy(true);
    try {
      const attachments = (await json<LeaveAttachment[]>(`/api/v1/leave/requests/${leaveId}/attachments`)) ?? [];
      setAttachmentsByLeave(current => ({ ...current, [leaveId]: attachments }));
      setLoadedAttachmentLeaves(current => new Set(current).add(leaveId));
      setMessage(attachments.length === 0 ? "Bu izin talebinde henüz ek bulunmuyor." : `${attachments.length} izin eki yüklendi.`);
    } finally { setBusy(false); }
  }

  async function uploadAttachment(row: LeaveRow, event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    setBusy(true);
    try {
      const form = new FormData();
      form.append("file", file);
      const response = await authFetch(`/api/v1/leave/requests/${row.id}/attachments`, { method: "POST", body: form });
      if (!response?.ok) { setMessage(await errorMessage(response, "İzin eki yüklenemedi.")); return; }
      const saved = await response.json() as LeaveAttachment;
      setAttachmentsByLeave(current => ({ ...current, [row.id]: [saved, ...(current[row.id] ?? [])] }));
      setLoadedAttachmentLeaves(current => new Set(current).add(row.id));
      setMessage("İzin eki güvenli depolama alanına kaydedildi.");
    } finally { setBusy(false); }
  }

  async function openAttachment(attachmentId: string) {
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/leave/attachments/${attachmentId}/file`);
      if (!response?.ok) { setMessage(await errorMessage(response, "İzin eki açılamadı.")); return; }
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      window.open(url, "_blank", "noopener,noreferrer");
      window.setTimeout(() => URL.revokeObjectURL(url), 60000);
    } finally { setBusy(false); }
  }

  function attachmentRequired(row: LeaveRow) { return types.find(x => x.id === row.leaveTypeId)?.attachmentRequired ?? false; }

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

  const draftCount = rows.filter(row => row.status === "DRAFT").length;
  const pendingCount = rows.filter(row => ["SUBMITTED", "PENDING_APPROVAL"].includes(row.status)).length;
  const approvedCount = rows.filter(row => row.status === "APPROVED").length;
  const canOpenApprovals = permissions.has("leave.manager.approve") || permissions.has("leave.approve") || permissions.has("leave.approver.manage");

  return <main className="page-shell">
    <PageHeader
      eyebrow="İnsan Kaynakları"
      title="İzin Yönetimi"
      description="İzin taleplerini, destekleyici belgeleri ve personel bakiyelerini tek çalışma alanından yönetin."
      status={message}
      actions={canOpenApprovals ? <Link className="secondary-button" href="/leave/approvals">Onay merkezine git <Icon name="arrow" size={15}/></Link> : null}
    />

    <section className="stat-grid" aria-label="İzin göstergeleri">
      <article className="stat-card"><span className="stat-icon"><Icon name="calendar"/></span><span className="stat-copy"><strong>{rows.length}</strong><span>Toplam talep</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{pendingCount}</strong><span>Onay bekleyen</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="box"/></span><span className="stat-copy"><strong>{draftCount}</strong><span>Taslak talep</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{approvedCount}</strong><span>Onaylanan</span></span></article>
    </section>

    <div className="content-stack">
    {permissions.has("leave.create") ? <section className="panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Yeni talep</span><h2>İzin taslağı oluşturun</h2><p>Talep önce taslak olarak kaydedilir; ardından belge ve bakiye kontrolleriyle onaya gönderilir.</p></div></div>
      <OperationDisclosure title="Yeni izin talebi başlat" description="Personel, izin türü ve tarih aralığını seçerek bir taslak oluşturun."><form className="inline-form" onSubmit={createLeave}>
        <label className="field-label">Personel<select name="employeeId" required><option value="">Seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label>
        <label className="field-label">İzin Türü<select name="leaveTypeId" required><option value="">Seçin</option>{types.filter(x => x.id).map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}{x.attachmentRequired ? " · Ek zorunlu" : ""}</option>)}</select></label>
        <label className="field-label">Başlangıç<input name="startDate" type="date" required/></label>
        <label className="field-label">Başlangıç Bölümü<select name="startDayPart" defaultValue="FULL_DAY"><option value="FULL_DAY">Tam Gün</option><option value="FIRST_HALF">İlk Yarım</option><option value="SECOND_HALF">İkinci Yarım</option></select></label>
        <label className="field-label">Bitiş<input name="endDate" type="date" required/></label>
        <label className="field-label">Bitiş Bölümü<select name="endDayPart" defaultValue="FULL_DAY"><option value="FULL_DAY">Tam Gün</option><option value="FIRST_HALF">İlk Yarım</option><option value="SECOND_HALF">İkinci Yarım</option></select></label>
        <label className="field-label">Açıklama<input name="reason" maxLength={2000}/></label>
        <button className="primary-button" disabled={busy}>{busy ? "Kaydediliyor…" : "Taslak oluştur"}</button>
      </form></OperationDisclosure>
    </section> : null}

    {permissions.has("leave.view") ? <section className="panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Talep takibi</span><h2>İzin kayıtları</h2><p>Talep durumlarını, ekleri ve kullanılabilir işlemleri birlikte izleyin.</p></div><strong>{rows.length}</strong></div>
      <div className="table-wrap responsive-table-wrap" role="region" aria-label="İzin kayıtları" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Personel</th><th>İzin</th><th>Tarih</th><th>Gün</th><th>Durum</th><th>Ekler</th><th>İşlem</th></tr></thead><tbody>
        {rows.map(row => <tr key={row.id}>
          <td data-label="Personel"><strong>{row.employeeName}</strong><small>{row.employeeNo}</small></td>
          <td data-label="İzin">{row.leaveTypeName}<small>{row.leaveTypeCode}{attachmentRequired(row) ? " · Ek zorunlu" : ""}</small></td>
          <td data-label="Tarih">{formatDate(row.startDate)} → {formatDate(row.endDate)}<small>{dayPartLabel(row.startDayPart)} / {dayPartLabel(row.endDayPart)}</small></td>
          <td data-label="Gün"><strong>{row.requestedDays}</strong></td>
          <td data-label="Durum"><span className={`status-badge ${leaveStatusClass(row.status)}`}>{leaveStatusLabel(row.status)}</span></td>
          <td data-label="Ekler"><div className="action-row">{permissions.has("leave.attachment.view") ? <button className="table-button" disabled={busy} onClick={() => void loadAttachments(row.id)}>{loadedAttachmentLeaves.has(row.id) ? `Yenile (${attachmentsByLeave[row.id]?.length ?? 0})` : "Ekleri göster"}</button> : null}{permissions.has("leave.attachment.upload") && row.status === "DRAFT" ? <label className="table-button">Ek yükle<input hidden type="file" accept="application/pdf,image/jpeg,image/png" onChange={event => void uploadAttachment(row, event)}/></label> : null}</div>{(attachmentsByLeave[row.id] ?? []).map(file => <div key={file.id}><button className="table-button document-open" disabled={busy} onClick={() => void openAttachment(file.id)}>{file.fileName}</button></div>)}</td>
          <td data-label="İşlem"><div className="action-row">{permissions.has("leave.submit") && row.status === "DRAFT" ? <button className="table-button" disabled={busy} onClick={() => void act(row, "submit")}>Onaya gönder</button> : null}{permissions.has("leave.submit") && ["DRAFT", "SUBMITTED", "PENDING_APPROVAL"].includes(row.status) ? <button className="table-button button-danger" disabled={busy} onClick={() => void act(row, "withdraw")}>Geri çek</button> : null}</div></td>
        </tr>)}
        {rows.length === 0 ? <tr><td className="empty-row" colSpan={7}>Henüz izin talebi bulunmuyor.</td></tr> : null}
      </tbody></table></div>
    </section> : null}

    {permissions.has("leave.balance.view") ? <section className="panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Bakiye yönetimi</span><h2>Personel izin bakiyesi</h2><p>Personel seçerek dönemsel hakediş ve kullanılabilir günleri görüntüleyin.</p></div><strong>{balances.length}</strong></div>
      <div className="selection-bar"><label className="field-label">Personel<select value={selectedEmployeeId} onChange={e => void selectEmployee(e.target.value)}><option value="">Personel seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label><div className="selection-context"><strong>{selectedEmployeeId ? `${balances.length} bakiye kaydı` : "Personel bekleniyor"}</strong><span>Seçim yalnız bakiye bölümünü etkiler.</span></div></div>
      {permissions.has("leave.balance.manage") && selectedEmployeeId ? <OperationDisclosure title="Hakediş veya düzeltme ekle" description="Değişiklik, seçili personelin izin bakiyesine uygulanır."><form className="inline-form" onSubmit={createEntitlement}>
        <label className="field-label">Bakiye Takipli İzin<select name="leaveTypeId" required><option value="">Seçin</option>{types.filter(x => x.balanceRequired).map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        <label className="field-label">Dönem Başlangıç<input name="periodStart" type="date" required/></label>
        <label className="field-label">Dönem Bitiş<input name="periodEnd" type="date" required/></label>
        <label className="field-label">Hakediş<input name="entitledDays" type="number" min={0} step="0.5" required/></label>
        <label className="field-label">Devir<input name="carryOverDays" type="number" min={0} step="0.5" defaultValue={0}/></label>
        <label className="field-label">Düzeltme<input name="adjustmentDays" type="number" step="0.5" defaultValue={0}/></label>
        <label className="field-label">Not<input name="note" maxLength={1000}/></label>
        <button className="primary-button" disabled={busy}>{busy ? "Kaydediliyor…" : "Hakedişi kaydet"}</button>
      </form></OperationDisclosure> : null}
      <div className="table-wrap responsive-table-wrap" role="region" aria-label="Personel izin bakiyesi" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>İzin türü</th><th>Dönem</th><th>Hakediş</th><th>Devir</th><th>Rezerve</th><th>Kullanılan</th><th>Kullanılabilir</th></tr></thead><tbody>{balances.map(x => <tr key={x.id}><td data-label="İzin türü"><strong>{x.leaveTypeName}</strong><small>{x.leaveTypeCode}</small></td><td data-label="Dönem">{formatDate(x.periodStart)} → {formatDate(x.periodEnd)}</td><td data-label="Hakediş">{x.entitledDays}</td><td data-label="Devir">{x.carryOverDays}</td><td data-label="Rezerve">{x.reservedDays}</td><td data-label="Kullanılan">{x.usedDays}</td><td data-label="Kullanılabilir"><strong className="amount-positive">{x.availableDays}</strong></td></tr>)}{balances.length === 0 ? <tr><td className="empty-row" colSpan={7}>{selectedEmployeeId ? "Seçili personel için bakiye kaydı bulunmuyor." : "Bakiye görüntülemek için personel seçin."}</td></tr> : null}</tbody></table></div>
    </section> : null}
    </div>
  </main>;
}

function leaveStatusLabel(status: string) {
  return status === "DRAFT" ? "Taslak" : status === "SUBMITTED" ? "Gönderildi" : status === "PENDING_APPROVAL" ? "Onay bekliyor" : status === "APPROVED" ? "Onaylandı" : status === "REJECTED" ? "Reddedildi" : status === "WITHDRAWN" ? "Geri çekildi" : status;
}
function leaveStatusClass(status: string) {
  return status === "APPROVED" ? "success" : status === "REJECTED" ? "danger" : ["SUBMITTED", "PENDING_APPROVAL"].includes(status) ? "warning" : "";
}
function dayPartLabel(value: string) { return value === "FULL_DAY" ? "Tam gün" : value === "FIRST_HALF" ? "İlk yarım" : value === "SECOND_HALF" ? "İkinci yarım" : value; }
function formatDate(value: string) { return new Intl.DateTimeFormat("tr-TR", { day: "2-digit", month: "short", year: "numeric" }).format(new Date(`${value}T00:00:00`)); }
