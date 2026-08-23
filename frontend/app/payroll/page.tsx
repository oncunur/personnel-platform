"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

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
      setMessage(`Bordro durumu: ${row.status}`);
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

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 8 · BORDRO</span><h1>Bordro & Ücret Merkezi</h1><p>{message}</p></section>

    <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">ŞİRKET</span><h2>Çalışma alanı</h2></div></div><label className="field-label">Şirket<select value={companyId} onChange={e => void loadCompany(e.target.value)}><option value="">Seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name} · {x.defaultCurrency}</option>)}</select></label></section>

    {permissions.has("payroll.compensation.view") ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">ÜCRET</span><h2>Personel ücret geçmişi</h2></div><strong>{compensations.length}</strong></div><label className="field-label">Personel<select value={employeeId} onChange={e => void loadCompensations(e.target.value)}><option value="">Seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label>{permissions.has("payroll.compensation.manage") ? <form className="inline-form" onSubmit={createCompensation}><label className="field-label">Başlangıç<input name="validFrom" type="date" defaultValue={firstDayOfCurrentMonth()} required/></label><label className="field-label">Bitiş [hariç]<input name="validUntilExclusive" type="date"/></label><label className="field-label">Aylık baz ücret<input name="monthlyBaseSalary" type="number" min="0.01" step="0.01" required/></label><label className="field-label">Döviz<input name="currency" defaultValue={selectedCompany?.defaultCurrency ?? "TRY"} maxLength={3} required/></label><label className="field-label">FM çarpanı<input name="overtimeMultiplier" type="number" min="1" max="5" step="0.01" defaultValue="1.5" required/></label><button className="primary-button" disabled={busy || !employeeId}>Ücret ekle</button></form> : null}<div className="table-wrap"><table className="data-table"><thead><tr><th>Dönem</th><th>Baz ücret</th><th>FM çarpanı</th></tr></thead><tbody>{compensations.length === 0 ? <tr><td colSpan={3}>Ücret tanımı yok.</td></tr> : compensations.map(x => <tr key={x.id}><td>{x.validFrom} → {x.validUntilExclusive ?? "∞"}</td><td><strong>{x.monthlyBaseSalary.toFixed(2)} {x.currency}</strong></td><td>{x.overtimeMultiplier}</td></tr>)}</tbody></table></div></section> : null}

    <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">DÖNEM</span><h2>Bordro dönemleri</h2></div><strong>{periods.length}</strong></div>{permissions.has("payroll.period.manage") ? <form className="inline-form" onSubmit={createPeriod}><label className="field-label">Yıl<input name="year" type="number" min="2000" max="2200" defaultValue={new Date().getFullYear()} required/></label><label className="field-label">Ay<input name="month" type="number" min="1" max="12" defaultValue={new Date().getMonth() + 1} required/></label><button className="primary-button" disabled={busy || !companyId}>Dönem / revision oluştur</button></form> : null}<div className="table-wrap"><table className="data-table"><thead><tr><th>Dönem</th><th>Rev.</th><th>Durum</th><th>Versiyon</th><th></th></tr></thead><tbody>{periods.length === 0 ? <tr><td colSpan={5}>Bordro dönemi yok.</td></tr> : periods.map(x => <tr key={x.id}><td>{x.year}/{String(x.month).padStart(2, "0")}</td><td>{x.revision}</td><td><strong>{x.status}</strong></td><td>{x.version}</td><td><button className="secondary-button" type="button" onClick={() => void selectPeriod(x.id)}>Seç</button></td></tr>)}</tbody></table></div>{selectedPeriod ? <div className="actions action-row"><span><strong>Seçili:</strong> {selectedPeriod.year}/{selectedPeriod.month} rev.{selectedPeriod.revision} · {selectedPeriod.status}</span>{action ? <button className="primary-button" disabled={busy} onClick={() => void periodAction(action[0])}>{action[1]}</button> : null}{selectedPeriod.status === "CLOSED" ? <span>Kapatılmış dönem immutable. Düzeltme için yeni revision oluşturun.</span> : null}</div> : null}</section>

    {selectedPeriod && permissions.has("payroll.period.view") ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">SONUÇ</span><h2>Personel bordro sonuçları</h2></div><strong>{results.length}</strong></div><p>Bu aşamadaki <strong>Pay Before Statutory</strong> ülke bazlı vergi/SGK kesintilerinden öncedir. Yemek ve konaklama tutarları işveren maliyetidir.</p><div className="table-wrap"><table className="data-table"><thead><tr><th>Personel</th><th>Plan / Çalışma / Ücretli izin</th><th>Onaylı FM</th><th>Baz ücret</th><th>Devamsızlık</th><th>FM kazanç</th><th>Statutory öncesi</th><th>Yemek</th><th>Konaklama</th><th>İşveren maliyeti</th></tr></thead><tbody>{results.length === 0 ? <tr><td colSpan={10}>Henüz hesaplanmış sonuç yok.</td></tr> : results.map(x => <tr key={x.id}><td>{x.employeeNo} · {x.employeeName}</td><td>{x.plannedMinutes} / {x.workedMinutes} / {x.paidLeaveMinutes} dk</td><td>{x.approvedOvertimeMinutes} dk</td><td>{x.baseSalaryAmount.toFixed(2)} {x.currencySnapshot}</td><td>-{x.absenceDeductionAmount.toFixed(2)}</td><td>+{x.overtimeEarningAmount.toFixed(2)}</td><td><strong>{x.payBeforeStatutory.toFixed(2)} {x.currencySnapshot}</strong></td><td>{x.mealEmployerCost.toFixed(2)}</td><td>{x.accommodationEmployerCost.toFixed(2)}</td><td><strong>{x.employerCostBeforeStatutory.toFixed(2)} {x.currencySnapshot}</strong></td></tr>)}</tbody></table></div></section> : null}
  </main>;
}
