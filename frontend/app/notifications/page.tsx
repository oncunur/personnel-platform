"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type Permission = { code: string };
type Me = { permissions: Permission[] };
type Company = { id: string; code: string; name: string };
type User = { id: string; username: string; isActive: boolean };
type Role = { id: string; code: string; name: string };
type Template = { id: string; companyId: string; code: string; name: string; titleTemplate: string; bodyTemplate: string; deepLinkTemplate: string; isActive: boolean; version: number };
type Rule = { id: string; companyId: string; code: string; name: string; sourceModule: string; eventType: string; priority: string; recipientKind: string; recipientUserId: string | null; recipientUsername: string | null; recipientRoleId: string | null; recipientRoleCode: string | null; templateId: string; templateCode: string; escalateAfterMinutes: number | null; escalationRecipientKind: string | null; escalationUserId: string | null; escalationUsername: string | null; escalationRoleId: string | null; escalationRoleCode: string | null; isActive: boolean; version: number };
type NotificationRow = { id: string; companyId: string; sourceModule: string; sourceEventType: string; priority: string; title: string; body: string; deepLink: string; status: string; dueAt: string | null; snoozedUntil: string | null; escalationLevel: number; createdAt: string; version: number };
type Center = { criticalCount: number; pendingCount: number; overdueCount: number; critical: NotificationRow[]; pending: NotificationRow[]; overdue: NotificationRow[] };
type RunResult = { sourceEvents: number; ruleMatches: number; created: number; duplicates: number; escalated: number };
type AuthResponse = { accessToken: string };

