"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

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
    setMessage("Maliyet ledger ve raporlama merkezi hazır.");
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
      if (!r?.ok) { setMessage(await errorMessage(r, "Maliyet ledger işlenemedi.")); return; }
      const body = await r.json() as { payrollEntriesCreated: number; mealEntriesCreated: number; accommodationEntriesCreated: number; duplicates: number };
      setMessage(`Ledger işlendi: bordro ${body.payrollEntriesCreated}, yemek ${body.mealEntriesCreated}, konaklama ${body.accommodationEntriesCreated}, mevcut ${body.duplicates}.`);
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

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 13 · COST / REPORTING</span><h1>Raporlama & Maliyet Merkezi</h1><p>{message}</p></section>
    <section className="panel audit-panel"><form className="inline-form" onSubmit={refreshReports}><label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)} required><option value="">Seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Başlangıç<input type="date" value={from} onChange={e => setFrom(e.target.value)} required/></label><label className="field-label">Bitiş<input type="date" value={to} onChange={e => setTo(e.target.value)} required/></label><button className="primary-button" disabled={busy || !companyId}>Raporu yenile</button>{permissions.has("finance.cost.process") ? <button className="secondary-button" type="button" disabled={busy || !companyId} onClick={() => void syncLedger()}>Cost ledger işle</button> : null}</form></section>

    {permissions.has("reporting.view") ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">YÖNETİM DASHBOARD</span><h2>Proje KPI</h2></div><strong>{management.length} proje</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Proje</th><th>HC / Man-day</th><th>Saat / FM</th><th>Yemek / Gece</th><th>Toplam Maliyet</th><th></th></tr></thead><tbody>{management.length === 0 ? <tr><td colSpan={6}>Bu filtrede proje verisi yok.</td></tr> : management.map(x => <tr key={x.projectId}><td><strong>{x.projectCode}</strong><small>{x.projectName}</small></td><td>{x.headcount} / {x.manDays.toFixed(2)}</td><td>{x.workedHours.toFixed(2)} / {x.approvedOvertimeHours.toFixed(2)}</td><td>{x.mealQuantity.toFixed(2)} / {x.accommodationNights}</td><td>{costText(x.costs)}</td><td><a className="table-button" href={`/reports/projects/${x.projectId}?from=${from}&to=${to}`}>Project 360</a></td></tr>)}</tbody></table></div></section> : null}

    {permissions.has("finance.cost.view") ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">IMMUTABLE LEDGER</span><h2>Merkezi Maliyet Kayıtları</h2></div><strong>{ledger.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>Kaynak</th><th>Personel</th><th>Proje / CC</th><th>Kategori</th><th>Miktar</th><th>Tutar</th><th>Dağıtım</th></tr></thead><tbody>{ledger.length === 0 ? <tr><td colSpan={8}>Ledger kaydı bulunmuyor.</td></tr> : ledger.map(x => <tr key={x.id}><td>{x.costDate}</td><td>{x.sourceType}</td><td>{x.employeeNo ?? "—"}<small>{x.employeeName ?? ""}</small></td><td>{x.projectCode ?? "UNALLOCATED"}<small>{x.costCenterCode ?? "—"}</small></td><td>{x.category}</td><td>{x.quantity} {x.unit}</td><td><strong>{x.amount.toFixed(2)} {x.currency}</strong></td><td>{x.allocationBasis}</td></tr>)}</tbody></table></div></section> : null}

    {permissions.has("reporting.export") ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">BACKGROUND EXPORT</span><h2>XLSX / PDF</h2></div></div><form className="inline-form" onSubmit={createExport}><label className="field-label">Rapor<select name="reportType" required><option value="COST_LEDGER">Cost Ledger</option><option value="MANAGEMENT">Management</option><option value="PROJECT_360">Project 360</option></select></label><label className="field-label">Proje<select value={projectId} onChange={e => setProjectId(e.target.value)}><option value="">Seçin</option>{projects.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Format<select name="format"><option value="XLSX">XLSX</option><option value="PDF">PDF</option></select></label><button className="primary-button" disabled={busy || !companyId}>Export oluştur</button></form><div className="table-wrap"><table className="data-table"><thead><tr><th>Rapor</th><th>Format</th><th>Durum</th><th>Oluşturma</th><th>Dosya</th></tr></thead><tbody>{exports.length === 0 ? <tr><td colSpan={5}>Export işi yok.</td></tr> : exports.map(x => <tr key={x.id}><td>{x.reportType}</td><td>{x.format}</td><td><span className={`status-badge ${x.status === "COMPLETED" ? "success" : x.status === "FAILED" ? "danger" : ""}`}>{x.status}</span>{x.errorMessage ? <small>{x.errorMessage}</small> : null}</td><td>{new Date(x.createdAt).toLocaleString("tr-TR")}</td><td>{x.status === "COMPLETED" ? <button className="table-button" disabled={busy} onClick={() => void downloadExport(x.id)}>{x.fileName ?? "İndir"}</button> : "—"}</td></tr>)}</tbody></table></div></section> : null}
  </main>;
}
