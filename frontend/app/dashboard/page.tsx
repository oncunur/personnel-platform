"use client";

import { useEffect, useState } from "react";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type RoleSummary = { id: string; code: string; name: string };
type PermissionSummary = { id: string; code: string; name: string; module: string };
type ScopeSummary = { scopeType: string; scopeId: string | null; validFrom: string; validUntil: string | null };
type MeResponse = { userId: string; username: string; email: string | null; securityVersion: number; roles: RoleSummary[]; permissions: PermissionSummary[]; scopes: ScopeSummary[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };

export default function DashboardPage() {
  const [me, setMe] = useState<MeResponse | null>(null);
  const [message, setMessage] = useState("Oturum doğrulanıyor…");
  useEffect(() => { void loadSession(); }, []);

  async function loadSession() {
    let accessToken = sessionStorage.getItem("pp_access_token") ?? await refreshAccessToken();
    if (!accessToken) { window.location.replace("/login"); return; }
    let response = await fetchMe(accessToken);
    if (response.status === 401) { accessToken = await refreshAccessToken(); if (!accessToken) { window.location.replace("/login"); return; } response = await fetchMe(accessToken); }
    if (!response.ok) { setMessage("Oturum bilgisi alınamadı."); return; }
    setMe(await response.json() as MeResponse); setMessage("Oturum aktif.");
  }
  async function refreshAccessToken(): Promise<string | null> { try { const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!response.ok) { clearLocalSession(); return null; } const body = await response.json() as AuthResponse; sessionStorage.setItem("pp_access_token", body.accessToken); sessionStorage.setItem("pp_access_token_expires_at", body.accessTokenExpiresAt); return body.accessToken; } catch { return null; } }
  function fetchMe(accessToken: string) { return fetch(`${apiBase}/api/v1/auth/me`, { headers: { Authorization: `Bearer ${accessToken}` }, credentials: "include" }); }
  async function logout() { try { await fetch(`${apiBase}/api/v1/auth/logout`, { method: "POST", credentials: "include" }); } finally { clearLocalSession(); window.location.replace("/login"); } }
  function clearLocalSession() { sessionStorage.removeItem("pp_access_token"); sessionStorage.removeItem("pp_access_token_expires_at"); }

  const canOpenSecurity = me?.permissions.some(x => ["system.user.view", "system.role.view", "audit.view"].includes(x.code)) ?? false;
  const canOpenOrganization = me?.permissions.some(x => x.code.startsWith("organization.")) ?? false;
  const canOpenPersonnel = me?.permissions.some(x => x.code === "personnel.view") ?? false;

  return <main className="shell">
    <section className="hero compact"><span className="eyebrow">SPRINT 2 · PLATFORM</span><h1>Platform Dashboard</h1><p>{message}</p>{me ? <div className="session-summary"><strong>{me.username}</strong><span>{me.email ?? "E-posta tanımlı değil"}</span><span>Security version: {me.securityVersion}</span><span>Roller: {me.roles.map(x => x.code).join(", ") || "—"}</span><span>Scope: {me.scopes.map(x => x.scopeType).join(", ") || "—"}</span></div> : null}<div className="actions action-row">{canOpenPersonnel ? <a className="primary" href="/personnel">Personel</a> : null}{canOpenOrganization ? <a className="primary" href="/organization">Organizasyon</a> : null}{canOpenSecurity ? <a className="primary" href="/security">Security Console</a> : null}<button className="secondary-button" type="button" onClick={logout}>Çıkış yap</button></div></section>
    <section className="grid" aria-label="Platform durum kartları"><article className="card"><span>Aktif</span><h2>Identity / Session</h2></article><article className="card"><span>Aktif</span><h2>Role / Permission / Scope</h2></article><article className="card"><span>Aktif</span><h2>Organization Core</h2></article><article className="card"><span>Geliştiriliyor</span><h2>Personnel Core / Personel 360</h2></article><article className="card"><span>Sıradaki</span><h2>Özlük & Belgeler</h2></article></section>
  </main>;
}
