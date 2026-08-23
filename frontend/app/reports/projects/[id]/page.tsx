"use client";

import { FormEvent, useEffect, useState } from "react";
import { useParams } from "next/navigation";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type Cost = { currency: string; payrollCost: number; mealCost: number; accommodationCost: number; totalCost: number };
type Project360 = { projectId: string; companyId: string; projectCode: string; projectName: string; from: string; to: string; headcount: number; manDays: number; workedHours: number; approvedOvertimeHours: number; mealQuantity: number; accommodationNights: number; costs: Cost[] };
type Ledger = { id: string; costDate: string; sourceType: string; sourceLineKey: string; employeeNo: string | null; employeeName: string | null; costCenterCode: string | null; category: string; quantity: number; unit: string; amount: number; currency: string; allocationBasis: string };
type AuthResponse = { accessToken: string };

export default function Project360Page() {
  const params = useParams<{ id: string }>();
  const today = new Date().toISOString().slice(0, 10);
  const [from, setFrom] = useState(`${today.slice(0, 7)}-01`);
  const [to, setTo] = useState(today);
  const [project, setProject] = useState<Project360 | null>(null);
  const [ledger, setLedger] = useState<Ledger[]>([]);
  const [message, setMessage] = useState("Project 360 yükleniyor…");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const query = new URLSearchParams(window.location.search);
    const resolvedFrom = query.get("from") ?? `${today.slice(0, 7)}-01`;
    const resolvedTo = query.get("to") ?? today;
    setFrom(resolvedFrom); setTo(resolvedTo);
    void loadRange(resolvedFrom, resolvedTo);
  }, [params.id]);

  async function load(event?: FormEvent) { event?.preventDefault(); await loadRange(from, to); }

  async function loadRange(rangeFrom: string, rangeTo: string) {
    setBusy(true);
    try {
      const [summaryResponse, ledgerResponse] = await Promise.all([
        authFetch(`/api/v1/reports/projects/${params.id}/360?from=${rangeFrom}&to=${rangeTo}`),
        authFetch(`/api/v1/finance/cost-ledger?projectId=${params.id}&from=${rangeFrom}&to=${rangeTo}&take=1000`),
      ]);
      if (!summaryResponse?.ok) {
        setMessage(await errorMessage(summaryResponse, "Project 360 alınamadı."));
        setProject(null); setLedger([]); return;
      }
      setProject(await summaryResponse.json() as Project360);
      setLedger(ledgerResponse?.ok ? await ledgerResponse.json() as Ledger[] : []);
      setMessage("Project 360 güncel kaynak snapshot ve immutable cost ledger üzerinden oluşturuldu.");
    } finally { setBusy(false); }
  }

  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh();
    if (!token) { window.location.replace("/login"); return null; }
    let response = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status !== 401) return response;
    token = await refresh(); if (!token) { window.location.replace("/login"); return response; }
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> { try { const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!response.ok) return null; const body = await response.json() as AuthResponse; sessionStorage.setItem("pp_access_token", body.accessToken); return body.accessToken; } catch { return null; } }
  async function errorMessage(response: Response | null, fallback: string) { if (!response) return fallback; const body = await response.json().catch(() => null) as { error?: { code?: string; message?: string } } | null; return body?.error?.code ? `${body.error.code}: ${body.error.message ?? fallback}` : body?.error?.message ?? fallback; }
  const money = (value: number, currency: string) => `${value.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency}`;

  return <main className="shell">
    <a className="back" href="/reports">← Raporlama & Maliyet Merkezi</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 13 · PROJECT 360</span><h1>{project ? `${project.projectCode} · ${project.projectName}` : "Project 360"}</h1><p>{message}</p></section>
    <section className="panel audit-panel"><form className="inline-form" onSubmit={load}><label className="field-label">Başlangıç<input type="date" value={from} onChange={e => setFrom(e.target.value)} required /></label><label className="field-label">Bitiş<input type="date" value={to} onChange={e => setTo(e.target.value)} required /></label><button className="primary-button" disabled={busy}>Yenile</button></form></section>

    {project ? <>
      <section className="grid" aria-label="Project 360 KPI"><article className="card"><span>Headcount</span><h2>{project.headcount}</h2></article><article className="card"><span>Man-day</span><h2>{project.manDays.toFixed(2)}</h2></article><article className="card"><span>Çalışma Saati</span><h2>{project.workedHours.toFixed(2)}</h2></article><article className="card"><span>Onaylı FM</span><h2>{project.approvedOvertimeHours.toFixed(2)}</h2></article><article className="card"><span>Yemek</span><h2>{project.mealQuantity.toFixed(2)}</h2></article><article className="card"><span>Konaklama Gecesi</span><h2>{project.accommodationNights}</h2></article></section>
      <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">MALİYET KIRILIMI</span><h2>Para Birimi Bazında</h2></div><strong>{project.from} / {project.to}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Döviz</th><th>Bordro</th><th>Yemek</th><th>Konaklama</th><th>Toplam</th></tr></thead><tbody>{project.costs.length === 0 ? <tr><td colSpan={5}>Bu dönemde projeye atanmış maliyet yok.</td></tr> : project.costs.map(x => <tr key={x.currency}><td><strong>{x.currency}</strong></td><td>{money(x.payrollCost, x.currency)}</td><td>{money(x.mealCost, x.currency)}</td><td>{money(x.accommodationCost, x.currency)}</td><td><strong>{money(x.totalCost, x.currency)}</strong></td></tr>)}</tbody></table></div></section>
      <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">DRILL-DOWN</span><h2>Immutable Cost Ledger</h2></div><strong>{ledger.length} satır</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>Kaynak</th><th>Personel</th><th>Cost Center</th><th>Kategori</th><th>Miktar</th><th>Tutar</th><th>Dağıtım</th></tr></thead><tbody>{ledger.length === 0 ? <tr><td colSpan={8}>Bu filtrede ledger satırı yok.</td></tr> : ledger.map(x => <tr key={x.id}><td>{x.costDate}</td><td>{x.sourceType}<small>{x.sourceLineKey}</small></td><td>{x.employeeNo ?? "—"}<small>{x.employeeName ?? ""}</small></td><td>{x.costCenterCode ?? "—"}</td><td>{x.category}</td><td>{x.quantity} {x.unit}</td><td><strong>{money(x.amount, x.currency)}</strong></td><td>{x.allocationBasis}</td></tr>)}</tbody></table></div></section>
    </> : null}
  </main>;
}
