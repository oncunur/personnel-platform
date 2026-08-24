"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { useActionDialog } from "../../components/ActionDialog";
import { Icon } from "../../components/Icon";
import { PageHeader } from "../../components/PageHeader";

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
  const { ask, dialog } = useActionDialog();
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
    const result = await ask({
      title: "Fazla mesai talebi oluşturun",
      description: `${formatDate(daily.attendanceDate)} tarihinde ${daily.overtimeCandidateMinutes} dakika fazla mesai adayı hesaplandı.`,
      confirmLabel: "Talebi oluştur",
      fields: [
        { name: "requestedMinutes", label: "Talep edilecek dakika", type: "number", initialValue: String(daily.overtimeCandidateMinutes), min: 1, max: daily.overtimeCandidateMinutes, step: 1, required: true },
        { name: "reason", label: "Fazla mesai gerekçesi (isteğe bağlı)", multiline: true },
      ],
    });
    if (!result) return;
    const requestedMinutes = Number(result.requestedMinutes);
    const reason = result.reason.trim() || null;
    setBusy(true);
    try {
      const response = await authFetch("/api/v1/attendance/overtime", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ dailyAttendanceId: daily.id, requestedMinutes, reason }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Fazla mesai talebi oluşturulamadı.")); return; }
      setMessage("Fazla mesai talebi yönetici onayına gönderildi.");
      await refreshQueues();
    } finally { setBusy(false); }
  }

  async function decide(item: Inbox, approve: boolean) {
    const isHrApproval = approve && item.status === "PENDING_HR";
    const result = await ask({
      title: approve ? "Fazla mesaiyi onaylayın" : "Fazla mesaiyi reddedin",
      description: `${item.employeeName} için ${formatDate(item.attendanceDate)} tarihli ${item.requestedMinutes} dakikalık talep.`,
      confirmLabel: approve ? "Kararı onayla" : "Talebi reddet",
      tone: approve ? "success" : "danger",
      fields: [
        ...(isHrApproval ? [{ name: "approvedMinutes", label: "Nihai onaylanacak dakika", type: "number" as const, initialValue: String(item.requestedMinutes), min: 1, max: item.requestedMinutes, step: 1, required: true }] : []),
        { name: "note", label: approve ? "Onay notu (isteğe bağlı)" : "Red açıklaması (isteğe bağlı)", multiline: true },
      ],
    });
    if (!result) return;
    const approvedMinutes = isHrApproval ? Number(result.approvedMinutes) : null;
    const note = result.note.trim() || null;
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/attendance/overtime/${item.id}/decision`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ approve, approvedMinutes, note, version: item.version }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Karar kaydedilemedi.")); return; }
      setMessage(approve ? (item.status === "PENDING_MANAGER" ? "Yönetici onayı tamamlandı; talep HR onayına geçti." : "Fazla mesai nihai olarak onaylandı.") : "Fazla mesai talebi reddedildi.");
      await refreshQueues();
    } finally { setBusy(false); }
  }

  async function cancel(row: Overtime) {
    const confirmed = await ask({
      title: "Fazla mesai talebi iptal edilsin mi?",
      description: `${row.employeeName} için ${formatDate(row.attendanceDate)} tarihli ${row.requestedMinutes} dakikalık talep iptal edilecek.`,
      confirmLabel: "Talebi iptal et",
      tone: "danger",
    });
    if (!confirmed) return;
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

  const approvedCount = rows.filter(row => row.status === "APPROVED").length;
  const pendingCount = rows.filter(row => row.status.startsWith("PENDING")).length;
  const approvedMinutes = rows.reduce((total, row) => total + row.approvedMinutes, 0);

  return <main className="page-shell">
    <PageHeader eyebrow="Puantaj ve Vardiya" title="Fazla Mesai Merkezi" description="Puantaj adaylarını talebe dönüştürün, yönetici ve İK kararlarını izleyin." status={message} actions={<Link className="secondary-button" href="/attendance/daily">Günlük puantaja dön <Icon name="arrow" size={15}/></Link>}/>

    <section className="stat-grid" aria-label="Fazla mesai göstergeleri">
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{rows.length}</strong><span>Toplam talep</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="calendar"/></span><span className="stat-copy"><strong>{pendingCount}</strong><span>Onay bekleyen</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{approvedCount}</strong><span>Onaylanan</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="chart"/></span><span className="stat-copy"><strong>{approvedMinutes} dk</strong><span>Toplam onay</span></span></article>
    </section>

    <div className="content-stack">
    {permissions.has("attendance.overtime.request") && permissions.has("attendance.daily.view") ? <section className="panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Adaydan talebe</span><h2>Puantaj fazla mesai adayları</h2><p>Seçili personelin bu ay oluşan fazla mesai adaylarını talebe dönüştürün.</p></div><strong>{dailyRows.length}</strong></div>
      <div className="selection-bar"><label className="field-label">Personel<select value={employeeId} onChange={e => void selectEmployee(e.target.value)}><option value="">Personel seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label><div className="selection-context"><strong>{employeeId ? `${dailyRows.length} aday kayıt` : "Personel bekleniyor"}</strong><span>Ay başından bugüne kadar olan adaylar gösterilir.</span></div></div>
      <div className="table-wrap responsive-table-wrap" role="region" aria-label="Fazla mesai adayları" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Tarih</th><th>FM adayı</th><th>Puantaj işlemi</th><th>İşlem</th></tr></thead><tbody>{dailyRows.map(x => <tr key={x.id}><td data-label="Tarih">{formatDate(x.attendanceDate)}</td><td data-label="FM adayı"><strong>{x.overtimeCandidateMinutes} dk</strong></td><td data-label="Puantaj işlemi"><span className={`status-badge ${x.processingStatus === "REVIEW_REQUIRED" ? "danger" : "success"}`}>{processingLabel(x.processingStatus)}</span></td><td data-label="İşlem"><button className="table-button button-success" disabled={busy} onClick={() => void createRequest(x)}>Talep oluştur</button></td></tr>)}{dailyRows.length === 0 ? <tr><td className="empty-row" colSpan={4}>{employeeId ? "Seçili personelin bu ay için fazla mesai adayı bulunmuyor." : "Adayları görüntülemek için personel seçin."}</td></tr> : null}</tbody></table></div>
    </section> : null}

    {(permissions.has("attendance.overtime.manager.approve") || permissions.has("attendance.overtime.hr.approve")) ? <section className="panel attention-panel warning">
      <div className="panel-heading"><div><span className="page-eyebrow">Onay kutusu</span><h2>Bekleyen fazla mesailer</h2><p>Yetki adımınıza ulaşan talepler için karar verin.</p></div><strong>{inbox.length}</strong></div>
      <div className="table-wrap responsive-table-wrap" role="region" aria-label="Bekleyen fazla mesai onayları" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Personel</th><th>Tarih</th><th>Aday</th><th>Talep</th><th>Adım</th><th>İşlem</th></tr></thead><tbody>{inbox.map(x => <tr key={x.id}><td data-label="Personel"><strong>{x.employeeName}</strong><small>{x.employeeNo}</small></td><td data-label="Tarih">{formatDate(x.attendanceDate)}</td><td data-label="Aday">{x.candidateMinutes} dk</td><td data-label="Talep"><strong>{x.requestedMinutes} dk</strong></td><td data-label="Adım"><span className="status-badge warning">{x.status === "PENDING_MANAGER" ? "Yönetici" : "İK nihai"}</span></td><td data-label="İşlem"><div className="action-row"><button className="table-button button-success" disabled={busy || !x.canDecide} onClick={() => void decide(x, true)}>Onayla</button><button className="table-button button-danger" disabled={busy || !x.canDecide} onClick={() => void decide(x, false)}>Reddet</button></div></td></tr>)}{inbox.length === 0 ? <tr><td className="empty-row" colSpan={6}>Bekleyen fazla mesai onayı yok.</td></tr> : null}</tbody></table></div>
    </section> : null}

    {permissions.has("attendance.overtime.view") ? <section className="panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Talep geçmişi</span><h2>Fazla mesai talepleri</h2><p>Taleplerin aday, istenen ve nihai onaylanan dakika karşılaştırması.</p></div><strong>{rows.length}</strong></div>
      <div className="table-wrap responsive-table-wrap" role="region" aria-label="Fazla mesai talep geçmişi" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Personel</th><th>Tarih</th><th>Kaynak</th><th>Aday</th><th>Talep</th><th>Onaylanan</th><th>Durum</th><th>İşlem</th></tr></thead><tbody>{rows.map(x => <tr key={x.id}><td data-label="Personel"><strong>{x.employeeName}</strong><small>{x.employeeNo}</small></td><td data-label="Tarih">{formatDate(x.attendanceDate)}</td><td data-label="Kaynak">Günlük v{x.sourceDailyVersion}</td><td data-label="Aday">{x.candidateMinutes} dk</td><td data-label="Talep">{x.requestedMinutes} dk</td><td data-label="Onaylanan"><strong>{x.approvedMinutes} dk</strong></td><td data-label="Durum"><span className={`status-badge ${overtimeStatusClass(x.status)}`}>{overtimeStatusLabel(x.status)}</span></td><td data-label="İşlem">{permissions.has("attendance.overtime.request") && x.status === "PENDING_MANAGER" ? <button className="table-button button-danger" disabled={busy} onClick={() => void cancel(x)}>İptal et</button> : "—"}</td></tr>)}{rows.length === 0 ? <tr><td className="empty-row" colSpan={8}>Fazla mesai talebi bulunmuyor.</td></tr> : null}</tbody></table></div>
    </section> : null}
    </div>
    {dialog}
  </main>;
}

function formatDate(value: string) { return new Intl.DateTimeFormat("tr-TR", { day: "2-digit", month: "short", year: "numeric" }).format(new Date(`${value}T00:00:00`)); }
function processingLabel(value: string) { return value === "REVIEW_REQUIRED" ? "Kontrol gerekli" : value === "CALCULATED" ? "Hesaplandı" : value; }
function overtimeStatusLabel(value: string) { return value === "PENDING_MANAGER" ? "Yönetici onayı" : value === "PENDING_HR" ? "İK onayı" : value === "APPROVED" ? "Onaylandı" : value === "REJECTED" ? "Reddedildi" : value === "CANCELLED" ? "İptal edildi" : value; }
function overtimeStatusClass(value: string) { return value === "APPROVED" ? "success" : ["REJECTED", "CANCELLED"].includes(value) ? "danger" : value.startsWith("PENDING") ? "warning" : ""; }
