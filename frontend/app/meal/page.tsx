"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { Icon } from "../components/Icon";
import { PageHeader } from "../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type Permission = { code: string };
type Me = { permissions: Permission[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type Camp = { id: string; companyId: string; code: string; name: string; isActive: boolean };
type Employee = { id: string; companyId: string; employeeNo: string; firstName: string; lastName: string; status: string };
type EmployeePage = { items: Employee[]; totalCount: number };
type MealType = { id: string; code: string; name: string; displayOrder: number };
type Rate = { id: string; campId: string; mealTypeId: string; mealTypeCode: string; mealTypeName: string; validFrom: string; validUntilExclusive: string | null; unitPrice: number; currency: string; version: number };
type Consumption = { id: string; employeeNo: string; employeeName: string; campCode: string; campName: string; mealTypeCode: string; mealTypeName: string; consumptionDate: string; quantity: number; unitPriceSnapshot: number; currencySnapshot: string; totalCostSnapshot: number; source: string; externalEventId: string | null; note: string | null };
type ConsumptionPage = { items: Consumption[]; page: number; pageSize: number; totalCount: number };

function todayLocal() {
  const now = new Date();
  const offset = now.getTimezoneOffset() * 60000;
  return new Date(now.getTime() - offset).toISOString().slice(0, 10);
}

function formatDate(value: string | null) {
  return value ? new Date(`${value}T00:00:00`).toLocaleDateString("tr-TR") : "Süresiz";
}

function sourceLabel(source: string) {
  return source === "MANUAL" ? "Manuel" : source === "IMPORT" ? "Dosyadan" : source === "INTEGRATION" ? "Entegrasyon" : source;
}

export default function MealPage() {
  const [me, setMe] = useState<Me | null>(null);
  const [camps, setCamps] = useState<Camp[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [types, setTypes] = useState<MealType[]>([]);
  const [rates, setRates] = useState<Rate[]>([]);
  const [consumptions, setConsumptions] = useState<Consumption[]>([]);
  const [campId, setCampId] = useState("");
  const [mealTypeId, setMealTypeId] = useState("");
  const [message, setMessage] = useState("Yemek verileri yükleniyor…");
  const [busy, setBusy] = useState(false);
  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const selectedCamp = useMemo(() => camps.find(x => x.id === campId) ?? null, [camps, campId]);
  const totalConsumptionCost = useMemo(() => consumptions.reduce((sum, row) => sum + row.totalCostSnapshot, 0), [consumptions]);

  useEffect(() => { void initialize(); }, []);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const codes = new Set(current.permissions.map(x => x.code));
    const [typeRows, campRows, employeePage, consumptionPage] = await Promise.all([
      codes.has("meal.type.view") ? json<MealType[]>("/api/v1/meal/types") : Promise.resolve(null),
      codes.has("camp.site.view") ? json<Camp[]>("/api/v1/camp/sites") : Promise.resolve(null),
      codes.has("personnel.view") ? json<EmployeePage>("/api/v1/personnel/employees?status=ACTIVE&pageSize=100") : Promise.resolve(null),
      codes.has("meal.consumption.view") ? json<ConsumptionPage>("/api/v1/meal/consumptions?page=1&pageSize=100") : Promise.resolve(null),
    ]);
    setTypes(typeRows ?? []);
    setCamps(campRows ?? []);
    setEmployees(employeePage?.items ?? []);
    setConsumptions(consumptionPage?.items ?? []);
    setMessage("Yemek tüketimi ve maliyet kayıtları hazır.");
  }

  async function selectCamp(id: string) {
    setCampId(id); setRates([]);
    if (!id || !permissions.has("meal.rate.view")) return;
    setRates(await json<Rate[]>(`/api/v1/meal/rates?campId=${id}`) ?? []);
  }

  async function reloadConsumptions() {
    if (!permissions.has("meal.consumption.view")) return;
    const page = await json<ConsumptionPage>("/api/v1/meal/consumptions?page=1&pageSize=100");
    setConsumptions(page?.items ?? []);
  }

  async function createRate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!campId || !mealTypeId) return; setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const response = await authFetch("/api/v1/meal/rates", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ campId, mealTypeId, validFrom: form.get("validFrom"), validUntilExclusive: form.get("validUntilExclusive") || null, unitPrice: Number(form.get("unitPrice")), currency: form.get("currency") }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Yemek fiyatı kaydedilemedi.")); return; }
      event.currentTarget.reset(); setRates(await json<Rate[]>(`/api/v1/meal/rates?campId=${campId}`) ?? []); setMessage("Tarih-etkin yemek fiyatı kaydedildi.");
    } finally { setBusy(false); }
  }

  async function recordConsumption(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const source = String(form.get("source") ?? "MANUAL");
      const response = await authFetch("/api/v1/meal/consumptions", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ employeeId: form.get("employeeId"), campId: form.get("campId"), mealTypeId: form.get("mealTypeId"), consumptionDate: form.get("consumptionDate"), quantity: Number(form.get("quantity")), source, externalEventId: form.get("externalEventId") || null, note: form.get("note") || null }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Yemek tüketimi kaydedilemedi.")); return; }
      event.currentTarget.reset(); setMessage("Yemek tüketimi kaydedildi; fiyat ve maliyet snapshot olarak sabitlendi."); await reloadConsumptions();
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

  return <main className="page-shell">
    <PageHeader eyebrow="Yemek operasyonu" title="Yemek tüketimi ve maliyetler" description="Kamp bazlı öğün fiyatlarını yönetin, tüketimleri kaydedin ve gerçekleşen maliyeti izleyin." status={message}/>

    <section className="stat-grid" aria-label="Yemek özeti">
      <article className="stat-card"><span className="stat-icon"><Icon name="building"/></span><span className="stat-copy"><strong>{camps.length}</strong><span>Hizmet verilen kamp</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="box"/></span><span className="stat-copy"><strong>{types.length}</strong><span>Tanımlı öğün</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{consumptions.length}</strong><span>Listelenen tüketim</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="wallet"/></span><span className="stat-copy"><strong>{totalConsumptionCost.toLocaleString("tr-TR", { maximumFractionDigits: 0 })}</strong><span>Toplam kayıtlı maliyet</span></span></article>
    </section>

    <section className="panel workspace-panel">
      <div className="workspace-copy"><span className="eyebrow dark">Fiyat çalışma alanı</span><h2>Kamp ve öğün seçimi</h2><p>Fiyat dönemleri bu seçimdeki kamp için listelenir; yeni fiyat seçili öğüne uygulanır.</p></div>
      <div className="workspace-select inline-form"><label className="field-label">Kamp<select value={campId} onChange={e => void selectCamp(e.target.value)}><option value="">Kamp seçin</option>{camps.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Öğün<select value={mealTypeId} onChange={e => setMealTypeId(e.target.value)}><option value="">Öğün seçin</option>{types.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label></div>
    </section>

    <div className="content-stack">
      {permissions.has("meal.rate.view") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Fiyat yönetimi</span><h2>{selectedCamp?.name ?? "Seçili kamp"} fiyat dönemleri</h2><p>Tüketim kaydında ilgili tarihteki birim fiyat otomatik olarak sabitlenir.</p></div><strong>{rates.length}</strong></div>
        {permissions.has("meal.rate.manage") ? <div className="form-surface"><div className="form-surface-heading"><div><strong>Yeni öğün fiyatı</strong><span>Bitiş boş bırakılırsa fiyat yeni bir dönem eklenene kadar geçerlidir.</span></div></div><form className="inline-form" onSubmit={createRate}><label className="field-label">Başlangıç<input name="validFrom" type="date" defaultValue={todayLocal()} required/></label><label className="field-label">Bitiş tarihi (hariç)<input name="validUntilExclusive" type="date"/></label><label className="field-label">Birim fiyat<input name="unitPrice" type="number" min="0.01" step="0.01" required/></label><label className="field-label">Para birimi<input name="currency" defaultValue="TRY" maxLength={3} required/></label><button className="primary-button" disabled={busy || !campId || !mealTypeId}>Fiyatı kaydet</button></form></div> : null}
        <div className="table-wrap"><table className="data-table"><thead><tr><th>Öğün</th><th>Başlangıç</th><th>Bitiş</th><th>Birim fiyat</th></tr></thead><tbody>{rates.length === 0 ? <tr><td className="empty-row" colSpan={4}>{campId ? "Bu kamp için fiyat kaydı yok." : "Fiyatları görmek için kamp seçin."}</td></tr> : rates.map(x => <tr key={x.id}><td><strong>{x.mealTypeName}</strong><small>{x.mealTypeCode}</small></td><td>{formatDate(x.validFrom)}</td><td>{formatDate(x.validUntilExclusive)}</td><td><strong>{x.unitPrice.toLocaleString("tr-TR", { minimumFractionDigits: 2 })} {x.currency}</strong></td></tr>)}</tbody></table></div>
      </section> : null}

      {permissions.has("meal.consumption.record") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Yeni tüketim</span><h2>Personel yemek kaydı</h2><p>Kamp ve öğün tarihine uygun fiyat bulunur, maliyet kayda sabitlenir.</p></div></div>
        <div className="form-surface"><form className="inline-form" onSubmit={recordConsumption}><label className="field-label">Personel<select name="employeeId" required><option value="">Personel seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label><label className="field-label">Kamp<select name="campId" required><option value="">Kamp seçin</option>{camps.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Öğün<select name="mealTypeId" required><option value="">Öğün seçin</option>{types.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label><label className="field-label">Tarih<input name="consumptionDate" type="date" defaultValue={todayLocal()} required/></label><label className="field-label">Adet<input name="quantity" type="number" min="0.01" max="10" step="0.01" defaultValue="1" required/></label><label className="field-label">Kayıt kaynağı<select name="source" defaultValue="MANUAL"><option value="MANUAL">Manuel giriş</option><option value="IMPORT">Dosyadan aktarım</option><option value="INTEGRATION">Entegrasyon</option></select></label><label className="field-label">Dış sistem kayıt no<input name="externalEventId" maxLength={200}/></label><label className="field-label">Not<input name="note" maxLength={1000}/></label><button className="primary-button" disabled={busy}>Tüketimi kaydet</button></form></div>
        {!permissions.has("personnel.view") || !permissions.has("camp.site.view") ? <p className="notice">Personel veya kamp listesine erişim yetkiniz olmadığı için bazı seçimler kullanılamıyor.</p> : null}
      </section> : null}

      {permissions.has("meal.consumption.view") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Tüketim geçmişi</span><h2>Son yemek kayıtları</h2><p>Maliyetler, kayıt anındaki fiyat üzerinden gösterilir.</p></div><strong>{consumptions.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>Personel</th><th>Kamp</th><th>Öğün</th><th>Adet</th><th>Birim fiyat</th><th>Toplam</th><th>Kaynak</th></tr></thead><tbody>{consumptions.length === 0 ? <tr><td className="empty-row" colSpan={8}>Henüz tüketim kaydı yok.</td></tr> : consumptions.map(x => <tr key={x.id}><td>{formatDate(x.consumptionDate)}</td><td><strong>{x.employeeName}</strong><small>{x.employeeNo}</small></td><td>{x.campCode} · {x.campName}</td><td>{x.mealTypeName}</td><td>{x.quantity.toLocaleString("tr-TR")}</td><td>{x.unitPriceSnapshot.toLocaleString("tr-TR", { minimumFractionDigits: 2 })} {x.currencySnapshot}</td><td><strong>{x.totalCostSnapshot.toLocaleString("tr-TR", { minimumFractionDigits: 2 })} {x.currencySnapshot}</strong></td><td><span className="status-badge">{sourceLabel(x.source)}</span></td></tr>)}</tbody></table></div></section> : null}
    </div>
  </main>;
}
