"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useActionDialog } from "../../components/ActionDialog";
import { Icon } from "../../components/Icon";
import { OperationDisclosure } from "../../components/OperationDisclosure";
import { PageHeader } from "../../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type Permission = { code: string };
type Me = { permissions: Permission[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type InboxItem = { approvalId: string; approvalVersion: number; leaveId: string; leaveVersion: number; employeeId: string; employeeNo: string; employeeName: string; companyId: string; leaveTypeCode: string; leaveTypeName: string; startDate: string; endDate: string; requestedDays: number; stepCode: string; status: string; canDecide: boolean };
type ApprovalStep = { id: string; leaveId: string; stepOrder: number; stepCode: string; approverEmployeeId: string | null; approverEmployeeName: string | null; assignedUserId: string | null; assignedUsername: string | null; status: string; decidedByUserId: string | null; decidedByUsername: string | null; decidedAt: string | null; decisionNote: string | null; version: number };
type ApprovalHistory = { id: string; action: string; stepCode: string | null; fromStatus: string | null; toStatus: string | null; actorUsername: string | null; occurredAt: string; note: string | null };
type LeaveSummary = { id: string; employeeNo: string; employeeName: string; leaveTypeName: string; startDate: string; endDate: string; requestedDays: number; status: string; version: number };
type Workflow = { leave: LeaveSummary; steps: ApprovalStep[]; history: ApprovalHistory[] };
type Employee = { id: string; employeeNo: string; firstName: string; lastName: string; status: string };
type EmployeePage = { items: Employee[]; totalCount: number };
type User = { id: string; username: string; email: string | null; isActive: boolean };
type Link = { id: string; userId: string; username: string; employeeId: string; employeeNo: string; employeeName: string; companyId: string; isActive: boolean; version: number };

export default function LeaveApprovalsPage() {
  const [me, setMe] = useState<Me | null>(null);
  const [inbox, setInbox] = useState<InboxItem[]>([]);
  const [workflow, setWorkflow] = useState<Workflow | null>(null);
  const [links, setLinks] = useState<Link[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [message, setMessage] = useState("Onay kutusu yükleniyor…");
  const [busy, setBusy] = useState(false);
  const { ask, dialog } = useActionDialog();

  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);

  useEffect(() => { void initialize(); }, []);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const codes = new Set(current.permissions.map(x => x.code));
    const canApprove = codes.has("leave.manager.approve") || codes.has("leave.approve");
    const [inboxRows, linkRows, userRows, employeeRows] = await Promise.all([
      canApprove && codes.has("leave.view") ? json<InboxItem[]>("/api/v1/leave/approvals/inbox") : Promise.resolve(null),
      codes.has("leave.approver.manage") ? json<Link[]>("/api/v1/leave/approver-links") : Promise.resolve(null),
      codes.has("leave.approver.manage") && codes.has("system.user.view") ? json<User[]>("/api/v1/security/users") : Promise.resolve(null),
      codes.has("leave.approver.manage") && codes.has("personnel.view") ? json<EmployeePage>("/api/v1/personnel/employees?status=ACTIVE&pageSize=100") : Promise.resolve(null),
    ]);
    setInbox(inboxRows ?? []);
    setLinks(linkRows ?? []);
    setUsers((userRows ?? []).filter(x => x.isActive));
    setEmployees(employeeRows?.items ?? []);
    setMessage("Onay kutusu güncel.");
  }

  async function reloadInbox() {
    const rows = await json<InboxItem[]>("/api/v1/leave/approvals/inbox");
    setInbox(rows ?? []);
  }

  async function openWorkflow(leaveId: string) {
    const detail = await json<Workflow>(`/api/v1/leave/requests/${leaveId}/workflow`);
    if (!detail) { setMessage("Onay akışı alınamadı."); return; }
    setWorkflow(detail);
  }

  async function decide(item: InboxItem, approve: boolean) {
    const result = await ask({
      title: approve ? "İzin talebini onaylayın" : "İzin talebini reddedin",
      description: `${item.employeeName} için ${item.requestedDays} günlük ${item.leaveTypeName.toLocaleLowerCase("tr-TR")} talebi.`,
      confirmLabel: approve ? "Talebi onayla" : "Talebi reddet",
      tone: approve ? "success" : "danger",
      fields: [{ name: "note", label: approve ? "Onay notu (isteğe bağlı)" : "Red gerekçesi", required: !approve, multiline: true, placeholder: approve ? "Kararınıza kısa bir not ekleyebilirsiniz." : "Talebin neden reddedildiğini açıklayın." }],
    });
    if (!result) return;
    const note = result.note.trim();
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/leave/requests/${item.leaveId}/approvals/${item.approvalId}/decision`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ approvalVersion: item.approvalVersion, leaveVersion: item.leaveVersion, approve, note: note.trim() || null }),
      });
      if (!response?.ok) { setMessage(await errorMessage(response, "Onay kararı kaydedilemedi.")); return; }
      const detail = await response.json() as Workflow;
      setWorkflow(detail);
      setMessage(approve ? "Onay kararı kaydedildi." : "Red kararı kaydedildi; rezerve bakiye serbest bırakıldı.");
      await reloadInbox();
    } finally { setBusy(false); }
  }

  async function setLink(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const userId = String(form.get("userId") ?? "");
      const employeeId = String(form.get("employeeId") ?? "");
      const response = await authFetch(`/api/v1/leave/approver-links/${userId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ employeeId }),
      });
      if (!response?.ok) { setMessage(await errorMessage(response, "Kullanıcı-personel eşlemesi kaydedilemedi.")); return; }
      const saved = await response.json() as Link;
      setLinks(current => [saved, ...current.filter(x => x.userId !== saved.userId && x.employeeId !== saved.employeeId)]);
      setMessage("Kullanıcı-personel eşlemesi kaydedildi. Yeni yönetici onayları bu kimliği kullanacak.");
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

  const managerCount = inbox.filter(item => item.stepCode === "MANAGER").length;
  const hrCount = inbox.filter(item => item.stepCode !== "MANAGER").length;

  return <main className="page-shell">
    <PageHeader eyebrow="İzin Yönetimi" title="İzin Onay Kutusu" description="Yönetici ve insan kaynakları kararlarını, onay akışını ve kimlik eşleşmelerini yönetin." status={message} actions={<a className="secondary-button" href="/leave">İzin merkezine dön <Icon name="arrow" size={15}/></a>}/>

    <section className="stat-grid" aria-label="İzin onay göstergeleri">
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{inbox.length}</strong><span>Karar bekleyen</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{managerCount}</strong><span>Yönetici adımı</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="settings"/></span><span className="stat-copy"><strong>{hrCount}</strong><span>İK nihai adımı</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="plug"/></span><span className="stat-copy"><strong>{links.length}</strong><span>Kimlik eşleşmesi</span></span></article>
    </section>

    <div className="content-stack">
    {(permissions.has("leave.manager.approve") || permissions.has("leave.approve")) && permissions.has("leave.view") ? <section className="panel attention-panel warning">
      <div className="panel-heading"><div><span className="page-eyebrow">Onay kutusu</span><h2>Karar bekleyen izinler</h2><p>Yetkiniz kapsamındaki talepleri inceleyip karar verin.</p></div><strong>{inbox.length}</strong></div>
      <div className="table-wrap responsive-table-wrap" role="region" aria-label="Karar bekleyen izinler" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Personel</th><th>İzin</th><th>Tarih</th><th>Adım</th><th>İşlem</th></tr></thead><tbody>{inbox.map(item => <tr key={item.approvalId}><td data-label="Personel"><strong>{item.employeeName}</strong><small>{item.employeeNo}</small></td><td data-label="İzin">{item.leaveTypeName}<small>{item.requestedDays} gün</small></td><td data-label="Tarih">{formatDate(item.startDate)} → {formatDate(item.endDate)}</td><td data-label="Adım"><span className="status-badge warning">{item.stepCode === "MANAGER" ? "Yönetici" : "İK nihai"}</span></td><td data-label="İşlem"><div className="action-row"><button className="table-button button-success" disabled={busy || !item.canDecide} onClick={() => void decide(item, true)}>Onayla</button><button className="table-button button-danger" disabled={busy || !item.canDecide} onClick={() => void decide(item, false)}>Reddet</button><button className="table-button" onClick={() => void openWorkflow(item.leaveId)}>Akışı aç</button></div></td></tr>)}{inbox.length === 0 ? <tr><td className="empty-row" colSpan={5}>Karar bekleyen izin bulunmuyor.</td></tr> : null}</tbody></table></div>
    </section> : null}

    {workflow ? <section className="security-grid">
      <article className="panel"><div className="panel-heading"><div><span className="page-eyebrow">Seçili akış</span><h2>{workflow.leave.employeeName}</h2><p>{workflow.leave.leaveTypeName} · {formatDate(workflow.leave.startDate)} → {formatDate(workflow.leave.endDate)} · {workflow.leave.requestedDays} gün</p></div><span className={`status-badge ${statusClass(workflow.leave.status)}`}>{statusLabel(workflow.leave.status)}</span></div><div className="table-wrap responsive-table-wrap" role="region" aria-label="Seçili izin onay akışı" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Sıra</th><th>Adım</th><th>Atanan</th><th>Durum</th><th>Karar</th></tr></thead><tbody>{workflow.steps.map(step => <tr key={step.id}><td data-label="Sıra">{step.stepOrder}</td><td data-label="Adım">{step.stepCode === "MANAGER" ? "Yönetici" : "İK nihai"}</td><td data-label="Atanan">{step.assignedUsername ?? step.approverEmployeeName ?? "Rol bazlı"}</td><td data-label="Durum"><span className={`status-badge ${statusClass(step.status)}`}>{statusLabel(step.status)}</span></td><td data-label="Karar">{step.decidedByUsername ?? "—"}<small>{step.decisionNote ?? ""}</small></td></tr>)}</tbody></table></div></article>
      <article className="panel"><div className="panel-heading"><div><span className="page-eyebrow">İşlem izi</span><h2>Onay geçmişi</h2><p>Akışta gerçekleşen tüm karar ve durum değişiklikleri.</p></div><strong>{workflow.history.length}</strong></div><div className="table-wrap responsive-table-wrap" role="region" aria-label="İzin onay geçmişi" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Zaman</th><th>Olay</th><th>Durum</th><th>Kullanıcı</th></tr></thead><tbody>{workflow.history.map(h => <tr key={h.id}><td data-label="Zaman">{new Date(h.occurredAt).toLocaleString("tr-TR")}</td><td data-label="Olay">{historyLabel(h.action)}<small>{h.stepCode ?? ""}</small></td><td data-label="Durum">{statusLabel(h.fromStatus ?? "—")} → {statusLabel(h.toStatus ?? "—")}<small>{h.note ?? ""}</small></td><td data-label="Kullanıcı">{h.actorUsername ?? "Sistem"}</td></tr>)}</tbody></table></div></article>
    </section> : null}

    {permissions.has("leave.approver.manage") ? <section className="panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Kimlik eşlemesi</span><h2>Kullanıcı ↔ personel eşlemesi</h2><p>Yönetici onaylarının doğru personel kimliğiyle eşleşmesini sağlayın.</p></div><strong>{links.length}</strong></div>
      <div className="table-wrap responsive-table-wrap" role="region" aria-label="Kullanıcı personel eşleşmeleri" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Kullanıcı</th><th>Personel</th><th>Sicil</th><th>Durum</th></tr></thead><tbody>{links.map(link => <tr key={link.id}><td data-label="Kullanıcı"><strong>{link.username}</strong></td><td data-label="Personel">{link.employeeName}</td><td data-label="Sicil">{link.employeeNo}</td><td data-label="Durum"><span className={`status-badge ${link.isActive ? "success" : ""}`}>{link.isActive ? "Aktif" : "Pasif"}</span></td></tr>)}{links.length === 0 ? <tr><td className="empty-row" colSpan={4}>Henüz kullanıcı-personel eşlemesi bulunmuyor.</td></tr> : null}</tbody></table></div>
      {permissions.has("system.user.view") && permissions.has("personnel.view") ? <OperationDisclosure title="Yeni kullanıcı-personel eşlemesi" description="Bir kullanıcı ve personel yalnız bir aktif eşleşmede yer alabilir."><form className="inline-form" onSubmit={setLink}><label className="field-label">Sistem kullanıcısı<select name="userId" required><option value="">Seçin</option>{users.map(x => <option key={x.id} value={x.id}>{x.username}{x.email ? ` · ${x.email}` : ""}</option>)}</select></label><label className="field-label">Personel kimliği<select name="employeeId" required><option value="">Seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label><button className="primary-button" disabled={busy}>Eşleştir</button></form></OperationDisclosure> : <div className="notice">Eşleme düzenlemek için kullanıcı ve personel görüntüleme yetkileri gerekir.</div>}
    </section> : null}
    </div>
    {dialog}
  </main>;
}

function formatDate(value: string) { return new Intl.DateTimeFormat("tr-TR", { day: "2-digit", month: "short", year: "numeric" }).format(new Date(`${value}T00:00:00`)); }
function statusLabel(value: string) { return value === "DRAFT" ? "Taslak" : value === "SUBMITTED" ? "Gönderildi" : value === "PENDING" ? "Bekliyor" : value === "PENDING_APPROVAL" ? "Onay bekliyor" : value === "APPROVED" ? "Onaylandı" : value === "REJECTED" ? "Reddedildi" : value === "WITHDRAWN" ? "Geri çekildi" : value === "COMPLETED" ? "Tamamlandı" : value === "SKIPPED" ? "Atlandı" : value; }
function statusClass(value: string) { return ["APPROVED", "COMPLETED"].includes(value) ? "success" : value === "REJECTED" ? "danger" : value.startsWith("PENDING") ? "warning" : ""; }
function historyLabel(value: string) { return value === "SUBMITTED" ? "Gönderildi" : value === "APPROVED" ? "Onaylandı" : value === "REJECTED" ? "Reddedildi" : value === "WITHDRAWN" ? "Geri çekildi" : value; }
