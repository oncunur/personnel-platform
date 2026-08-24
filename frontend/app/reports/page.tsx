"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { Icon } from "../components/Icon";
import { OperationDisclosure } from "../components/OperationDisclosure";
import { PageHeader } from "../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type Permission = { code: string };
type Scope = { scopeType: string; scopeId: string | null };
type Me = { permissions: Permission[]; scopes: Scope[] };
type Named = { id: string; code: string; name: string };
type Cost = { currency: string; payrollCost: number; mealCost: number; accommodationCost: number; totalCost: number };
type Management = { projectId: string; projectCode: string; projectName: string; headcount: number; manDays: number; workedHours: number; approvedOvertimeHours: number; mealQuantity: number; accommodationNights: number; costs: Cost[] };
type Ledger = { id: string; costDate: string; sourceType: string; employeeNo: string | null; employeeName: string | null; projectId: string | null; projectCode: string | null; projectName: string | null; costCenterCode: string | null; category: string; quantity: number; unit: string; amount: number; currency: string; allocationBasis: string };
type ExportJob = { id: string; reportType: string; format: string; status: string; fileName: string | null; fileSizeBytes: number | null; createdAt: string; errorMessage: string | null };
type AuthResponse = { accessToken: string };

const formatDate = (value: string) => new Date(`${value}T00:00:00`).toLocaleDateString("tr-TR");
const exportStatus = (value: string) => value === "COMPLETED" ? "Hazır" : value === "FAILED" ? "Başarısız" : value === "PROCESSING" ? "Hazırlanıyor" : value === "QUEUED" ? "Sırada" : value;
const reportLabel = (value: string) => value === "COST_LEDGER" ? "Maliyet kayıtları" : value === "MANAGEMENT" ? "Yönetim özeti" : value === "PROJECT_360" ? "Proje 360" : value;
const sourceLabel = (value: string) => value === "PAYROLL" ? "Bordro" : value === "MEAL" ? "Yemek" : value === "ACCOMMODATION" ? "Konaklama" : value;

