"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { Icon, IconName } from "../components/Icon";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type RoleSummary = { id: string; code: string; name: string };
type PermissionSummary = { id: string; code: string; name: string; module: string };
type ScopeSummary = { scopeType: string; scopeId: string | null; validFrom: string; validUntil: string | null };
type MeResponse = { userId: string; username: string; email: string | null; securityVersion: number; roles: RoleSummary[]; permissions: PermissionSummary[]; scopes: ScopeSummary[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type ModuleItem = { href: string; title: string; description: string; icon: IconName; prefixes: string[] };

const modules: ModuleItem[] = [
  { href: "/personnel", title: "Personel Yönetimi", description: "Personel kartları, organizasyon bilgileri ve Personel 360.", icon: "people", prefixes: ["personnel."] },
  { href: "/attendance", title: "Puantaj ve Vardiya", description: "Takvim, vardiya, günlük puantaj ve fazla mesai süreçleri.", icon: "calendar", prefixes: ["attendance."] },
  { href: "/payroll", title: "Bordro ve Ücret", description: "Ücret geçmişi, dönem hesaplama ve bordro onay akışı.", icon: "wallet", prefixes: ["payroll."] },
  { href: "/leave", title: "İzin Yönetimi", description: "İzin talepleri, bakiyeler ve yönetici onayları.", icon: "calendar", prefixes: ["leave."] },
  { href: "/camp", title: "Kamp ve Konaklama", description: "Kamp, oda, yatak ve konaklama maliyetleri.", icon: "building", prefixes: ["camp."] },
  { href: "/assets", title: "Zimmet ve Stok", description: "Demirbaş, zimmet ve stok hareketlerinin yönetimi.", icon: "box", prefixes: ["administration.asset.", "administration.stock."] },
  { href: "/workflow", title: "Talep ve Onaylar", description: "Kurumsal talepler, onay adımları ve SLA takibi.", icon: "workflow", prefixes: ["workflow."] },
  { href: "/reports", title: "Raporlama ve Maliyet", description: "Yönetim göstergeleri, maliyet defteri ve dışa aktarımlar.", icon: "chart", prefixes: ["reporting.", "finance.cost."] },
  { href: "/organization", title: "Organizasyon", description: "Şirket, şube, departman, pozisyon ve proje yapısı.", icon: "building", prefixes: ["organization."] },
  { href: "/security", title: "Sistem Yönetimi", description: "Kullanıcı, rol, yetki, kapsam ve denetim kayıtları.", icon: "settings", prefixes: ["system.", "audit."] },
];

export default function DashboardPage() {
  const [me, setMe] = useState<MeResponse | null>(null);
  const [message, setMessage] = useState("Oturum ve yetkiler doğrulanıyor…");
  useEffect(() => { void loadSession(); }, []);

  async function loadSession() {
    let accessToken = sessionStorage.getItem("pp_access_token") ?? await refreshAccessToken();
    if (!accessToken) { window.location.replace("/login"); return; }
    let response = await fetchMe(accessToken);
    if (response.status === 401) {
      accessToken = await refreshAccessToken();
      if (!accessToken) { window.location.replace("/login"); return; }
      response = await fetchMe(accessToken);
    }
    if (!response.ok) { setMessage("Oturum bilgileri şu anda alınamadı."); return; }
    setMe(await response.json() as MeResponse);
    setMessage("Oturumunuz güvenli ve kullanıma hazır.");
  }
  async function refreshAccessToken(): Promise<string | null> {
    try {
      const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" });
      if (!response.ok) { clearLocalSession(); return null; }
      const body = await response.json() as AuthResponse;
      sessionStorage.setItem("pp_access_token", body.accessToken);
      sessionStorage.setItem("pp_access_token_expires_at", body.accessTokenExpiresAt);
      return body.accessToken;
    } catch { return null; }
  }
  function fetchMe(accessToken: string) { return fetch(`${apiBase}/api/v1/auth/me`, { headers: { Authorization: `Bearer ${accessToken}` }, credentials: "include" }); }
  function clearLocalSession() { sessionStorage.removeItem("pp_access_token"); sessionStorage.removeItem("pp_access_token_expires_at"); }

  const permissionCodes = useMemo(() => me?.permissions.map((item) => item.code) ?? [], [me]);
  const visibleModules = modules.filter((item) => item.prefixes.some((prefix) => permissionCodes.some((permission) => permission.startsWith(prefix))));
  const uniqueScopeTypes = [...new Set(me?.scopes.map((scope) => scope.scopeType) ?? [])];

  return <main className="page-shell">
    <section className="overview-banner">
      <div className="overview-content">
        <span className="overview-kicker"><span className="status-dot"/> Oturum aktif</span>
        <h1>{me ? `Hoş geldiniz, ${me.username}` : "Personel ve İdari İşler Platformu"}</h1>
        <p>Günlük insan kaynakları ve idari operasyonlarınıza tek, sade ve güvenli çalışma alanından ulaşın.</p>
      </div>
      <div className="overview-profile">
        <span>Oturum özeti</span>
        <strong>{me?.email ?? "Kullanıcı bilgileri yükleniyor"}</strong>
        <small>{message}</small>
        <div className="role-chips">{me?.roles.slice(0, 4).map((role) => <span className="role-chip" key={role.id}>{role.name}</span>)}</div>
      </div>
    </section>

    <section className="stat-grid" aria-label="Oturum göstergeleri">
      <article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{me?.roles.length ?? "—"}</strong><span>Aktif rol</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="settings"/></span><span className="stat-copy"><strong>{me?.permissions.length ?? "—"}</strong><span>Tanımlı yetki</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="building"/></span><span className="stat-copy"><strong>{me?.scopes.length ?? "—"}</strong><span>Erişim kapsamı</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{visibleModules.length || "—"}</strong><span>Kullanılabilir modül</span></span></article>
    </section>

    <section className="content-grid">
      <div className="panel">
        <div className="panel-heading"><div><span className="page-eyebrow">Hızlı erişim</span><h2>Sık kullanılan çalışma alanları</h2><p>Yetkilerinize göre kullanabileceğiniz temel modüller.</p></div><strong>{visibleModules.length}</strong></div>
        <div className="module-grid">
          {visibleModules.slice(0, 8).map((item) => <Link className="module-card" href={item.href} key={item.href}>
            <span className="module-icon"><Icon name={item.icon}/></span>
            <span className="module-card-copy"><strong>{item.title}</strong><span>{item.description}</span><small>Modülü aç <Icon name="arrow" size={14}/></small></span>
          </Link>)}
          {me && visibleModules.length === 0 ? <p className="muted">Hesabınız için erişilebilir bir operasyon modülü bulunmuyor.</p> : null}
        </div>
      </div>
      <aside className="panel">
        <div className="panel-heading"><div><span className="page-eyebrow">Erişim özeti</span><h2>Rol ve kapsamlar</h2><p>Bu oturumda etkin olan erişim çerçeveniz.</p></div></div>
        <div className="stack">
          <div><span className="page-eyebrow">Roller</span><div className="role-chips">{me?.roles.map((role) => <span className="role-chip" key={role.id}>{role.code}</span>) ?? <span className="muted">Yükleniyor…</span>}</div></div>
          <div><span className="page-eyebrow">Kapsam türleri</span><div className="role-chips">{uniqueScopeTypes.map((scope) => <span className="role-chip" key={scope}>{scope}</span>)}{me && uniqueScopeTypes.length === 0 ? <span className="muted">Kapsam bulunmuyor</span> : null}</div></div>
          <div className="notice"><span className="status-dot"/><span>{message}</span></div>
        </div>
      </aside>
    </section>
  </main>;
}
