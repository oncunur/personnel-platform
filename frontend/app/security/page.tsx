"use client";

import { useEffect, useMemo, useState } from "react";
import { Icon } from "../components/Icon";
import { PageHeader } from "../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type RoleSummary = { id: string; code: string; name: string };
type PermissionSummary = { id: string; code: string; name: string; module: string };
type ScopeSummary = { scopeType: string; scopeId: string | null; validFrom: string; validUntil: string | null };
type MeResponse = {
  userId: string;
  username: string;
  email: string | null;
  securityVersion: number;
  roles: RoleSummary[];
  permissions: PermissionSummary[];
  scopes: ScopeSummary[];
};
type SecurityUser = {
  id: string;
  username: string;
  email: string | null;
  isActive: boolean;
  lastLoginAt: string | null;
  securityVersion: number;
};
type AuditLog = {
  id: string;
  category: string;
  eventType: string;
  succeeded: boolean;
  severity: string;
  occurredAt: string;
  actorUsername: string | null;
  ipAddress: string | null;
  targetType: string | null;
  targetId: string | null;
  errorCode: string | null;
};
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };

export default function SecurityPage() {
  const [me, setMe] = useState<MeResponse | null>(null);
  const [users, setUsers] = useState<SecurityUser[]>([]);
  const [roles, setRoles] = useState<RoleSummary[]>([]);
  const [audit, setAudit] = useState<AuditLog[]>([]);
  const [message, setMessage] = useState("Güvenlik verileri yükleniyor…");
  const [busyUserId, setBusyUserId] = useState<string | null>(null);

  const permissionCodes = useMemo(
    () => new Set(me?.permissions.map((permission) => permission.code) ?? []),
    [me],
  );
  const activeUsers = useMemo(() => users.filter(user => user.isActive), [users]);
  const failedEvents = useMemo(() => audit.filter(item => !item.succeeded), [audit]);
  const criticalEvents = useMemo(() => audit.filter(item => ["HIGH", "CRITICAL"].includes(item.severity)), [audit]);

  useEffect(() => {
    void initialize();
  }, []);

  async function initialize() {
    const current = await authorizedJson<MeResponse>("/api/v1/auth/me");
    if (!current) {
      window.location.replace("/login");
      return;
    }

    setMe(current);
    const codes = new Set(current.permissions.map((permission) => permission.code));

    const [userRows, roleRows, auditRows] = await Promise.all([
      codes.has("system.user.view") ? authorizedJson<SecurityUser[]>("/api/v1/security/users") : Promise.resolve([]),
      codes.has("system.role.view") ? authorizedJson<RoleSummary[]>("/api/v1/security/roles") : Promise.resolve([]),
      codes.has("audit.view") ? authorizedJson<AuditLog[]>("/api/v1/security/audit?take=50") : Promise.resolve([]),
    ]);

    setUsers(userRows ?? []);
    setRoles(roleRows ?? []);
    setAudit(auditRows ?? []);
    setMessage("Güvenlik görünümü güncel.");
  }

  async function setUserActive(user: SecurityUser, active: boolean) {
    if (!permissionCodes.has("system.user.manage")) return;
    setBusyUserId(user.id);
    try {
      const path = `/api/v1/security/users/${user.id}/${active ? "activate" : "deactivate"}`;
      const updated = await authorizedJson<SecurityUser>(path, { method: "POST" });
      if (updated) {
        setUsers((current) => current.map((item) => (item.id === updated.id ? updated : item)));
      }
    } finally {
      setBusyUserId(null);
    }
  }

  async function logoutAll() {
    const response = await authorizedFetch("/api/v1/auth/logout-all", { method: "POST" });
    if (response?.ok) {
      clearLocalSession();
      window.location.replace("/login");
    }
  }

  async function authorizedJson<T>(path: string, init?: RequestInit): Promise<T | null> {
    const response = await authorizedFetch(path, init);
    if (!response) return null;
    if (response.status === 401) {
      clearLocalSession();
      return null;
    }
    if (!response.ok) {
      setMessage(`İstek tamamlanamadı (${response.status}).`);
      return null;
    }
    return (await response.json()) as T;
  }

  async function authorizedFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let accessToken = sessionStorage.getItem("pp_access_token");
    if (!accessToken) accessToken = await refreshAccessToken();
    if (!accessToken) return null;

    let response = await fetch(`${apiBase}${path}`, {
      ...init,
      headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${accessToken}` },
      credentials: "include",
    });

    if (response.status !== 401) return response;

    accessToken = await refreshAccessToken();
    if (!accessToken) return response;

    response = await fetch(`${apiBase}${path}`, {
      ...init,
      headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${accessToken}` },
      credentials: "include",
    });
    return response;
  }

  async function refreshAccessToken(): Promise<string | null> {
    try {
      const response = await fetch(`${apiBase}/api/v1/auth/refresh`, {
        method: "POST",
        credentials: "include",
      });
      if (!response.ok) {
        clearLocalSession();
        return null;
      }
      const body = (await response.json()) as AuthResponse;
      sessionStorage.setItem("pp_access_token", body.accessToken);
      sessionStorage.setItem("pp_access_token_expires_at", body.accessTokenExpiresAt);
      return body.accessToken;
    } catch {
      return null;
    }
  }

  function clearLocalSession() {
    sessionStorage.removeItem("pp_access_token");
    sessionStorage.removeItem("pp_access_token_expires_at");
  }

  return (
    <main className="page-shell">
      <PageHeader eyebrow="Güvenlik ve erişim" title="Güvenlik konsolu" description="Kullanıcı durumlarını, rol tanımlarını, oturum kapsamını ve güvenlik olaylarını izleyin." status={message} actions={<button className="secondary-button button-danger" type="button" onClick={logoutAll}>Tüm oturumlarımı kapat</button>}/>

      <section className="stat-grid" aria-label="Güvenlik özeti">
        <article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{activeUsers.length}</strong><span>Aktif kullanıcı</span></span></article>
        <article className="stat-card"><span className="stat-icon"><Icon name="settings"/></span><span className="stat-copy"><strong>{roles.length}</strong><span>Tanımlı rol</span></span></article>
        <article className="stat-card"><span className="stat-icon"><Icon name="bell"/></span><span className="stat-copy"><strong>{failedEvents.length}</strong><span>Başarısız güvenlik olayı</span></span></article>
        <article className="stat-card"><span className="stat-icon"><Icon name="chart"/></span><span className="stat-copy"><strong>{criticalEvents.length}</strong><span>Yüksek öncelikli olay</span></span></article>
      </section>

      <section className="panel session-panel"><div className="workspace-copy"><span className="eyebrow dark">Mevcut oturum</span><h2>{me?.username ?? "Kullanıcı yükleniyor"}</h2><p>{me?.email ?? "E-posta bilgisi yok"} · Güvenlik sürümü {me?.securityVersion ?? "—"}</p></div><div><span className="session-label">Roller</span><div className="role-chips">{me?.roles.length ? me.roles.map(role => <span className="role-chip" key={role.id}>{role.name}</span>) : <span className="role-chip">Rol yok</span>}</div></div><div><span className="session-label">Erişim kapsamı</span><div className="role-chips">{me?.scopes.length ? me.scopes.map((scope,index) => <span className="role-chip" key={`${scope.scopeType}-${scope.scopeId}-${index}`}>{scope.scopeType}{scope.scopeId ? ` · ${scope.scopeId.slice(0,8)}…` : " · Tümü"}</span>) : <span className="role-chip">Kapsam yok</span>}</div></div></section>

      <div className="content-stack">
        <section className="security-grid">
          <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Kullanıcı erişimi</span><h2>Kullanıcılar</h2><p>Hesap durumunu ve son giriş bilgisini yönetin.</p></div><strong>{users.length}</strong></div>{permissionCodes.has("system.user.view") ? <div className="table-wrap"><table className="data-table"><thead><tr><th>Kullanıcı</th><th>Durum</th><th>Güvenlik sürümü</th><th>Son giriş</th><th>İşlem</th></tr></thead><tbody>{users.length?users.map((user) => <tr key={user.id}><td><strong>{user.username}</strong><small>{user.email ?? "E-posta yok"}</small></td><td><span className={`status-badge ${user.isActive ? "success" : "danger"}`}>{user.isActive ? "Aktif" : "Pasif"}</span></td><td>{user.securityVersion}</td><td>{user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString("tr-TR") : "—"}</td><td>{permissionCodes.has("system.user.manage") && user.id !== me?.userId ? <button className={`table-button ${user.isActive?"button-danger":"button-success"}`} disabled={busyUserId === user.id} type="button" onClick={() => void setUserActive(user, !user.isActive)}>{user.isActive ? "Pasife al" : "Aktifleştir"}</button> : user.id===me?.userId?"Mevcut kullanıcı":"—"}</td></tr>):<tr><td className="empty-row" colSpan={5}>Kullanıcı kaydı yok.</td></tr>}</tbody></table></div> : <p className="notice">Kullanıcı listesini görüntüleme yetkiniz yok.</p>}</article>

          <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Rol tabanlı erişim</span><h2>Roller</h2><p>Sistemde kullanılabilen yetki grupları.</p></div><strong>{roles.length}</strong></div>{permissionCodes.has("system.role.view") ? <div className="role-list">{roles.length?roles.map((role) => <div className="role-row" key={role.id}><strong>{role.name}</strong><span>{role.code}</span></div>):<p className="muted">Rol tanımı yok.</p>}</div> : <p className="notice">Rol listesini görüntüleme yetkiniz yok.</p>}</article>
        </section>

        <section className={`panel attention-panel ${failedEvents.length?"danger":"success"}`}><div className="panel-heading"><div><span className="eyebrow dark">Güvenlik olayları</span><h2>Son denetim kayıtları</h2><p>{failedEvents.length?`${failedEvents.length} başarısız olay inceleme gerektiriyor.`:"Listelenen olaylarda başarısız işlem yok."}</p></div><strong>{audit.length}</strong></div>{permissionCodes.has("audit.view") ? <div className="table-wrap"><table className="data-table"><thead><tr><th>Zaman</th><th>Olay</th><th>Önem</th><th>Sonuç</th><th>Kullanıcı</th><th>IP adresi</th><th>Hata</th></tr></thead><tbody>{audit.length?audit.map((item) => <tr key={item.id}><td>{new Date(item.occurredAt).toLocaleString("tr-TR")}</td><td><strong>{item.eventType}</strong><small>{item.targetType && item.targetId ? `${item.targetType} · ${item.targetId}` : item.category}</small></td><td><span className={`status-badge ${["HIGH","CRITICAL"].includes(item.severity)?"danger":item.severity==="MEDIUM"?"warning":""}`}>{item.severity==="CRITICAL"?"Kritik":item.severity==="HIGH"?"Yüksek":item.severity==="MEDIUM"?"Orta":"Bilgi"}</span></td><td><span className={`status-badge ${item.succeeded ? "success" : "danger"}`}>{item.succeeded ? "Başarılı" : "Başarısız"}</span></td><td>{item.actorUsername ?? "—"}</td><td>{item.ipAddress ?? "—"}</td><td>{item.errorCode ?? "—"}</td></tr>):<tr><td className="empty-row" colSpan={7}>Güvenlik olayı yok.</td></tr>}</tbody></table></div> : <p className="notice">Güvenlik olaylarını görüntüleme yetkiniz yok.</p>}</section>
      </div>
    </main>
  );
}
