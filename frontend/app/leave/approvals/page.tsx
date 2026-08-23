"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

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
    const note = window.prompt(approve ? "Onay notu (opsiyonel)" : "Red gerekçesi", "") ?? "";
    if (!approve && !note.trim()) { setMessage("Red işlemi için gerekçe girin."); return; }
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

  return <main className="shell">
    <a className="back" href="/leave">← İzin Merkezi</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 4 · ONAY AKIŞI</span><h1>İzin Onay Kutusu</h1><p>{message}</p><div className="session-summary"><strong>{inbox.length}</strong><span>karar bekleyen işlem</span><span>Yönetici → HR</span></div></section>

    {(permissions.has("leave.manager.approve") || permissions.has("leave.approve")) && permissions.has("leave.view") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">ACTION INBOX</span><h2>Karar Bekleyenler</h2></div><strong>{inbox.length}</strong></div>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Personel</th><th>İzin</th><th>Tarih</th><th>Adım</th><th>İşlem</th></tr></thead><tbody>{inbox.map(item => <tr key={item.approvalId}><td><strong>{item.employeeName}</strong><small>{item.employeeNo}</small></td><td>{item.leaveTypeName}<small>{item.requestedDays} gün</small></td><td>{item.startDate} → {item.endDate}</td><td>{item.stepCode === "MANAGER" ? "Yönetici" : "HR Nihai"}</td><td><div className="actions action-row"><button className="table-button" disabled={busy || !item.canDecide} onClick={() => void decide(item, true)}>Onayla</button><button className="table-button" disabled={busy || !item.canDecide} onClick={() => void decide(item, false)}>Reddet</button><button className="table-button" onClick={() => void openWorkflow(item.leaveId)}>Akış</button></div></td></tr>)}</tbody></table>{inbox.length === 0 ? <p className="muted">Karar bekleyen izin bulunmuyor.</p> : null}</div>
    </section> : null}

    {workflow ? <section className="security-grid">
      <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">AKIŞ</span><h2>{workflow.leave.employeeName}</h2></div><strong>{workflow.leave.status}</strong></div><p>{workflow.leave.leaveTypeName} · {workflow.leave.startDate} → {workflow.leave.endDate} · {workflow.leave.requestedDays} gün</p><div className="table-wrap"><table className="data-table"><thead><tr><th>Sıra</th><th>Adım</th><th>Atanan</th><th>Durum</th><th>Karar</th></tr></thead><tbody>{workflow.steps.map(step => <tr key={step.id}><td>{step.stepOrder}</td><td>{step.stepCode}</td><td>{step.assignedUsername ?? step.approverEmployeeName ?? "Rol bazlı"}</td><td>{step.status}</td><td>{step.decidedByUsername ?? "—"}<small>{step.decisionNote ?? ""}</small></td></tr>)}</tbody></table></div></article>
      <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">HISTORY</span><h2>Onay Geçmişi</h2></div><strong>{workflow.history.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Zaman</th><th>Olay</th><th>Durum</th><th>Kullanıcı</th></tr></thead><tbody>{workflow.history.map(h => <tr key={h.id}><td>{new Date(h.occurredAt).toLocaleString("tr-TR")}</td><td>{h.action}<small>{h.stepCode ?? ""}</small></td><td>{h.fromStatus ?? "—"} → {h.toStatus ?? "—"}<small>{h.note ?? ""}</small></td><td>{h.actorUsername ?? "Sistem"}</td></tr>)}</tbody></table></div></article>
    </section> : null}

    {permissions.has("leave.approver.manage") ? <section className="panel audit-panel">
      <div className="panel-heading"><div><span className="eyebrow dark">IDENTITY MAPPING</span><h2>Kullanıcı ↔ Personel Eşlemesi</h2></div><strong>{links.length}</strong></div>
      {permissions.has("system.user.view") && permissions.has("personnel.view") ? <form className="inline-form" onSubmit={setLink}><label className="field-label">Sistem Kullanıcısı<select name="userId" required><option value="">Seçin</option>{users.map(x => <option key={x.id} value={x.id}>{x.username}{x.email ? ` · ${x.email}` : ""}</option>)}</select></label><label className="field-label">Personel Kimliği<select name="employeeId" required><option value="">Seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label><button className="primary-button" disabled={busy}>Eşleştir</button></form> : <p className="muted">Eşleme düzenlemek için ayrıca kullanıcı ve personel görüntüleme yetkileri gerekir.</p>}
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Kullanıcı</th><th>Personel</th><th>Sicil</th><th>Durum</th></tr></thead><tbody>{links.map(link => <tr key={link.id}><td>{link.username}</td><td>{link.employeeName}</td><td>{link.employeeNo}</td><td>{link.isActive ? "Aktif" : "Pasif"}</td></tr>)}</tbody></table></div>
    </section> : null}
  </main>;
}
