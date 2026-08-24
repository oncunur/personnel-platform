"use client";

import { FormEvent, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { Icon } from "../../../components/Icon";
import { PageHeader } from "../../../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type Cost = { currency: string; payrollCost: number; mealCost: number; accommodationCost: number; totalCost: number };
type Project360 = { projectId: string; companyId: string; projectCode: string; projectName: string; from: string; to: string; headcount: number; manDays: number; workedHours: number; approvedOvertimeHours: number; mealQuantity: number; accommodationNights: number; costs: Cost[] };
type Ledger = { id: string; costDate: string; sourceType: string; sourceLineKey: string; employeeNo: string | null; employeeName: string | null; costCenterCode: string | null; category: string; quantity: number; unit: string; amount: number; currency: string; allocationBasis: string };
type AuthResponse = { accessToken: string };

const sourceLabels: Record<string, string> = { PAYROLL: "Bordro", MEAL: "Yemek", ACCOMMODATION: "Konaklama", MANUAL: "Manuel", ASSET: "Demirbaş", VEHICLE: "Araç" };
const categoryLabels: Record<string, string> = { PAYROLL: "Bordro", MEAL: "Yemek", ACCOMMODATION: "Konaklama", ASSET: "Demirbaş", VEHICLE: "Araç" };
const allocationLabels: Record<string, string> = { PROJECT_ASSIGNMENT: "Proje ataması", COST_CENTER: "Maliyet merkezi", DIRECT: "Doğrudan", EQUAL: "Eşit dağıtım" };

export default function Project360Page() {
  const params = useParams<{ id: string }>();
  const today = new Date().toISOString().slice(0, 10);
  const [from, setFrom] = useState(`${today.slice(0, 7)}-01`);
  const [to, setTo] = useState(today);
  const [project, setProject] = useState<Project360 | null>(null);
  const [ledger, setLedger] = useState<Ledger[]>([]);
  const [message, setMessage] = useState("Proje 360 görünümü yükleniyor…");
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
        setMessage(await errorMessage(summaryResponse, "Proje 360 özeti alınamadı."));
        setProject(null); setLedger([]); return;
      }
      setProject(await summaryResponse.json() as Project360);
      setLedger(ledgerResponse?.ok ? await ledgerResponse.json() as Ledger[] : []);
      setMessage("Proje özeti güncel operasyon kayıtları ve değiştirilemeyen maliyet defteri üzerinden oluşturuldu.");
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

  return <main className="page-shell">
    <PageHeader eyebrow="Proje raporları" title={project ? `${project.projectCode} · ${project.projectName}` : "Proje 360"} description="Personel kapasitesi, çalışma süresi, operasyon miktarları ve maliyetleri tek proje görünümünde inceleyin." status={message} actions={<a className="secondary-button" href="/reports">Raporlama merkezine dön</a>}/>
    <section className="panel workspace-panel"><div className="workspace-copy"><span className="eyebrow dark">Rapor dönemi</span><h2>Tarih aralığını seçin</h2><p>Tüm proje göstergeleri ve maliyet satırları seçilen döneme göre yenilenir.</p></div><form className="inline-form workspace-select" onSubmit={load}><label className="field-label">Başlangıç<input type="date" value={from} onChange={e => setFrom(e.target.value)} required /></label><label className="field-label">Bitiş<input type="date" value={to} onChange={e => setTo(e.target.value)} required /></label><button className="primary-button" disabled={busy}><Icon name="chart" size={17}/>Raporu yenile</button></form></section>

    {project ? <>
      <section className="stat-grid" aria-label="Proje göstergeleri"><article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{project.headcount}</strong><span>Personel sayısı</span></span></article><article className="stat-card"><span className="stat-icon"><Icon name="calendar"/></span><span className="stat-copy"><strong>{project.manDays.toLocaleString("tr-TR", { maximumFractionDigits: 2 })}</strong><span>Adam/gün</span></span></article><article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{project.workedHours.toLocaleString("tr-TR", { maximumFractionDigits: 2 })}</strong><span>Çalışma saati</span></span></article><article className="stat-card"><span className="stat-icon"><Icon name="chart"/></span><span className="stat-copy"><strong>{project.approvedOvertimeHours.toLocaleString("tr-TR", { maximumFractionDigits: 2 })}</strong><span>Onaylı fazla mesai</span></span></article></section>
      <div className="content-stack">
        <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Operasyon miktarları</span><h2>Yemek ve konaklama kullanımı</h2><p>Seçilen dönemde proje adına oluşan operasyon tüketimleri.</p></div></div><div className="detail-grid"><div className="detail-item"><span>Yemek miktarı</span><strong>{project.mealQuantity.toLocaleString("tr-TR", { maximumFractionDigits: 2 })}</strong></div><div className="detail-item"><span>Konaklama gecesi</span><strong>{project.accommodationNights.toLocaleString("tr-TR")}</strong></div><div className="detail-item"><span>Rapor dönemi</span><strong>{new Date(project.from).toLocaleDateString("tr-TR")} – {new Date(project.to).toLocaleDateString("tr-TR")}</strong></div></div></section>
        <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Maliyet kırılımı</span><h2>Para birimine göre maliyetler</h2><p>Bordro, yemek ve konaklama maliyetlerini ayrı ayrı karşılaştırın.</p></div><strong>{project.costs.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Para birimi</th><th>Bordro</th><th>Yemek</th><th>Konaklama</th><th>Toplam</th></tr></thead><tbody>{project.costs.length === 0 ? <tr><td className="empty-row" colSpan={5}>Bu dönemde projeye atanmış maliyet bulunmuyor.</td></tr> : project.costs.map(x => <tr key={x.currency}><td><strong>{x.currency}</strong></td><td>{money(x.payrollCost, x.currency)}</td><td>{money(x.mealCost, x.currency)}</td><td>{money(x.accommodationCost, x.currency)}</td><td><strong>{money(x.totalCost, x.currency)}</strong></td></tr>)}</tbody></table></div></section>
        <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Maliyet ayrıntısı</span><h2>Değiştirilemeyen maliyet defteri</h2><p>Toplamları oluşturan kaynak kayıtları ve dağıtım yöntemlerini inceleyin.</p></div><strong>{ledger.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>Kaynak</th><th>Personel</th><th>Maliyet merkezi</th><th>Kategori</th><th>Miktar</th><th>Tutar</th><th>Dağıtım</th></tr></thead><tbody>{ledger.length === 0 ? <tr><td className="empty-row" colSpan={8}>Bu filtrede maliyet satırı bulunmuyor.</td></tr> : ledger.map(x => <tr key={x.id}><td>{new Date(x.costDate).toLocaleDateString("tr-TR")}</td><td>{sourceLabels[x.sourceType] ?? x.sourceType}<small>{x.sourceLineKey}</small></td><td>{x.employeeNo ?? "—"}<small>{x.employeeName ?? ""}</small></td><td>{x.costCenterCode ?? "—"}</td><td>{categoryLabels[x.category] ?? x.category}</td><td>{x.quantity.toLocaleString("tr-TR")} {x.unit}</td><td><strong>{money(x.amount, x.currency)}</strong></td><td>{allocationLabels[x.allocationBasis] ?? x.allocationBasis}</td></tr>)}</tbody></table></div></section>
      </div>
    </> : null}
  </main>;
}
