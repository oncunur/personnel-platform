"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { Icon } from "../components/Icon";
import { OperationDisclosure } from "../components/OperationDisclosure";
import { PageHeader } from "../components/PageHeader";

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

const priorityLabels: Record<string, { label: string; tone: string }> = {
  INFO: { label: "Bilgi", tone: "" },
  NORMAL: { label: "Normal", tone: "" },
  IMPORTANT: { label: "Önemli", tone: "warning" },
  CRITICAL: { label: "Kritik", tone: "danger" },
};

const notificationStatuses: Record<string, { label: string; tone: string }> = {
  NEW: { label: "Yeni", tone: "warning" },
  SEEN: { label: "Görüldü", tone: "" },
  IN_PROGRESS: { label: "İşlemde", tone: "warning" },
  SNOOZED: { label: "Ertelendi", tone: "" },
  COMPLETED: { label: "Tamamlandı", tone: "success" },
  ESCALATED: { label: "Üst seviyeye aktarıldı", tone: "danger" },
};

const recipientLabels: Record<string, string> = {
  CURRENT_APPROVER: "Mevcut onaylayan",
  REQUESTER: "Talep sahibi",
  RESPONSIBLE: "Sorumlu kişi",
  USER: "Belirli kullanıcı",
  ROLE: "Belirli rol",
  MANAGER: "Yönetici",
};

function priorityOf(value: string) {
  return priorityLabels[value] ?? { label: value, tone: "" };
}

