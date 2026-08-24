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
const scopeLabels: Record<string, string> = {
  GLOBAL: "Tüm platform",
  COMPANY: "Şirket",
  BRANCH: "Şube",
  DEPARTMENT: "Departman",
  PROJECT: "Proje",
  SELF: "Kendi kayıtları",
};

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
  const roleSummary = me ? (me.roles.slice(0, 2).map((role) => role.name).join(", ") || "Standart kullanıcı") : "Yükleniyor…";
  const scopeSummary = me ? (uniqueScopeTypes.map((scope) => scopeLabels[scope] ?? "Özel kapsam").join(", ") || "Kendi kayıtları") : "Yükleniyor…";
  const hasNotifications = permissionCodes.some((permission) => permission.startsWith("notification."));

  return <main className="page-shell">
    <section className="overview-banner">
      <div className="overview-content">
        <span className="overview-kicker"><span className="status-dot"/> Oturum aktif</span>
        <h1>{me ? `Hoş geldiniz, ${me.username}` : "Personel ve İdari İşler Platformu"}</h1>
        <p>Günlük insan kaynakları ve idari operasyonlarınıza tek, sade ve güvenli çalışma alanından ulaşın.</p>
      </div>
      <div className="overview-profile">
        <span>Oturum özeti</span>
        <strong>{me ? (me.email ?? me.username) : "Kullanıcı bilgileri yükleniyor"}</strong>
        <small>{message}</small>
        <div className="role-chips">{me?.roles.slice(0, 4).map((role) => <span className="role-chip" key={role.id}>{role.name}</span>)}</div>
      </div>
    </section>

    <section className="content-grid dashboard-content-grid">
      <div className="panel">
        <div className="panel-heading"><div><span className="page-eyebrow">İşe başlayın</span><h2>Çalışma alanınızı seçin</h2><p>Yapmak istediğiniz işleme göre ilgili alanı açın.</p></div><strong>{me ? visibleModules.length : "…"}</strong></div>
        <div className="module-grid">
          {!me ? <p className="muted">Çalışma alanları yükleniyor…</p> : null}
          {visibleModules.map((item) => <Link className="module-card" href={item.href} key={item.href}>
            <span className="module-icon"><Icon name={item.icon}/></span>
            <span className="module-card-copy"><strong>{item.title}</strong><span>{item.description}</span><small>Modülü aç <Icon name="arrow" size={14}/></small></span>
          </Link>)}
          {me && visibleModules.length === 0 ? <p className="muted">Hesabınız için erişilebilir bir operasyon modülü bulunmuyor.</p> : null}
        </div>
      </div>
      <aside className="panel dashboard-start-panel">
        <div className="panel-heading"><div><span className="page-eyebrow">Hesabınız hazır</span><h2>Size açık alanlar</h2><p>Teknik yetki kodları yerine erişiminizin kısa özeti.</p></div></div>
        <div className="dashboard-facts">
          <div><span>Çalışma alanı</span><strong>{me ? `${visibleModules.length} alan kullanıma açık` : "Yükleniyor…"}</strong></div>
          <div><span>Erişim profili</span><strong>{roleSummary}</strong></div>
          <div><span>İşlem kapsamı</span><strong>{scopeSummary}</strong></div>
        </div>
        {hasNotifications ? <Link className="secondary-button dashboard-notification-link" href="/notifications"><Icon name="bell" size={16}/> Bildirim merkezini aç</Link> : null}
        <div className="notice dashboard-session-notice"><span className="status-dot"/><span>{message}</span></div>
        <p className="dashboard-help">Aradığınız alanı bulamazsanız sol menüdeki arama kutusunu kullanabilirsiniz.</p>
      </aside>
    </section>
  </main>;
}
