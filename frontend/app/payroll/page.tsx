"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { Icon } from "../components/Icon";
import { PageHeader } from "../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type Permission = { code: string };
type Me = { permissions: Permission[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type Company = { id: string; code: string; name: string; defaultCurrency: string; isActive: boolean };
type Employee = { id: string; companyId: string; employeeNo: string; firstName: string; lastName: string; status: string };
type EmployeePage = { items: Employee[]; totalCount: number };
type Compensation = { id: string; companyId: string; employeeId: string; employeeNo: string; employeeName: string; validFrom: string; validUntilExclusive: string | null; monthlyBaseSalary: number; currency: string; overtimeMultiplier: number; version: number };
type Period = { id: string; companyId: string; year: number; month: number; revision: number; previousRevisionId: string | null; status: string; calculationVersion: string; calculatedAt: string | null; approvedAt: string | null; closedAt: string | null; version: number };
type PayrollResult = { id: string; payrollPeriodId: string; employeeId: string; employeeNo: string; employeeName: string; monthlyBaseSalarySnapshot: number; currencySnapshot: string; overtimeMultiplierSnapshot: number; plannedMinutes: number; workedMinutes: number; paidLeaveMinutes: number; approvedOvertimeMinutes: number; baseSalaryAmount: number; absenceDeductionAmount: number; overtimeEarningAmount: number; payBeforeStatutory: number; mealEmployerCost: number; accommodationEmployerCost: number; employerCostBeforeStatutory: number; calculatedAt: string };

function firstDayOfCurrentMonth() {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;
}

export default function PayrollPage() {
  const [me, setMe] = useState<Me | null>(null);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [companyId, setCompanyId] = useState("");
  const [employeeId, setEmployeeId] = useState("");
  const [compensations, setCompensations] = useState<Compensation[]>([]);
  const [periods, setPeriods] = useState<Period[]>([]);
  const [selectedPeriodId, setSelectedPeriodId] = useState("");
  const [results, setResults] = useState<PayrollResult[]>([]);
  const [message, setMessage] = useState("Bordro verileri yükleniyor…");
  const [busy, setBusy] = useState(false);
  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const selectedCompany = companies.find(x => x.id === companyId);
  const selectedPeriod = periods.find(x => x.id === selectedPeriodId);

  useEffect(() => { void initialize(); }, []);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const companyRows = await json<Company[]>("/api/v1/organization/companies");
    setCompanies(companyRows ?? []);
    const first = companyRows?.[0]?.id ?? "";
    setCompanyId(first);
    if (first) await loadCompany(first);
    setMessage("Bordro merkezi hazır.");
  }

  async function loadCompany(id: string) {
    setCompanyId(id); setEmployeeId(""); setCompensations([]); setSelectedPeriodId(""); setResults([]);
    const [employeePage, periodRows] = await Promise.all([
      json<EmployeePage>(`/api/v1/personnel/employees?companyId=${id}&pageSize=100`),
      permissions.has("payroll.period.view") || me === null ? json<Period[]>("/api/v1/payroll/periods") : Promise.resolve(null),
    ]);
    setEmployees(employeePage?.items ?? []);
    setPeriods((periodRows ?? []).filter(x => x.companyId === id));
  }

  async function loadCompensations(id: string) {
    setEmployeeId(id);
    if (!id || !permissions.has("payroll.compensation.view")) { setCompensations([]); return; }
    setCompensations(await json<Compensation[]>(`/api/v1/payroll/compensations?employeeId=${id}`) ?? []);
  }

  async function reloadPeriods(selectId?: string) {
    if (!permissions.has("payroll.period.view")) return;
    const rows = await json<Period[]>("/api/v1/payroll/periods") ?? [];
    const filtered = rows.filter(x => x.companyId === companyId);
    setPeriods(filtered);
    if (selectId) setSelectedPeriodId(selectId);
  }

  async function selectPeriod(id: string) {
    setSelectedPeriodId(id); setResults([]);
    if (!id || !permissions.has("payroll.period.view")) return;
    setResults(await json<PayrollResult[]>(`/api/v1/payroll/periods/${id}/results`) ?? []);
  }

  async function createCompensation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!employeeId) return; setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const response = await authFetch("/api/v1/payroll/compensations", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ employeeId, validFrom: form.get("validFrom"), validUntilExclusive: form.get("validUntilExclusive") || null, monthlyBaseSalary: Number(form.get("monthlyBaseSalary")), currency: String(form.get("currency") ?? "").toUpperCase(), overtimeMultiplier: Number(form.get("overtimeMultiplier")) }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Ücret tanımı kaydedilemedi.")); return; }
      setCompensations(await json<Compensation[]>(`/api/v1/payroll/compensations?employeeId=${employeeId}`) ?? []);
      setMessage("Tarih-etkin ücret tanımı kaydedildi."); event.currentTarget.reset();
    } finally { setBusy(false); }
  }

  async function createPeriod(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!companyId) return; setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const response = await authFetch("/api/v1/payroll/periods", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ companyId, year: Number(form.get("year")), month: Number(form.get("month")) }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Bordro dönemi oluşturulamadı.")); return; }
      const row = await response.json() as Period; await reloadPeriods(row.id); await selectPeriod(row.id); setMessage(`Bordro dönemi oluşturuldu: ${row.year}/${row.month} rev.${row.revision}`);
    } finally { setBusy(false); }
  }

  async function periodAction(action: "open" | "calculate" | "review" | "approve" | "close") {
    if (!selectedPeriod) return; setBusy(true);
    try {
      const response = await authFetch(`/api/v1/payroll/periods/${selectedPeriod.id}/${action}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: selectedPeriod.version }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Bordro işlemi tamamlanamadı.")); return; }
      const row = await response.json() as Period;
      await reloadPeriods(row.id); await selectPeriod(row.id);
      setMessage(`Bordro durumu: ${payrollStatusLabel(row.status)}.`);
    } finally { setBusy(false); }
  }

  async function json<T>(path: string): Promise<T | null> { const response = await authFetch(path); return response?.ok ? await response.json() as T : null; }
  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh(); if (!token) return null;
    let response = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status !== 401) return response;
    token = await refresh(); if (!token) return response;
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> { try { const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!response.ok) return null; const body = await response.json() as AuthResponse; sessionStorage.setItem("pp_access_token", body.accessToken); return body.accessToken; } catch { return null; } }
  async function errorMessage(response: Response | null, fallback: string) { if (!response) return fallback; const body = await response.json().catch(() => null) as { error?: { code?: string; message?: string } } | null; return body?.error?.code ? `${body.error.code}: ${body.error.message ?? fallback}` : body?.error?.message ?? fallback; }

  const action = selectedPeriod?.status === "DRAFT" && permissions.has("payroll.period.manage") ? ["open", "Dönemi aç"] as const
    : selectedPeriod?.status === "OPEN" && permissions.has("payroll.calculate") ? ["calculate", "Hesapla"] as const
    : selectedPeriod?.status === "CALCULATED" && permissions.has("payroll.review") ? ["review", "İncelemeye al"] as const
    : selectedPeriod?.status === "UNDER_REVIEW" && permissions.has("payroll.approve") ? ["approve", "Onayla"] as const
    : selectedPeriod?.status === "APPROVED" && permissions.has("payroll.close") ? ["close", "Kapat ve kilitle"] as const
    : null;

  const closedPeriods = periods.filter(period => period.status === "CLOSED").length;
  const activePeriodCount = periods.filter(period => period.status !== "CLOSED").length;

  return <main className="page-shell">
    <PageHeader
      eyebrow="İnsan Kaynakları"
      title="Bordro ve Ücret"
      description="Ücret geçmişini, bordro dönemlerini ve hesaplama sonuçlarını kontrollü bir dönem akışında yönetin."
      status={message}
    />

    <section className="stat-grid" aria-label="Bordro göstergeleri">
      <article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{employees.length}</strong><span>Kapsamdaki personel</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="calendar"/></span><span className="stat-copy"><strong>{activePeriodCount}</strong><span>Açık işlem dönemi</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="wallet"/></span><span className="stat-copy"><strong>{closedPeriods}</strong><span>Kapatılan dönem</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="chart"/></span><span className="stat-copy"><strong>{results.length}</strong><span>Seçili dönem sonucu</span></span></article>
    </section>

    <section className="panel workspace-panel">
      <div className="workspace-copy"><span className="page-eyebrow">Çalışma kapsamı</span><h2>{selectedCompany?.name ?? "Şirket seçin"}</h2><p>Personel, ücret ve bordro dönemleri seçilen şirkete göre güncellenir.</p></div>
      <label className="field-label workspace-select">Şirket<select value={companyId} onChange={e => void loadCompany(e.target.value)}><option value="">Şirket seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name} · {x.defaultCurrency}</option>)}</select></label>
    </section>

    <div className="content-stack">
    {permissions.has("payroll.compensation.view") ? <section className="panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Ücret geçmişi</span><h2>Personel ücret tanımları</h2><p>Tarih aralığına göre baz ücret ve fazla mesai çarpanı geçmişini yönetin.</p></div><strong>{compensations.length}</strong></div>
      <div className="selection-bar"><label className="field-label">Personel<select value={employeeId} onChange={e => void loadCompensations(e.target.value)}><option value="">Personel seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label><div className="selection-context"><strong>{employeeId ? `${compensations.length} ücret kaydı` : "Personel bekleniyor"}</strong><span>Ücret geçmişi yalnız seçili personel için gösterilir.</span></div></div>
      {permissions.has("payroll.compensation.manage") && employeeId ? <div className="form-surface"><div className="form-surface-heading"><div><strong>Yeni ücret dönemi ekleyin</strong><span>Tarih aralıkları çakışmamalı; kayıtlar geçmişe dönük izlenir.</span></div></div><form className="inline-form" onSubmit={createCompensation}><label className="field-label">Başlangıç<input name="validFrom" type="date" defaultValue={firstDayOfCurrentMonth()} required/></label><label className="field-label">Bitiş (hariç)<input name="validUntilExclusive" type="date"/></label><label className="field-label">Aylık baz ücret<input name="monthlyBaseSalary" type="number" min="0.01" step="0.01" required/></label><label className="field-label">Döviz<input name="currency" defaultValue={selectedCompany?.defaultCurrency ?? "TRY"} maxLength={3} required/></label><label className="field-label">FM çarpanı<input name="overtimeMultiplier" type="number" min="1" max="5" step="0.01" defaultValue="1.5" required/></label><button className="primary-button" disabled={busy || !employeeId}>{busy ? "Kaydediliyor…" : "Ücret ekle"}</button></form></div> : null}
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Geçerlilik dönemi</th><th>Baz ücret</th><th>FM çarpanı</th></tr></thead><tbody>{compensations.map(x => <tr key={x.id}><td>{formatDate(x.validFrom)} → {x.validUntilExclusive ? formatDate(x.validUntilExclusive) : "Devam ediyor"}</td><td><strong>{formatMoney(x.monthlyBaseSalary, x.currency)}</strong></td><td>{x.overtimeMultiplier.toLocaleString("tr-TR")}×</td></tr>)}{compensations.length === 0 ? <tr><td className="empty-row" colSpan={3}>{employeeId ? "Seçili personel için ücret tanımı yok." : "Ücret geçmişini görüntülemek için personel seçin."}</td></tr> : null}</tbody></table></div>
    </section> : null}

    <section className="panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Dönem yönetimi</span><h2>Bordro dönemleri</h2><p>Oluşturma, hesaplama, inceleme, onay ve kapatma adımlarını sırayla ilerletin.</p></div><strong>{periods.length}</strong></div>
      {permissions.has("payroll.period.manage") ? <div className="form-surface"><div className="form-surface-heading"><div><strong>Yeni dönem veya revizyon</strong><span>Aynı ay yeniden oluşturulursa yeni bir revizyon açılır.</span></div></div><form className="inline-form" onSubmit={createPeriod}><label className="field-label">Yıl<input name="year" type="number" min="2000" max="2200" defaultValue={new Date().getFullYear()} required/></label><label className="field-label">Ay<input name="month" type="number" min="1" max="12" defaultValue={new Date().getMonth() + 1} required/></label><button className="primary-button" disabled={busy || !companyId}>Dönem oluştur</button></form></div> : null}
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Dönem</th><th>Revizyon</th><th>Durum</th><th>Versiyon</th><th>İşlem</th></tr></thead><tbody>{periods.map(x => <tr key={x.id}><td><strong>{monthLabel(x.year, x.month)}</strong></td><td>Rev. {x.revision}</td><td><span className={`status-badge ${payrollStatusClass(x.status)}`}>{payrollStatusLabel(x.status)}</span></td><td>v{x.version}</td><td><button className="table-button" type="button" disabled={selectedPeriodId === x.id} onClick={() => void selectPeriod(x.id)}>{selectedPeriodId === x.id ? "Seçili" : "Dönemi seç"}</button></td></tr>)}{periods.length === 0 ? <tr><td className="empty-row" colSpan={5}>Bu şirket için bordro dönemi bulunmuyor.</td></tr> : null}</tbody></table></div>
      {selectedPeriod ? <div className="selected-summary"><div className="selected-summary-copy"><strong>{monthLabel(selectedPeriod.year, selectedPeriod.month)} · Rev. {selectedPeriod.revision}</strong><small>{selectedPeriod.status === "CLOSED" ? "Kapatılmış dönem değiştirilemez. Düzeltme için yeni revizyon oluşturun." : `Mevcut adım: ${payrollStatusLabel(selectedPeriod.status)}`}</small></div><div className="action-row"><span className={`status-badge ${payrollStatusClass(selectedPeriod.status)}`}>{payrollStatusLabel(selectedPeriod.status)}</span>{action ? <button className="primary-button" disabled={busy} onClick={() => void periodAction(action[0])}>{action[1]}</button> : null}</div></div> : null}
    </section>

    {selectedPeriod && permissions.has("payroll.period.view") ? <section className="panel">
      <div className="panel-heading"><div><span className="page-eyebrow">Hesaplama sonucu</span><h2>Personel bordro sonuçları</h2><p>Kesinti ve kazanç bileşenlerini seçili dönem için karşılaştırın.</p></div><strong>{results.length}</strong></div>
      <div className="notice"><Icon name="settings" size={17}/><span>Gösterilen “yasal kesintiler öncesi ücret” vergi ve SGK kesintilerinden öncedir. Yemek ve konaklama tutarları işveren maliyetidir.</span></div>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Personel</th><th>Plan / Çalışma / Ücretli izin</th><th>Onaylı FM</th><th>Baz ücret</th><th>Devamsızlık</th><th>FM kazancı</th><th>Yasal kesinti öncesi</th><th>Yemek</th><th>Konaklama</th><th>İşveren maliyeti</th></tr></thead><tbody>{results.map(x => <tr key={x.id}><td><strong>{x.employeeName}</strong><small>{x.employeeNo}</small></td><td>{x.plannedMinutes} / {x.workedMinutes} / {x.paidLeaveMinutes} dk</td><td>{x.approvedOvertimeMinutes} dk</td><td>{formatMoney(x.baseSalaryAmount, x.currencySnapshot)}</td><td><span className="amount-negative">−{formatMoney(x.absenceDeductionAmount, x.currencySnapshot)}</span></td><td><span className="amount-positive">+{formatMoney(x.overtimeEarningAmount, x.currencySnapshot)}</span></td><td><strong>{formatMoney(x.payBeforeStatutory, x.currencySnapshot)}</strong></td><td>{formatMoney(x.mealEmployerCost, x.currencySnapshot)}</td><td>{formatMoney(x.accommodationEmployerCost, x.currencySnapshot)}</td><td><strong>{formatMoney(x.employerCostBeforeStatutory, x.currencySnapshot)}</strong></td></tr>)}{results.length === 0 ? <tr><td className="empty-row" colSpan={10}>Bu dönem için henüz hesaplanmış bordro sonucu yok.</td></tr> : null}</tbody></table></div>
    </section> : null}
    </div>
  </main>;
}

function payrollStatusLabel(status: string) { return status === "DRAFT" ? "Taslak" : status === "OPEN" ? "Açık" : status === "CALCULATED" ? "Hesaplandı" : status === "UNDER_REVIEW" ? "İncelemede" : status === "APPROVED" ? "Onaylandı" : status === "CLOSED" ? "Kapatıldı" : status; }
function payrollStatusClass(status: string) { return ["APPROVED", "CLOSED"].includes(status) ? "success" : ["CALCULATED", "UNDER_REVIEW"].includes(status) ? "warning" : ""; }
function monthLabel(year: number, month: number) { return new Intl.DateTimeFormat("tr-TR", { month: "long", year: "numeric" }).format(new Date(year, month - 1, 1)); }
function formatDate(value: string) { return new Intl.DateTimeFormat("tr-TR", { day: "2-digit", month: "short", year: "numeric" }).format(new Date(`${value}T00:00:00`)); }
function formatMoney(value: number, currency: string) { return new Intl.NumberFormat("tr-TR", { style: "currency", currency, minimumFractionDigits: 2 }).format(value); }
