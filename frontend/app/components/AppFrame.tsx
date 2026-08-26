"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ReactNode, useEffect, useMemo, useRef, useState } from "react";
import { Icon, IconName } from "./Icon";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
const productTitle = "Personel & İdari İşler Platformu";
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type MeResponse = { username: string; email: string | null; permissions: { code: string }[] };
type NavItem = { href: string; label: string; icon: IconName; prefixes: string[] };
type NavGroup = { label: string; items: NavItem[] };

const publicRoutes = new Set(["/", "/login", "/api-health"]);
const navGroups: NavGroup[] = [
  { label: "Genel", items: [
    { href: "/dashboard", label: "Genel Bakış", icon: "home", prefixes: [""] },
    { href: "/notifications", label: "Bildirimler", icon: "bell", prefixes: ["notification."] },
  ]},
  { label: "İnsan Kaynakları", items: [
    { href: "/personnel", label: "Personel", icon: "people", prefixes: ["personnel."] },
    { href: "/documents", label: "Özlük ve Belgeler", icon: "box", prefixes: ["documents."] },
    { href: "/leave", label: "İzin Yönetimi", icon: "calendar", prefixes: ["leave."] },
    { href: "/attendance", label: "Puantaj ve Vardiya", icon: "calendar", prefixes: ["attendance."] },
    { href: "/payroll", label: "Bordro ve Ücret", icon: "wallet", prefixes: ["payroll."] },
  ]},
  { label: "İdari Operasyonlar", items: [
    { href: "/camp", label: "Kamp ve Konaklama", icon: "building", prefixes: ["camp."] },
    { href: "/meal", label: "Yemek Takibi", icon: "wallet", prefixes: ["meal."] },
    { href: "/assets", label: "Zimmet ve Stok", icon: "box", prefixes: ["administration.asset.", "administration.stock."] },
    { href: "/vehicles", label: "Araç Yönetimi", icon: "building", prefixes: ["administration.vehicle."] },
    { href: "/administration", label: "İdari İşler", icon: "workflow", prefixes: ["administration.task.", "administration.contract.", "administration.reminder."] },
  ]},
  { label: "Yönetim", items: [
    { href: "/workflow", label: "Talep ve Onaylar", icon: "workflow", prefixes: ["workflow."] },
    { href: "/reports", label: "Raporlama", icon: "chart", prefixes: ["reporting.", "finance.cost."] },
    { href: "/integrations", label: "Entegrasyonlar", icon: "plug", prefixes: ["integration.", "erp."] },
    { href: "/organization", label: "Organizasyon", icon: "building", prefixes: ["organization."] },
    { href: "/security", label: "Sistem Yönetimi", icon: "settings", prefixes: ["system.", "audit."] },
  ]},
];
const pageLabels: Record<string, string> = {
  "/login": "Güvenli Giriş", "/api-health": "Sistem Durumu",
  "/dashboard": "Genel Bakış", "/personnel": "Personel Yönetimi", "/documents": "Özlük ve Belgeler",
  "/leave": "İzin Yönetimi", "/attendance": "Puantaj ve Vardiya", "/payroll": "Bordro ve Ücret",
  "/camp": "Kamp ve Konaklama", "/meal": "Yemek Takibi", "/assets": "Zimmet ve Stok",
  "/vehicles": "Araç Yönetimi", "/administration": "İdari İşler", "/workflow": "Talep ve Onaylar",
  "/notifications": "Bildirim Merkezi", "/reports": "Raporlama ve Maliyet", "/integrations": "Entegrasyon Merkezi",
  "/imports": "Excel İçe Aktarım", "/erp": "ERP Merkezi", "/organization": "Organizasyon", "/security": "Sistem Yönetimi",
};