function notificationStatusOf(value: string) {
  return notificationStatuses[value] ?? { label: value, tone: "" };
}

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
  const [message, setMessage] = useState("Bildirim ve eylem merkezi yükleniyor…");
  const [busy, setBusy] = useState(false);
  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const activeUsers = useMemo(() => users.filter(x => x.isActive), [users]);
  const attentionRows = useMemo(() => {
    const seen = new Set<string>();
    return [...(center?.critical ?? []), ...(center?.overdue ?? [])].filter(row => {
      if (seen.has(row.id)) return false;
      seen.add(row.id);
      return true;
    });
  }, [center]);

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
    setMessage("Bildirim ve eylem merkezi güncel.");
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
      if (!response?.ok) { setMessage(await errorMessage(response, "Bildirim şablonu oluşturulamadı.")); return; }
      form.reset(); setMessage("Bildirim şablonu oluşturuldu."); await reload();
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
    setMessage(response?.ok ? `Bildirim şablonu ${row.isActive ? "pasife" : "aktife"} alındı.` : await errorMessage(response, "Bildirim şablonu güncellenemedi.")); await reload();
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
      const actionLabel = { seen: "görüldü", start: "üzerime alındı", complete: "tamamlandı", snooze: "bir saat ertelendi" }[action];
      setMessage(response?.ok ? `Bildirim ${actionLabel}.` : await errorMessage(response, "Bildirim güncellenemedi.")); await reload();
    } finally { setBusy(false); }
  }

  async function processNotifications() {
    setBusy(true);
    try {
      const response = await authFetch("/api/v1/notifications/process", { method: "POST" });
      if (!response?.ok) { setMessage(await errorMessage(response, "Bildirim işleme başarısız.")); return; }
      const result = await response.json() as RunResult; setMessage(`${result.sourceEvents} kaynak olayı işlendi; ${result.created} yeni bildirim oluşturuldu, ${result.duplicates} tekrar atlandı ve ${result.escalated} kayıt üst seviyeye aktarıldı.`); await reload();
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

  return <main className="page-shell">
    <PageHeader eyebrow="İş ve uyarı merkezi" title="Bildirimler" description="Kritik işleri önceliklendirin, bekleyen eylemleri sonuçlandırın ve bildirim kurallarını yönetin." status={message} actions={permissions.has("notification.process") ? <button className="primary-button" disabled={busy} onClick={() => void processNotifications()}><Icon name="workflow" size={17}/>Yeni olayları işle</button> : null}/>

    {permissions.has("notification.view") ? <section className="stat-grid" aria-label="Bildirim özeti">
      <article className="stat-card"><span className="stat-icon"><Icon name="bell"/></span><span className="stat-copy"><strong>{center?.criticalCount ?? 0}</strong><span>Kritik bildirim</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{center?.pendingCount ?? 0}</strong><span>Eylem bekleyen</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="calendar"/></span><span className="stat-copy"><strong>{center?.overdueCount ?? 0}</strong><span>Süresi geçen</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="chart"/></span><span className="stat-copy"><strong>{notifications.length}</strong><span>Toplam bildirim</span></span></article>
    </section> : null}

    {companies.length ? <section className="panel workspace-panel"><div className="workspace-copy"><span className="eyebrow dark">Çalışma alanı</span><h2>Bildirim kapsamı</h2><p>Görüntülenecek şirketi ve kurallarını seçin.</p></div><label className="field-label workspace-select">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Yetki kapsamımdaki tüm şirketler</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label></section> : null}

    <div className="content-stack">
      {permissions.has("notification.view") ? <>
        {attentionRows.length ? <NotificationTable title="Öncelikli inceleme" description="Kritik veya süresi geçmiş bildirimler önce gösterilir." rows={attentionRows} action={act} safeLink={safeLink} busy={busy} tone="danger"/> : <section className="panel attention-panel success"><div className="panel-heading"><div><span className="eyebrow dark">Öncelikli inceleme</span><h2>Acil müdahale gereken bildirim yok</h2><p>Kritik ve süresi geçmiş işler temiz görünüyor.</p></div><span className="status-badge success">Güncel</span></div></section>}
        <NotificationTable title="Eylem bekleyenler" description="Üzerinizde aksiyon bekleyen açık bildirimler." rows={center?.pending ?? []} action={act} safeLink={safeLink} busy={busy}/>
        <NotificationTable title="Bildirim geçmişi" description="Seçili kapsamda oluşturulan tüm bildirimler." rows={notifications} action={act} safeLink={safeLink} busy={busy}/>
      </> : null}

      {permissions.has("notification.rule.manage") ? <section className="organization-grid">
        <article className="panel"><OperationDisclosure title="Yeni bildirim şablonu oluştur" description="Başlık, açıklama ve yönlendirme bağlantısını tanımlayın."><form className="stack" onSubmit={createTemplate}><label className="field-label">Şablon kodu<input name="code" required/></label><label className="field-label">Şablon adı<input name="name" required/></label><label className="field-label">Bildirim başlığı<input name="title" required/></label><label className="field-label">Bildirim metni<textarea name="body" required/></label><label className="field-label">Yönlendirme yolu<input name="deepLink" required/><small>Örnek: /workflow?requestId=&#123;&#123;requestId&#125;&#125;</small></label><button className="primary-button" disabled={busy || !companyId}><Icon name="plus" size={17}/>Şablon oluştur</button></form></OperationDisclosure></article>
        <article className="panel"><OperationDisclosure title="Yeni bildirim kuralı oluştur" description="Hangi olayın kime, hangi öncelikle iletileceğini belirleyin."><form className="stack" onSubmit={createRule}><div className="inline-form"><label className="field-label">Kural kodu<input name="code" required/></label><label className="field-label">Kural adı<input name="name" required/></label><label className="field-label">Kaynak modül<select name="sourceModule" defaultValue="WORKFLOW"><option value="WORKFLOW">İş akışları</option><option value="ADMINISTRATION">İdari işler</option></select></label><label className="field-label">Olay türü<input name="eventType" required/></label><label className="field-label">Öncelik<select name="priority" defaultValue="IMPORTANT"><option value="INFO">Bilgi</option><option value="NORMAL">Normal</option><option value="IMPORTANT">Önemli</option><option value="CRITICAL">Kritik</option></select></label><label className="field-label">Alıcı türü<select name="recipientKind" defaultValue="CURRENT_APPROVER"><option value="CURRENT_APPROVER">Mevcut onaylayan</option><option value="REQUESTER">Talep sahibi</option><option value="RESPONSIBLE">Sorumlu kişi</option><option value="USER">Belirli kullanıcı</option><option value="ROLE">Belirli rol</option></select></label><label className="field-label">Alıcı hedefi<select name="recipientTarget" defaultValue=""><option value="">Dinamik alıcı / hedef seçin</option>{activeUsers.map(x => <option key={`u-${x.id}`} value={x.id}>Kullanıcı · {x.username}</option>)}{roles.map(x => <option key={`r-${x.id}`} value={x.id}>Rol · {x.code}</option>)}</select></label><label className="field-label">Bildirim şablonu<select name="templateId" required defaultValue=""><option value="">Şablon seçin</option>{templates.filter(x => x.isActive).map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Üst seviyeye aktarma süresi (dk)<input name="escalateAfter" type="number" min="1"/></label><label className="field-label">Üst seviye alıcı türü<select name="escalationKind" defaultValue="NONE"><option value="NONE">Aktarma yok</option><option value="MANAGER">Yönetici</option><option value="USER">Belirli kullanıcı</option><option value="ROLE">Belirli rol</option></select></label><label className="field-label">Üst seviye hedefi<select name="escalationTarget" defaultValue=""><option value="">Yönetici / hedef seçin</option>{activeUsers.map(x => <option key={`eu-${x.id}`} value={x.id}>Kullanıcı · {x.username}</option>)}{roles.map(x => <option key={`er-${x.id}`} value={x.id}>Rol · {x.code}</option>)}</select></label></div><button className="primary-button" disabled={busy || !companyId}><Icon name="plus" size={17}/>Kural ekle</button></form></OperationDisclosure></article>
      </section> : null}

      {permissions.has("notification.rule.view") ? <>
        <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Şablonlar</span><h2>Bildirim içerikleri</h2><p>Kullanılabilir başlık, metin ve yönlendirme şablonları.</p></div><strong>{templates.length}</strong></div><div className="table-wrap responsive-table-wrap" role="region" aria-label="Bildirim şablonları" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Kod / Ad</th><th>Başlık</th><th>Yönlendirme</th><th>Durum</th><th>İşlem</th></tr></thead><tbody>{templates.length === 0 ? <tr><td className="empty-row" colSpan={5}>Henüz bildirim şablonu tanımlanmadı.</td></tr> : templates.map(x => <tr key={x.id}><td data-label="Kod / Ad"><strong>{x.name}</strong><small>{x.code}</small></td><td data-label="Başlık">{x.titleTemplate}</td><td data-label="Yönlendirme"><code>{x.deepLinkTemplate}</code></td><td data-label="Durum"><span className={`status-badge ${x.isActive ? "success" : ""}`}>{x.isActive ? "Aktif" : "Pasif"}</span></td><td data-label="İşlem">{permissions.has("notification.rule.manage") ? <button className="secondary-button" onClick={() => void toggleTemplate(x)}>{x.isActive ? "Pasife al" : "Aktife al"}</button> : "—"}</td></tr>)}</tbody></table></div></section>
        <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Kurallar</span><h2>Yönlendirme ve üst seviye kuralları</h2><p>Kaynak olayların alıcı ve gecikme davranışlarını yönetin.</p></div><strong>{rules.length}</strong></div><div className="table-wrap responsive-table-wrap" role="region" aria-label="Bildirim yönlendirme kuralları" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Kural</th><th>Kaynak olay</th><th>Öncelik</th><th>Alıcı</th><th>Şablon</th><th>Üst seviyeye aktarma</th><th>Durum</th><th>İşlem</th></tr></thead><tbody>{rules.length === 0 ? <tr><td className="empty-row" colSpan={8}>Henüz bildirim kuralı tanımlanmadı.</td></tr> : rules.map(x => { const priority = priorityOf(x.priority); return <tr key={x.id}><td data-label="Kural"><strong>{x.name}</strong><small>{x.code}</small></td><td data-label="Kaynak olay">{x.sourceModule}<small>{x.eventType}</small></td><td data-label="Öncelik"><span className={`status-badge ${priority.tone}`}>{priority.label}</span></td><td data-label="Alıcı">{recipientLabels[x.recipientKind] ?? x.recipientKind}<small>{x.recipientUsername ?? x.recipientRoleCode ?? "Dinamik"}</small></td><td data-label="Şablon">{x.templateCode}</td><td data-label="Üst seviyeye aktarma">{x.escalateAfterMinutes ? `${x.escalateAfterMinutes} dk sonra ${recipientLabels[x.escalationRecipientKind ?? ""] ?? x.escalationRecipientKind ?? ""} ${x.escalationUsername ?? x.escalationRoleCode ?? ""}` : "Yok"}</td><td data-label="Durum"><span className={`status-badge ${x.isActive ? "success" : ""}`}>{x.isActive ? "Aktif" : "Pasif"}</span></td><td data-label="İşlem">{permissions.has("notification.rule.manage") ? <button className="secondary-button" onClick={() => void toggleRule(x)}>{x.isActive ? "Pasife al" : "Aktife al"}</button> : "—"}</td></tr>; })}</tbody></table></div></section>
      </> : null}
    </div>
  </main>;
}

function NotificationTable({ title, description, rows, action, safeLink, busy, tone = "" }: { title: string; description: string; rows: NotificationRow[]; action: (row: NotificationRow, action: "seen" | "start" | "complete" | "snooze") => Promise<void>; safeLink: (value: string) => string; busy: boolean; tone?: string }) {
  return <section className={`panel attention-panel ${tone}`}><div className="panel-heading"><div><span className="eyebrow dark">Eylem merkezi</span><h2>{title}</h2><p>{description}</p></div><strong>{rows.length}</strong></div><div className="table-wrap responsive-table-wrap" role="region" aria-label={title} tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Öncelik</th><th>Bildirim</th><th>Kaynak</th><th>Durum</th><th>Son tarih / Oluşturma</th><th>İşlemler</th></tr></thead><tbody>{rows.length === 0 ? <tr><td className="empty-row" colSpan={6}>Bu bölümde işlem bekleyen bildirim yok.</td></tr> : rows.map(x => { const priority = priorityOf(x.priority); const status = notificationStatusOf(x.status); const isOpen = !["COMPLETED", "ESCALATED"].includes(x.status); return <tr key={x.id}><td data-label="Öncelik"><span className={`status-badge ${priority.tone}`}>{priority.label}</span>{x.escalationLevel ? <small>Aktarma seviyesi {x.escalationLevel}</small> : null}</td><td data-label="Bildirim"><strong>{x.title}</strong><small>{x.body}</small></td><td data-label="Kaynak">{x.sourceModule}<small>{x.sourceEventType}</small></td><td data-label="Durum"><span className={`status-badge ${status.tone}`}>{status.label}</span>{x.snoozedUntil ? <small>{new Date(x.snoozedUntil).toLocaleString("tr-TR")} tarihine ertelendi</small> : null}</td><td data-label="Son tarih / Oluşturma">{new Date(x.dueAt ?? x.createdAt).toLocaleString("tr-TR")}</td><td data-label="İşlemler"><div className="action-row"><a className="table-button" href={safeLink(x.deepLink)}>Kaydı aç</a>{x.status === "NEW" ? <button className="secondary-button" disabled={busy} onClick={() => void action(x, "seen")}>Gördüm</button> : null}{isOpen ? <button className="secondary-button" disabled={busy} onClick={() => void action(x, "start")}>Üzerime al</button> : null}{isOpen ? <button className="secondary-button" disabled={busy} onClick={() => void action(x, "snooze")}>1 saat ertele</button> : null}{isOpen ? <button className="primary-button" disabled={busy} onClick={() => void action(x, "complete")}>Tamamla</button> : null}</div></td></tr>; })}</tbody></table></div></section>;
}