export default function ReportsPage() {
  const today = new Date().toISOString().slice(0, 10);
  const monthStart = `${today.slice(0, 7)}-01`;
  const [me, setMe] = useState<Me | null>(null);
  const [companies, setCompanies] = useState<Named[]>([]);
  const [projects, setProjects] = useState<Named[]>([]);
  const [companyId, setCompanyId] = useState("");
  const [projectId, setProjectId] = useState("");
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const [management, setManagement] = useState<Management[]>([]);
  const [ledger, setLedger] = useState<Ledger[]>([]);
  const [exports, setExports] = useState<ExportJob[]>([]);
  const [message, setMessage] = useState("Rapor merkezi yükleniyor…");
  const [busy, setBusy] = useState(false);
  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const totalHeadcount = useMemo(() => management.reduce((sum, row) => sum + row.headcount, 0), [management]);
  const totalWorkedHours = useMemo(() => management.reduce((sum, row) => sum + row.workedHours, 0), [management]);
  const pendingExports = useMemo(() => exports.filter(x => !["COMPLETED", "FAILED"].includes(x.status)), [exports]);

  useEffect(() => { void initialize(); }, []);
  useEffect(() => { if (companyId) void loadCompany(); }, [companyId]);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    let companyRows: Named[] = [];
    if (current.permissions.some(x => x.code === "organization.company.view")) companyRows = await json<Named[]>("/api/v1/organization/companies") ?? [];
    if (companyRows.length === 0) companyRows = current.scopes.filter(x => x.scopeType === "COMPANY" && x.scopeId).map(x => ({ id: x.scopeId!, code: "SCOPE", name: x.scopeId! }));
    setCompanies(companyRows);
    if (companyRows.length) setCompanyId(companyRows[0].id);
    setMessage("Maliyet kayıtları ve raporlama merkezi hazır.");
  }

  async function loadCompany() {
    const codes = new Set(me?.permissions.map(x => x.code) ?? []);
    const [projectRows, managementRows, ledgerRows, exportRows] = await Promise.all([
      codes.has("organization.project.view") ? json<Named[]>(`/api/v1/organization/projects?companyId=${companyId}`) : Promise.resolve(null),
      codes.has("reporting.view") ? json<Management[]>(`/api/v1/reports/management?companyId=${companyId}&from=${from}&to=${to}`) : Promise.resolve(null),
      codes.has("finance.cost.view") ? json<Ledger[]>(`/api/v1/finance/cost-ledger?companyId=${companyId}&from=${from}&to=${to}&take=500`) : Promise.resolve(null),
      codes.has("reporting.export") ? json<ExportJob[]>(`/api/v1/reports/exports?companyId=${companyId}&take=100`) : Promise.resolve(null),
    ]);
    setProjects(projectRows ?? []); setManagement(managementRows ?? []); setLedger(ledgerRows ?? []); setExports(exportRows ?? []);
    if (projectRows?.length && !projectRows.some(x => x.id === projectId)) setProjectId(projectRows[0].id);
  }

  async function refreshReports(event?: FormEvent) {
    event?.preventDefault(); setBusy(true);
    try { await loadCompany(); setMessage("Rapor verileri yenilendi."); } finally { setBusy(false); }
  }

  async function syncLedger() {
    setBusy(true);
    try {
      const r = await authFetch(`/api/v1/finance/cost-ledger/sync?companyId=${companyId}`, { method: "POST" });
      if (!r?.ok) { setMessage(await errorMessage(r, "Maliyet kayıtları işlenemedi.")); return; }
      const body = await r.json() as { payrollEntriesCreated: number; mealEntriesCreated: number; accommodationEntriesCreated: number; duplicates: number };
      setMessage(`Maliyet kayıtları işlendi: bordro ${body.payrollEntriesCreated}, yemek ${body.mealEntriesCreated}, konaklama ${body.accommodationEntriesCreated}, daha önce eklenen ${body.duplicates}.`);
      await loadCompany();
    } finally { setBusy(false); }
  }

  async function createExport(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const fd = new FormData(event.currentTarget); setBusy(true);
    try {
      const reportType = String(fd.get("reportType"));
      const r = await authFetch("/api/v1/reports/exports", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ companyId, reportType, format: fd.get("format"), projectId: reportType === "PROJECT_360" ? projectId || null : null, from, to }) });
      if (!r?.ok) { setMessage(await errorMessage(r, "Export kuyruğa alınamadı.")); return; }
      setMessage("Export kuyruğa alındı; worker tamamladığında indirilebilir olacak.");
      setExports(await json<ExportJob[]>(`/api/v1/reports/exports?companyId=${companyId}&take=100`) ?? []);
    } finally { setBusy(false); }
  }

  async function downloadExport(id: string) {
    setBusy(true);
    try {
      const r = await authFetch(`/api/v1/reports/exports/${id}/file`);
      if (!r?.ok) { setMessage(await errorMessage(r, "Export henüz indirilemedi.")); return; }
      const blob = await r.blob(); const url = URL.createObjectURL(blob); window.open(url, "_blank", "noopener,noreferrer"); window.setTimeout(() => URL.revokeObjectURL(url), 60000);
    } finally { setBusy(false); }
  }

  async function json<T>(path: string): Promise<T | null> { const r = await authFetch(path); return r?.ok ? await r.json() as T : null; }
  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> { let token = sessionStorage.getItem("pp_access_token") ?? await refresh(); if (!token) return null; let r = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" }); if (r.status !== 401) return r; token = await refresh(); if (!token) return r; return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" }); }
  async function refresh(): Promise<string | null> { try { const r = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!r.ok) return null; const b = await r.json() as AuthResponse; sessionStorage.setItem("pp_access_token", b.accessToken); return b.accessToken; } catch { return null; } }
  async function errorMessage(r: Response | null, fallback: string) { if (!r) return fallback; const b = await r.json().catch(() => null) as { error?: { code?: string; message?: string } } | null; return b?.error?.code ? `${b.error.code}: ${b.error.message ?? fallback}` : b?.error?.message ?? fallback; }
  const costText = (costs: Cost[]) => costs.map(x => `${x.currency} ${x.totalCost.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`).join(" · ") || "—";

  return <main className="page-shell">
    <PageHeader eyebrow="Raporlama ve maliyet" title="Yönetim raporları" description="Proje performansını, merkezi maliyet kayıtlarını ve dışa aktarımları aynı dönem üzerinden inceleyin." status={message}/>

    <section className="stat-grid" aria-label="Rapor özeti">
      <article className="stat-card"><span className="stat-icon"><Icon name="building"/></span><span className="stat-copy"><strong>{management.length}</strong><span>Raporlanan proje</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{totalHeadcount}</strong><span>Toplam çalışan sayısı</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="calendar"/></span><span className="stat-copy"><strong>{totalWorkedHours.toLocaleString("tr-TR", { maximumFractionDigits: 0 })}</strong><span>Çalışılan saat</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="chart"/></span><span className="stat-copy"><strong>{pendingExports.length}</strong><span>Hazırlanan dosya</span></span></article>
    </section>

    <section className="panel workspace-panel wide-workspace"><div className="workspace-copy"><span className="eyebrow dark">Rapor kapsamı</span><h2>Şirket ve dönem</h2><p>Tüm göstergeler, maliyetler ve dışa aktarımlar bu dönem üzerinden hazırlanır.</p></div><form className="inline-form" onSubmit={refreshReports}><label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)} required><option value="">Şirket seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Başlangıç<input type="date" value={from} onChange={e => setFrom(e.target.value)} required/></label><label className="field-label">Bitiş<input type="date" value={to} onChange={e => setTo(e.target.value)} required/></label><div className="action-row workspace-actions"><button className="primary-button" disabled={busy || !companyId}>Raporu yenile</button>{permissions.has("finance.cost.process") ? <button className="secondary-button" type="button" disabled={busy || !companyId} onClick={() => void syncLedger()}>Maliyetleri eşitle</button> : null}</div></form></section>

    <div className="content-stack">
      {permissions.has("reporting.view") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Yönetim özeti</span><h2>Proje performansı</h2><p>İnsan gücü, çalışma, tüketim ve maliyet göstergelerini proje bazında karşılaştırın.</p></div><strong>{management.length}</strong></div><div className="table-wrap responsive-table-wrap" role="region" aria-label="Proje performansı" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Proje</th><th>Çalışan / adam-gün</th><th>Saat / fazla mesai</th><th>Yemek / gece</th><th>Toplam maliyet</th><th>Detay</th></tr></thead><tbody>{management.length === 0 ? <tr><td className="empty-row" colSpan={6}>Bu şirket ve dönem için proje verisi yok.</td></tr> : management.map(x => <tr key={x.projectId}><td data-label="Proje"><strong>{x.projectCode}</strong><small>{x.projectName}</small></td><td data-label="Çalışan / adam-gün">{x.headcount} / {x.manDays.toLocaleString("tr-TR",{maximumFractionDigits:2})}</td><td data-label="Saat / fazla mesai">{x.workedHours.toLocaleString("tr-TR",{maximumFractionDigits:2})} / {x.approvedOvertimeHours.toLocaleString("tr-TR",{maximumFractionDigits:2})}</td><td data-label="Yemek / gece">{x.mealQuantity.toLocaleString("tr-TR",{maximumFractionDigits:2})} / {x.accommodationNights.toLocaleString("tr-TR")}</td><td data-label="Toplam maliyet"><strong>{costText(x.costs)}</strong></td><td data-label="Detay"><a className="table-button" href={`/reports/projects/${x.projectId}?from=${from}&to=${to}`}>Proje 360</a></td></tr>)}</tbody></table></div></section> : null}

      {permissions.has("finance.cost.view") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Maliyet defteri</span><h2>Merkezi maliyet kayıtları</h2><p>Bordro, yemek ve konaklama kaynaklarından gelen değiştirilemeyen kayıtlar.</p></div><strong>{ledger.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>Kaynak</th><th>Personel</th><th>Proje / maliyet merkezi</th><th>Kategori</th><th>Miktar</th><th>Tutar</th><th>Dağıtım</th></tr></thead><tbody>{ledger.length === 0 ? <tr><td className="empty-row" colSpan={8}>Bu dönem için maliyet kaydı bulunmuyor.</td></tr> : ledger.map(x => <tr key={x.id}><td>{formatDate(x.costDate)}</td><td><span className="status-badge">{sourceLabel(x.sourceType)}</span></td><td>{x.employeeNo ?? "—"}<small>{x.employeeName ?? "Personel bağlantısı yok"}</small></td><td>{x.projectCode ?? "Dağıtılmamış"}<small>{x.costCenterCode ?? "Maliyet merkezi yok"}</small></td><td>{x.category}</td><td>{x.quantity.toLocaleString("tr-TR")} {x.unit}</td><td><strong>{x.amount.toLocaleString("tr-TR",{minimumFractionDigits:2})} {x.currency}</strong></td><td>{x.allocationBasis}</td></tr>)}</tbody></table></div></section> : null}

      {permissions.has("reporting.export") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Dosya merkezi</span><h2>Excel veya PDF dosyaları</h2><p>Rapor arka planda hazırlanır; tamamlandığında buradan indirilebilir.</p></div><strong>{exports.length}</strong></div><OperationDisclosure title="Yeni rapor dosyası oluştur" description="Proje 360 seçerseniz proje alanı zorunlu hale gelir."><form className="inline-form" onSubmit={createExport}><label className="field-label">Rapor<select name="reportType" required><option value="COST_LEDGER">Maliyet kayıtları</option><option value="MANAGEMENT">Yönetim özeti</option><option value="PROJECT_360">Proje 360</option></select></label><label className="field-label">Proje<select value={projectId} onChange={e => setProjectId(e.target.value)}><option value="">Proje seçin</option>{projects.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Dosya biçimi<select name="format"><option value="XLSX">Excel</option><option value="PDF">PDF</option></select></label><button className="primary-button" disabled={busy || !companyId}>Dosyayı hazırla</button></form></OperationDisclosure><div className="table-wrap responsive-table-wrap" role="region" aria-label="Hazırlanan rapor dosyaları" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Rapor</th><th>Biçim</th><th>Durum</th><th>Oluşturma</th><th>Dosya</th></tr></thead><tbody>{exports.length === 0 ? <tr><td className="empty-row" colSpan={5}>Henüz dışa aktarım oluşturulmadı.</td></tr> : exports.map(x => <tr key={x.id}><td data-label="Rapor"><strong>{reportLabel(x.reportType)}</strong></td><td data-label="Biçim">{x.format === "XLSX" ? "Excel" : x.format}</td><td data-label="Durum"><span className={`status-badge ${x.status === "COMPLETED" ? "success" : x.status === "FAILED" ? "danger" : "warning"}`}>{exportStatus(x.status)}</span>{x.errorMessage ? <small>{x.errorMessage}</small> : null}</td><td data-label="Oluşturma">{new Date(x.createdAt).toLocaleString("tr-TR")}</td><td data-label="Dosya">{x.status === "COMPLETED" ? <button className="table-button" disabled={busy} onClick={() => void downloadExport(x.id)}>{x.fileName ?? "İndir"}</button> : "—"}</td></tr>)}</tbody></table></div></section> : null}
    </div>
  </main>;
}
