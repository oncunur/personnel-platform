"use client";

import { useEffect, useMemo, useState } from "react";

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
    <main className="shell">
      <a className="back" href="/dashboard">← Dashboard</a>
      <section className="hero compact">
        <span className="eyebrow">SPRINT 1 · SECURITY CONSOLE</span>
        <h1>Security Console</h1>
        <p>{message}</p>
        <div className="session-summary">
          <strong>{me?.username ?? "—"}</strong>
          <span>{me?.roles.map((role) => role.code).join(", ") || "Rol yok"}</span>
          <span>{me?.scopes.map((scope) => scope.scopeType).join(", ") || "Scope yok"}</span>
        </div>
        <div className="actions action-row">
          <button className="secondary-button" type="button" onClick={logoutAll}>Tüm oturumlarımı kapat</button>
        </div>
      </section>

      <section className="security-grid">
        <article className="panel">
          <div className="panel-heading"><div><span className="eyebrow dark">USERS</span><h2>Kullanıcılar</h2></div><strong>{users.length}</strong></div>
          {permissionCodes.has("system.user.view") ? (
            <div className="table-wrap">
              <table className="data-table">
                <thead><tr><th>Kullanıcı</th><th>Durum</th><th>Security v.</th><th>Son giriş</th><th></th></tr></thead>
                <tbody>{users.map((user) => (
                  <tr key={user.id}>
                    <td><strong>{user.username}</strong><small>{user.email ?? "E-posta yok"}</small></td>
                    <td><span className={`status-badge ${user.isActive ? "success" : "danger"}`}>{user.isActive ? "Aktif" : "Pasif"}</span></td>
                    <td>{user.securityVersion}</td>
                    <td>{user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleString("tr-TR") : "—"}</td>
                    <td>{permissionCodes.has("system.user.manage") && user.id !== me?.userId ? (
                      <button className="table-button" disabled={busyUserId === user.id} type="button" onClick={() => void setUserActive(user, !user.isActive)}>{user.isActive ? "Pasife al" : "Aktifleştir"}</button>
                    ) : null}</td>
                  </tr>
                ))}</tbody>
              </table>
            </div>
          ) : <p className="muted">Kullanıcı görüntüleme yetkiniz yok.</p>}
        </article>

        <article className="panel">
          <div className="panel-heading"><div><span className="eyebrow dark">RBAC</span><h2>Roller</h2></div><strong>{roles.length}</strong></div>
          {permissionCodes.has("system.role.view") ? (
            <div className="role-list">{roles.map((role) => <div className="role-row" key={role.id}><strong>{role.code}</strong><span>{role.name}</span></div>)}</div>
          ) : <p className="muted">Rol görüntüleme yetkiniz yok.</p>}
        </article>
      </section>

      <section className="panel audit-panel">
        <div className="panel-heading"><div><span className="eyebrow dark">APPEND-ONLY</span><h2>Son güvenlik olayları</h2></div><strong>{audit.length}</strong></div>
        {permissionCodes.has("audit.view") ? (
          <div className="table-wrap">
            <table className="data-table">
              <thead><tr><th>Zaman</th><th>Olay</th><th>Sonuç</th><th>Kullanıcı</th><th>IP</th><th>Hata</th></tr></thead>
              <tbody>{audit.map((item) => (
                <tr key={item.id}>
                  <td>{new Date(item.occurredAt).toLocaleString("tr-TR")}</td>
                  <td><strong>{item.eventType}</strong><small>{item.targetType && item.targetId ? `${item.targetType} · ${item.targetId}` : item.category}</small></td>
                  <td><span className={`status-badge ${item.succeeded ? "success" : "danger"}`}>{item.succeeded ? "Başarılı" : "Başarısız"}</span></td>
                  <td>{item.actorUsername ?? "—"}</td><td>{item.ipAddress ?? "—"}</td><td>{item.errorCode ?? "—"}</td>
                </tr>
              ))}</tbody>
            </table>
          </div>
        ) : <p className="muted">Audit görüntüleme yetkiniz yok.</p>}
      </section>
    </main>
  );
}