export default function NotificationsPage() {
  const [me, setMe] = useState<Me | null>(null);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [templates, setTemplates] = useState<Template[]>([]);
  const [rules, setRules] = useState<Rule[]>([]);
  const [notifications, setNotifications] = useState<NotificationRow[]>([]);
  const [center, setCenter] = useState<Center | null>(null);
  const [companyId, setCompanyId] = useState("");
  const [message, setMessage] = useState("Notification Center yükleniyor…");
  const [busy, setBusy] = useState(false);
  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);

  useEffect(() => { void initialize(); }, []);
  useEffect(() => { if (me) void reload(companyId); }, [companyId]);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const codes = new Set(current.permissions.map(x => x.code));
    const [companyRows, userRows, roleRows] = await Promise.all([
      codes.has("organization.company.view") ? json<Company[]>("/api/v1/organization/companies") : Promise.resolve([]),
      codes.has("system.user.view") ? json<User[]>("/api/v1/security/users") : Promise.resolve([]),
      codes.has("system.role.view") ? json<Role[]>("/api/v1/security/roles") : Promise.resolve([]),
    ]);
    const cs = companyRows ?? []; setCompanies(cs); setUsers(userRows ?? []); setRoles(roleRows ?? []);
    if (cs.length) setCompanyId(cs[0].id); else await reload("");
    setMessage("Notification Center hazır.");
  }

  async function reload(cid = companyId) {
    const suffix = cid ? `?companyId=${cid}` : "";
    const [templateRows, ruleRows, notificationRows, actionCenter] = await Promise.all([
      permissions.has("notification.rule.view") ? json<Template[]>(`/api/v1/notifications/templates${suffix}`) : Promise.resolve([]),
      permissions.has("notification.rule.view") ? json<Rule[]>(`/api/v1/notifications/rules${suffix}`) : Promise.resolve([]),
      permissions.has("notification.view") ? json<NotificationRow[]>(`/api/v1/notifications/${suffix}`) : Promise.resolve([]),
      permissions.has("notification.view") ? json<Center>(`/api/v1/notifications/action-center${suffix}`) : Promise.resolve(null),
    ]);
    setTemplates(templateRows ?? []); setRules(ruleRows ?? []); setNotifications(notificationRows ?? []); setCenter(actionCenter);
  }

  async function createTemplate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!companyId) return; setBusy(true);
    try {
      const form = event.currentTarget; const fd = new FormData(form);
      const response = await authFetch("/api/v1/notifications/templates", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ companyId, code: fd.get("code"), name: fd.get("name"), titleTemplate: fd.get("title"), bodyTemplate: fd.get("body"), deepLinkTemplate: fd.get("deepLink") }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Template oluşturulamadı.")); return; }
      form.reset(); setMessage("Bildirim template oluşturuldu."); await reload();
    } finally { setBusy(false); }
  }

  async function createRule(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!companyId) return; setBusy(true);
    try {
      const form = event.currentTarget; const fd = new FormData(form);
      const recipientKind = String(fd.get("recipientKind")); const recipientTarget = String(fd.get("recipientTarget") || "");
      const escalationKindRaw = String(fd.get("escalationKind") || ""); const escalationKind = escalationKindRaw === "NONE" ? null : escalationKindRaw;
      const escalationTarget = String(fd.get("escalationTarget") || ""); const after = Number(fd.get("escalateAfter")) || null;
      const body = { companyId, code: fd.get("code"), name: fd.get("name"), sourceModule: fd.get("sourceModule"), eventType: fd.get("eventType"), priority: fd.get("priority"), recipientKind, recipientUserId: recipientKind === "USER" ? recipientTarget : null, recipientRoleId: recipientKind === "ROLE" ? recipientTarget : null, templateId: fd.get("templateId"), escalateAfterMinutes: after, escalationRecipientKind: after ? escalationKind : null, escalationUserId: after && escalationKind === "USER" ? escalationTarget : null, escalationRoleId: after && escalationKind === "ROLE" ? escalationTarget : null };
      const response = await authFetch("/api/v1/notifications/rules", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Kural oluşturulamadı.")); return; }
      form.reset(); setMessage("Bildirim kuralı oluşturuldu."); await reload();
    } finally { setBusy(false); }
  }

  async function toggleTemplate(row: Template) {
    const response = await authFetch(`/api/v1/notifications/templates/${row.id}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: row.version, name: row.name, titleTemplate: row.titleTemplate, bodyTemplate: row.bodyTemplate, deepLinkTemplate: row.deepLinkTemplate, isActive: !row.isActive }) });
    setMessage(response?.ok ? `Template ${row.isActive ? "pasife" : "aktife"} alındı.` : await errorMessage(response, "Template güncellenemedi.")); await reload();
  }

  async function toggleRule(row: Rule) {
    const response = await authFetch(`/api/v1/notifications/rules/${row.id}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: row.version, name: row.name, sourceModule: row.sourceModule, eventType: row.eventType, priority: row.priority, recipientKind: row.recipientKind, recipientUserId: row.recipientUserId, recipientRoleId: row.recipientRoleId, templateId: row.templateId, escalateAfterMinutes: row.escalateAfterMinutes, escalationRecipientKind: row.escalationRecipientKind, escalationUserId: row.escalationUserId, escalationRoleId: row.escalationRoleId, isActive: !row.isActive }) });
    setMessage(response?.ok ? `Kural ${row.isActive ? "pasife" : "aktife"} alındı.` : await errorMessage(response, "Kural güncellenemedi.")); await reload();
  }

  async function act(row: NotificationRow, action: "seen" | "start" | "complete" | "snooze") {
    setBusy(true);
    try {
      const payload = action === "snooze" ? { version: row.version, until: new Date(Date.now() + 60 * 60 * 1000).toISOString() } : { version: row.version };
      const response = await authFetch(`/api/v1/notifications/${row.id}/${action}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) });
      setMessage(response?.ok ? `Bildirim ${action} işlemi tamamlandı.` : await errorMessage(response, "Bildirim güncellenemedi.")); await reload();
    } finally { setBusy(false); }
  }

  async function processNotifications() {
    setBusy(true);
    try {
      const response = await authFetch("/api/v1/notifications/process", { method: "POST" });
      if (!response?.ok) { setMessage(await errorMessage(response, "Bildirim işleme başarısız.")); return; }
      const result = await response.json() as RunResult; setMessage(`İşlendi: ${result.sourceEvents} event · ${result.created} yeni · ${result.duplicates} dedupe · ${result.escalated} escalation.`); await reload();
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
  const safeLink = (value: string) => value.startsWith("/") && !value.startsWith("//") ? value : "#";

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 12 · NOTIFICATION</span><h1>Notification & Action Center</h1><p>{message}</p><div className="actions action-row">{permissions.has("notification.process") ? <button className="primary-button" disabled={busy} onClick={() => void processNotifications()}>Eventleri işle</button> : null}</div></section>

    {companies.length ? <section className="panel audit-panel"><label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Tümü / scope</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label></section> : null}

    {permissions.has("notification.view") ? <>
      <section className="grid"><article className="card"><span>CRITICAL</span><h2>{center?.criticalCount ?? 0}</h2></article><article className="card"><span>PENDING</span><h2>{center?.pendingCount ?? 0}</h2></article><article className="card"><span>OVERDUE</span><h2>{center?.overdueCount ?? 0}</h2></article></section>
      <NotificationTable title="Critical" rows={center?.critical ?? []} action={act} safeLink={safeLink} busy={busy}/>
      <NotificationTable title="Overdue" rows={center?.overdue ?? []} action={act} safeLink={safeLink} busy={busy}/>
      <NotificationTable title="Pending / Action Center" rows={center?.pending ?? []} action={act} safeLink={safeLink} busy={busy}/>
      <NotificationTable title="Tüm Bildirimler" rows={notifications} action={act} safeLink={safeLink} busy={busy}/>
    </> : null}

    {permissions.has("notification.rule.manage") ? <section className="security-grid">
      <article className="panel"><h2>Template oluştur</h2><form className="inline-form" onSubmit={createTemplate}><input name="code" placeholder="Kod" required/><input name="name" placeholder="Ad" required/><input name="title" placeholder="Başlık: {{message}}" required/><input name="body" placeholder="Gövde template" required/><input name="deepLink" placeholder="/workflow?requestId={{requestId}}" required/><button className="primary-button" disabled={busy || !companyId}>Oluştur</button></form></article>
      <article className="panel"><h2>Kural oluştur</h2><form className="inline-form" onSubmit={createRule}><input name="code" placeholder="Kural kodu" required/><input name="name" placeholder="Kural adı" required/><select name="sourceModule" defaultValue="WORKFLOW"><option>WORKFLOW</option><option>ADMINISTRATION</option></select><input name="eventType" placeholder="WORKFLOW_APPROVAL_PENDING" required/><select name="priority" defaultValue="IMPORTANT"><option>INFO</option><option>NORMAL</option><option>IMPORTANT</option><option>CRITICAL</option></select><select name="recipientKind" defaultValue="CURRENT_APPROVER"><option>CURRENT_APPROVER</option><option>REQUESTER</option><option>RESPONSIBLE</option><option>USER</option><option>ROLE</option></select><select name="recipientTarget" defaultValue=""><option value="">Dinamik / hedef seçin</option>{users.filter(x => x.isActive).map(x => <option key={`u-${x.id}`} value={x.id}>USER · {x.username}</option>)}{roles.map(x => <option key={`r-${x.id}`} value={x.id}>ROLE · {x.code}</option>)}</select><select name="templateId" required defaultValue=""><option value="">Template seçin</option>{templates.filter(x => x.isActive).map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select><input name="escalateAfter" type="number" min="1" placeholder="Escalation dk (opsiyonel)"/><select name="escalationKind" defaultValue="NONE"><option>NONE</option><option>MANAGER</option><option>USER</option><option>ROLE</option></select><select name="escalationTarget" defaultValue=""><option value="">Manager / hedef seçin</option>{users.filter(x => x.isActive).map(x => <option key={`eu-${x.id}`} value={x.id}>USER · {x.username}</option>)}{roles.map(x => <option key={`er-${x.id}`} value={x.id}>ROLE · {x.code}</option>)}</select><button className="primary-button" disabled={busy || !companyId}>Kural ekle</button></form></article>
    </section> : null}

    {permissions.has("notification.rule.view") ? <>
      <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">TEMPLATES</span><h2>Bildirim Template’leri</h2></div><strong>{templates.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Kod</th><th>Ad</th><th>Başlık</th><th>Deep Link</th><th>Durum</th><th></th></tr></thead><tbody>{templates.map(x => <tr key={x.id}><td>{x.code}</td><td>{x.name}</td><td>{x.titleTemplate}</td><td>{x.deepLinkTemplate}</td><td>{x.isActive ? "ACTIVE" : "INACTIVE"}</td><td>{permissions.has("notification.rule.manage") ? <button className="secondary-button" onClick={() => void toggleTemplate(x)}>{x.isActive ? "Pasifleştir" : "Aktifleştir"}</button> : null}</td></tr>)}</tbody></table></div></section>
      <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">RULES</span><h2>Routing / Escalation Kuralları</h2></div><strong>{rules.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Kod</th><th>Kaynak Event</th><th>Priority</th><th>Recipient</th><th>Template</th><th>Escalation</th><th>Durum</th><th></th></tr></thead><tbody>{rules.map(x => <tr key={x.id}><td><strong>{x.code}</strong><small>{x.name}</small></td><td>{x.sourceModule} · {x.eventType}</td><td>{x.priority}</td><td>{x.recipientKind} · {x.recipientUsername ?? x.recipientRoleCode ?? "dynamic"}</td><td>{x.templateCode}</td><td>{x.escalateAfterMinutes ? `${x.escalateAfterMinutes} dk → ${x.escalationRecipientKind} ${x.escalationUsername ?? x.escalationRoleCode ?? ""}` : "—"}</td><td>{x.isActive ? "ACTIVE" : "INACTIVE"}</td><td>{permissions.has("notification.rule.manage") ? <button className="secondary-button" onClick={() => void toggleRule(x)}>{x.isActive ? "Pasifleştir" : "Aktifleştir"}</button> : null}</td></tr>)}</tbody></table></div></section>
    </> : null}
  </main>;
}

function NotificationTable({ title, rows, action, safeLink, busy }: { title: string; rows: NotificationRow[]; action: (row: NotificationRow, action: "seen" | "start" | "complete" | "snooze") => Promise<void>; safeLink: (value: string) => string; busy: boolean }) {
  return <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">ACTION CENTER</span><h2>{title}</h2></div><strong>{rows.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Priority</th><th>Bildirim</th><th>Kaynak</th><th>Durum</th><th>SLA / Zaman</th><th>Aksiyon</th></tr></thead><tbody>{rows.length === 0 ? <tr><td colSpan={6}>Kayıt yok.</td></tr> : rows.map(x => <tr key={x.id}><td><strong>{x.priority}</strong>{x.escalationLevel ? <small>Escalation L{x.escalationLevel}</small> : null}</td><td><strong>{x.title}</strong><small>{x.body}</small></td><td>{x.sourceModule}<small>{x.sourceEventType}</small></td><td>{x.status}</td><td>{x.dueAt ? new Date(x.dueAt).toLocaleString() : new Date(x.createdAt).toLocaleString()}</td><td><div className="actions action-row"><a className="table-button" href={safeLink(x.deepLink)}>Aç</a>{x.status === "NEW" ? <button className="secondary-button" disabled={busy} onClick={() => void action(x, "seen")}>Gördüm</button> : null}{!["COMPLETED","ESCALATED"].includes(x.status) ? <button className="secondary-button" disabled={busy} onClick={() => void action(x, "start")}>Başla</button> : null}{!["COMPLETED","ESCALATED"].includes(x.status) ? <button className="secondary-button" disabled={busy} onClick={() => void action(x, "snooze")}>1s Ertele</button> : null}{!["COMPLETED","ESCALATED"].includes(x.status) ? <button className="primary-button" disabled={busy} onClick={() => void action(x, "complete")}>Tamamla</button> : null}</div></td></tr>)}</tbody></table></div></section>;
}