export function AppFrame({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const [me, setMe] = useState<MeResponse | null>(null);
  const [menuOpen, setMenuOpen] = useState(false);
  const [navQuery, setNavQuery] = useState("");
  const [openGroups, setOpenGroups] = useState<Set<string>>(() => new Set(["Genel"]));
  const menuButtonRef = useRef<HTMLButtonElement>(null);
  const menuCloseButtonRef = useRef<HTMLButtonElement>(null);
  const isPublic = publicRoutes.has(pathname);
  useEffect(() => { if (!isPublic) void loadSession(); }, [isPublic]);
  useEffect(() => { setMenuOpen(false); setNavQuery(""); }, [pathname]);
  useEffect(() => {
    if (!menuOpen) return;
    const focusFrame = window.requestAnimationFrame(() => menuCloseButtonRef.current?.focus());
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === "Escape") closeMenu(true); };
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      window.cancelAnimationFrame(focusFrame);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [menuOpen]);

  function closeMenu(returnFocus = false) {
    setMenuOpen(false);
    if (returnFocus) window.requestAnimationFrame(() => menuButtonRef.current?.focus());
  }

  async function loadSession() {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh();
    if (!token) return;
    let response = await fetch(`${apiBase}/api/v1/auth/me`, { headers: { Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status === 401) {
      token = await refresh(); if (!token) return;
      response = await fetch(`${apiBase}/api/v1/auth/me`, { headers: { Authorization: `Bearer ${token}` }, credentials: "include" });
    }
    if (response.ok) setMe(await response.json() as MeResponse);
  }
  async function refresh(): Promise<string | null> {
    try {
      const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" });
      if (!response.ok) return null;
      const body = await response.json() as AuthResponse;
      sessionStorage.setItem("pp_access_token", body.accessToken);
      sessionStorage.setItem("pp_access_token_expires_at", body.accessTokenExpiresAt);
      return body.accessToken;
    } catch { return null; }
  }
  async function logout() {
    try { await fetch(`${apiBase}/api/v1/auth/logout`, { method: "POST", credentials: "include" }); }
    finally {
      sessionStorage.removeItem("pp_access_token"); sessionStorage.removeItem("pp_access_token_expires_at");
      window.location.replace("/login");
    }
  }

  function toggleGroup(label: string) {
    setOpenGroups((current) => {
      const next = new Set(current);
      if (next.has(label)) next.delete(label); else next.add(label);
      return next;
    });
  }

  const permissions = useMemo(() => new Set(me?.permissions.map((item) => item.code) ?? []), [me]);
  const visibleGroups = useMemo(() => navGroups.map((group) => ({ ...group, items: group.items.filter((item) =>
    item.href === "/dashboard" || item.prefixes.some((prefix) => [...permissions].some((permission) => permission.startsWith(prefix)))
  )})).filter((group) => group.items.length > 0), [permissions]);
  const activeHref = visibleGroups.flatMap((group) => group.items)
    .filter((item) => pathname === item.href || pathname.startsWith(`${item.href}/`))
    .sort((a, b) => b.href.length - a.href.length)[0]?.href;
  const activeGroupLabel = visibleGroups.find((group) => group.items.some((item) => item.href === activeHref))?.label;
  const normalizedNavQuery = navQuery.trim().toLocaleLowerCase("tr-TR");
  const filteredGroups = useMemo(() => {
    if (!normalizedNavQuery) return visibleGroups;
    return visibleGroups.map((group) => {
      if (group.label.toLocaleLowerCase("tr-TR").includes(normalizedNavQuery)) return group;
      return { ...group, items: group.items.filter((item) => item.label.toLocaleLowerCase("tr-TR").includes(normalizedNavQuery)) };
    }).filter((group) => group.items.length > 0);
  }, [normalizedNavQuery, visibleGroups]);
  useEffect(() => {
    if (!activeGroupLabel) return;
    setOpenGroups((current) => current.has(activeGroupLabel) ? current : new Set([...current, activeGroupLabel]));
  }, [activeGroupLabel]);
  const pageLabel = Object.entries(pageLabels).filter(([route]) => pathname === route || pathname.startsWith(`${route}/`))
    .sort(([a], [b]) => b.length - a.length)[0]?.[1] ?? "Platform";
  const documentTitle = pathname === "/" ? productTitle : `${pageLabel} | ${productTitle}`;
  const initials = (me?.username ?? "K").slice(0, 2).toLocaleUpperCase("tr-TR");

  if (isPublic) return <><title>{documentTitle}</title>{children}</>;
  return <><title>{documentTitle}</title><div className="app-frame">
    <a className="skip-link" href="#main-content">Ana içeriğe geç</a>
    <aside id="app-navigation" className={`app-sidebar ${menuOpen ? "is-open" : ""}`} aria-label="Ana navigasyon">
      <div className="app-brand"><span className="app-logo" aria-hidden="true">Pİ</span><span className="app-brand-copy"><strong>Personel & İdari</strong><small>İşler Platformu</small></span><button ref={menuCloseButtonRef} className="sidebar-close" type="button" onClick={() => closeMenu(true)} aria-label="Menüyü kapat"><Icon name="close"/></button></div>
      <nav className="app-nav" aria-busy={!me}>
        {me ? <><label className="nav-search"><span className="nav-search-icon"><Icon name="search" size={17}/></span><span className="nav-search-label">Menüde ara</span><input type="search" value={navQuery} onChange={(event) => setNavQuery(event.target.value)} placeholder="Menüde ara…" autoComplete="off"/></label>
          {filteredGroups.length ? filteredGroups.map((group, index) => {
            const expanded = Boolean(normalizedNavQuery) || openGroups.has(group.label);
            const panelId = `nav-group-${index}`;
            return <div className={`nav-group ${expanded ? "is-open" : ""}`} key={group.label}>
              <button className="nav-group-toggle" type="button" onClick={() => toggleGroup(group.label)} aria-expanded={expanded} aria-controls={panelId} disabled={Boolean(normalizedNavQuery)}>
                <span>{group.label}</span><span className="nav-group-chevron"><Icon name="arrow" size={14}/></span>
              </button>
              <div className="nav-group-items" id={panelId} hidden={!expanded}>{group.items.map((item) => <Link className={`nav-link ${activeHref === item.href ? "is-active" : ""}`} href={item.href} key={item.href} aria-current={activeHref === item.href ? "page" : undefined}><span className="nav-icon"><Icon name={item.icon}/></span><span>{item.label}</span></Link>)}</div>
            </div>;
          }) : <p className="nav-empty" role="status">Bu adla eşleşen bir menü bulunamadı.</p>}
        </> : <div className="nav-loading" role="status" aria-live="polite" aria-label="Menü yükleniyor"><span/><span/><span/><span/><span/></div>}
      </nav>
      <div className="app-sidebar-footer"><div className="sidebar-user"><span className="user-avatar">{initials}</span><span><strong>{me?.username ?? "Oturum yükleniyor"}</strong><small>{me?.email ?? "Personel Platformu"}</small></span></div><button className="icon-button inverse" type="button" onClick={() => void logout()} aria-label="Çıkış yap"><Icon name="logout"/></button></div>
    </aside>
    {menuOpen ? <button className="sidebar-scrim" type="button" aria-label="Menüyü kapat" onClick={() => closeMenu(true)}/> : null}
    <div className="app-main"><header className="app-topbar"><div className="topbar-context"><button ref={menuButtonRef} className="mobile-menu-button" type="button" onClick={() => setMenuOpen(true)} aria-label="Menüyü aç" aria-expanded={menuOpen} aria-controls="app-navigation"><Icon name="menu"/></button><span>Personel Platformu</span><strong>{pageLabel}</strong></div><div className="topbar-status"><span className="status-dot" aria-hidden="true"/> Güvenli oturum</div></header><div className="app-content" id="main-content" tabIndex={-1}>{children}</div></div>
  </div></>;
}
