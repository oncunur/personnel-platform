"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useActionDialog } from "../components/ActionDialog";
import { Icon } from "../components/Icon";
import { OperationDisclosure } from "../components/OperationDisclosure";
import { PageHeader } from "../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type Permission = { code: string };
type Me = { permissions: Permission[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type Company = { id: string; code: string; name: string };
type Camp = { id: string; companyId: string; code: string; name: string; address: string | null; isActive: boolean; version: number };
type Room = { id: string; campId: string; code: string; name: string; floor: number | null; isActive: boolean; version: number };
type Bed = { id: string; roomId: string; code: string; isActive: boolean; version: number };
type Rate = { id: string; campId: string; validFrom: string; validUntilExclusive: string | null; nightlyRate: number; currency: string; version: number };
type Employee = { id: string; companyId: string; employeeNo: string; firstName: string; lastName: string; status: string };
type EmployeePage = { items: Employee[]; totalCount: number };
type Stay = { id: string; companyId: string; employeeId: string; employeeNo: string; employeeName: string; campId: string; campCode: string; campName: string; roomId: string; roomCode: string; bedId: string; bedCode: string; rateId: string; projectIdSnapshot: string | null; costCenterIdSnapshot: string | null; checkInDate: string; checkOutDateExclusive: string | null; nights: number; nightlyRateSnapshot: number; currencySnapshot: string; currentOrFinalCost: number; status: string; note: string | null; version: number };
type StayPage = { items: Stay[]; page: number; pageSize: number; totalCount: number };

function localDate(offsetDays = 0) {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  const offset = d.getTimezoneOffset() * 60000;
  return new Date(d.getTime() - offset).toISOString().slice(0, 10);
}

function nextDate(value: string) {
  const date = new Date(`${value}T00:00:00`);
  date.setDate(date.getDate() + 1);
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
}

function formatDate(value: string | null) {
  return value ? new Date(`${value}T00:00:00`).toLocaleDateString("tr-TR") : "Açık";
}

function stayStatus(status: string) {
  return status === "ACTIVE" ? "Devam ediyor" : status === "CLOSED" ? "Tamamlandı" : status === "CANCELLED" ? "İptal edildi" : status;
}

export default function CampPage() {
  const [me, setMe] = useState<Me | null>(null);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [camps, setCamps] = useState<Camp[]>([]);
  const [rooms, setRooms] = useState<Room[]>([]);
  const [beds, setBeds] = useState<Bed[]>([]);
  const [rates, setRates] = useState<Rate[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [stays, setStays] = useState<Stay[]>([]);
  const [campId, setCampId] = useState("");
  const [roomId, setRoomId] = useState("");
  const [bedId, setBedId] = useState("");
  const [employeeId, setEmployeeId] = useState("");
  const [message, setMessage] = useState("Kamp verileri yükleniyor…");
  const [busy, setBusy] = useState(false);
  const { ask, dialog } = useActionDialog();
  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const selectedCamp = useMemo(() => camps.find(x => x.id === campId) ?? null, [camps, campId]);
  const activeStays = useMemo(() => stays.filter(x => x.status === "ACTIVE"), [stays]);

  useEffect(() => { void initialize(); }, []);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const [campRows, stayPage, employeePage, companyRows] = await Promise.all([
      current.permissions.some(x => x.code === "camp.site.view") ? json<Camp[]>("/api/v1/camp/sites") : Promise.resolve(null),
      current.permissions.some(x => x.code === "camp.stay.view") ? json<StayPage>("/api/v1/camp/stays?page=1&pageSize=100") : Promise.resolve(null),
      current.permissions.some(x => x.code === "personnel.view") ? json<EmployeePage>("/api/v1/personnel/employees?status=ACTIVE&pageSize=100") : Promise.resolve(null),
      current.permissions.some(x => x.code === "organization.company.view") ? json<Company[]>("/api/v1/organization/companies") : Promise.resolve(null),
    ]);
    setCamps(campRows ?? []);
    setStays(stayPage?.items ?? []);
    setEmployees(employeePage?.items ?? []);
    setCompanies(companyRows ?? []);
    setMessage("Kamp, yatak ve konaklama kayıtları hazır.");
  }

  async function selectCamp(id: string) {
    setCampId(id); setRoomId(""); setBedId(""); setRooms([]); setBeds([]); setRates([]);
    if (!id) return;
    const [roomRows, rateRows] = await Promise.all([
      permissions.has("camp.site.view") ? json<Room[]>(`/api/v1/camp/sites/${id}/rooms`) : Promise.resolve(null),
      permissions.has("camp.rate.view") ? json<Rate[]>(`/api/v1/camp/sites/${id}/rates`) : Promise.resolve(null),
    ]);
    setRooms(roomRows ?? []); setRates(rateRows ?? []);
  }

  async function selectRoom(id: string) {
    setRoomId(id); setBedId(""); setBeds([]);
    if (!id || !permissions.has("camp.site.view")) return;
    setBeds(await json<Bed[]>(`/api/v1/camp/rooms/${id}/beds`) ?? []);
  }

  async function reloadStays() {
    if (!permissions.has("camp.stay.view")) return;
    const page = await json<StayPage>("/api/v1/camp/stays?page=1&pageSize=100");
    setStays(page?.items ?? []);
  }

  async function createCamp(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true);
    try {
      const formElement = event.currentTarget;
      const form = new FormData(formElement);
      const response = await authFetch("/api/v1/camp/sites", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ companyId: form.get("companyId"), code: form.get("code"), name: form.get("name"), address: form.get("address") || null }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Kamp oluşturulamadı.")); return; }
      formElement.reset();
      setCamps(await json<Camp[]>("/api/v1/camp/sites") ?? []); setMessage("Kamp oluşturuldu.");
    } finally { setBusy(false); }
  }

  async function createRoom(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!campId) return; setBusy(true);
    try {
      const formElement = event.currentTarget;
      const form = new FormData(formElement);
      const floorRaw = String(form.get("floor") ?? "");
      const response = await authFetch(`/api/v1/camp/sites/${campId}/rooms`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ code: form.get("code"), name: form.get("name"), floor: floorRaw ? Number(floorRaw) : null }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Oda oluşturulamadı.")); return; }
      formElement.reset(); setRooms(await json<Room[]>(`/api/v1/camp/sites/${campId}/rooms`) ?? []); setMessage("Oda oluşturuldu.");
    } finally { setBusy(false); }
  }

  async function createBed(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!roomId) return; setBusy(true);
    try {
      const formElement = event.currentTarget;
      const form = new FormData(formElement);
      const response = await authFetch(`/api/v1/camp/rooms/${roomId}/beds`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ code: form.get("code") }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Yatak oluşturulamadı.")); return; }
      formElement.reset(); setBeds(await json<Bed[]>(`/api/v1/camp/rooms/${roomId}/beds`) ?? []); setMessage("Yatak oluşturuldu.");
    } finally { setBusy(false); }
  }

  async function createRate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!campId) return; setBusy(true);
    try {
      const formElement = event.currentTarget;
      const form = new FormData(formElement);
      const response = await authFetch(`/api/v1/camp/sites/${campId}/rates`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ validFrom: form.get("validFrom"), validUntilExclusive: form.get("validUntilExclusive") || null, nightlyRate: Number(form.get("nightlyRate")), currency: form.get("currency") }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Fiyat kaydedilemedi.")); return; }
      formElement.reset(); setRates(await json<Rate[]>(`/api/v1/camp/sites/${campId}/rates`) ?? []); setMessage("Tarih-etkin konaklama fiyatı kaydedildi.");
    } finally { setBusy(false); }
  }

  async function createStay(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!campId || !roomId || !bedId || !employeeId) return; setBusy(true);
    try {
      const formElement = event.currentTarget;
      const form = new FormData(formElement);
      const response = await authFetch("/api/v1/camp/stays", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ employeeId, campId, roomId, bedId, checkInDate: form.get("checkInDate"), checkOutDateExclusive: form.get("checkOutDateExclusive") || null, note: form.get("note") || null }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Konaklama oluşturulamadı.")); return; }
      formElement.reset(); setMessage("Personel yatağa atandı. Tarih çakışmaları DB seviyesinde korunuyor."); await reloadStays();
    } finally { setBusy(false); }
  }

  async function closeStay(row: Stay) {
    const earliestCheckOut = nextDate(row.checkInDate);
    const suggestedCheckOut = earliestCheckOut > localDate(1) ? earliestCheckOut : localDate(1);
    const result = await ask({
      title: "Konaklamayı sonlandırın",
      description: `${row.employeeName} için ${row.campName} konaklamasının çıkış tarihini belirleyin. Seçilen tarih konaklamaya dahil değildir.`,
      confirmLabel: "Çıkışı kaydet",
      tone: "success",
      fields: [{ name: "checkOut", label: "Çıkış tarihi", type: "date", initialValue: suggestedCheckOut, min: earliestCheckOut, required: true }],
    });
    if (!result) return;
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/camp/stays/${row.id}/close`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ checkOutDateExclusive: result.checkOut, version: row.version }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Konaklama kapatılamadı.")); return; }
      setMessage("Konaklama kapatıldı ve toplam maliyet sabitlendi."); await reloadStays();
    } finally { setBusy(false); }
  }

  async function cancelStay(row: Stay) {
    const confirmed = await ask({
      title: "Konaklama iptal edilsin mi?",
      description: `${row.employeeName} için ${row.campName} konaklama kaydı iptal durumuna alınacak.`,
      confirmLabel: "Konaklamayı iptal et",
      tone: "danger",
    });
    if (!confirmed) return;
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/camp/stays/${row.id}/cancel`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: row.version }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Konaklama iptal edilemedi.")); return; }
      setMessage("Konaklama iptal edildi."); await reloadStays();
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
  async function errorMessage(response: Response | null, fallback: string) { if (!response) return fallback; const body = await response.json().catch(() => null) as { error?: { message?: string } } | null; return body?.error?.message ?? fallback; }

  return <main className="page-shell">
    <PageHeader eyebrow="Kamp ve konaklama" title="Konaklama operasyonu" description="Kamp kapasitesini, yerleşimleri ve gecelik maliyetleri tek çalışma alanından yönetin." status={message}/>

    <section className="stat-grid" aria-label="Kamp özeti">
      <article className="stat-card"><span className="stat-icon"><Icon name="building"/></span><span className="stat-copy"><strong>{camps.length}</strong><span>Aktif kamp</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="box"/></span><span className="stat-copy"><strong>{rooms.length}</strong><span>Seçili kamptaki oda</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{activeStays.length}</strong><span>Devam eden konaklama</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="wallet"/></span><span className="stat-copy"><strong>{rates.length}</strong><span>Geçerli fiyat dönemi</span></span></article>
    </section>

    <section className="panel workspace-panel">
      <div className="workspace-copy"><span className="eyebrow dark">Çalışma kapsamı</span><h2>Kamp → oda → yatak seçimi</h2><p>Oda, yatak, fiyat ve yerleşim işlemleri bu seçime göre güncellenir.</p></div>
      <div className="workspace-select inline-form">
        <label className="field-label">Kamp<select value={campId} onChange={e => void selectCamp(e.target.value)}><option value="">Kamp seçin</option>{camps.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
        <label className="field-label">Oda<select value={roomId} onChange={e => void selectRoom(e.target.value)} disabled={!campId}><option value="">Oda seçin</option>{rooms.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
        <label className="field-label">Yatak<select value={bedId} onChange={e => setBedId(e.target.value)} disabled={!roomId}><option value="">Yatak seçin</option>{beds.map(x => <option key={x.id} value={x.id}>{x.code}</option>)}</select></label>
      </div>
    </section>

    <div className="content-stack">
      {permissions.has("camp.site.manage") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Kapasite tanımları</span><h2>Kamp yapısını genişletin</h2><p>Yeni kamp, seçili kampa oda veya seçili odaya yatak ekleyin.</p></div></div>
        <div className="organization-grid">
          <OperationDisclosure title="Yeni kamp ekle" description="Şirket kapsamına yeni konaklama alanı ekler."><form onSubmit={createCamp} className="stack">{companies.length ? <label className="field-label">Şirket<select name="companyId" required><option value="">Şirket seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label> : <label className="field-label">Şirket kimliği<input name="companyId" required/></label>}<label className="field-label">Kamp kodu<input name="code" required/></label><label className="field-label">Kamp adı<input name="name" required/></label><label className="field-label">Adres<input name="address"/></label><button className="primary-button" disabled={busy}>Kampı kaydet</button></form></OperationDisclosure>
          <OperationDisclosure title="Yeni oda ekle" description={selectedCamp ? `${selectedCamp.name} kampına eklenecek.` : "Önce bir kamp seçin."}><form onSubmit={createRoom} className="stack"><label className="field-label">Oda kodu<input name="code" required/></label><label className="field-label">Oda adı<input name="name" required/></label><label className="field-label">Kat<input name="floor" type="number"/></label><button className="primary-button" disabled={busy || !campId}>Odayı kaydet</button></form></OperationDisclosure>
          <OperationDisclosure title="Yeni yatak ekle" description={roomId ? "Seçili odaya yeni yatak ekler." : "Önce kamp ve oda seçin."}><form onSubmit={createBed} className="stack"><label className="field-label">Yatak kodu<input name="code" required/></label><button className="primary-button" disabled={busy || !roomId}>Yatağı kaydet</button></form></OperationDisclosure>
        </div>
      </section> : null}

      {permissions.has("camp.rate.view") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Maliyet</span><h2>{selectedCamp?.name ?? "Seçili kamp"} fiyat dönemleri</h2><p>Konaklama açılırken ilgili tarihteki gecelik tutar sabitlenir.</p></div><strong>{rates.length}</strong></div>
        <div className="table-wrap responsive-table-wrap" role="region" aria-label="Kamp fiyat dönemleri" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Başlangıç</th><th>Bitiş</th><th>Gecelik tutar</th></tr></thead><tbody>{rates.length === 0 ? <tr><td className="empty-row" colSpan={3}>{campId ? "Bu kamp için fiyat kaydı yok." : "Fiyatları görmek için kamp seçin."}</td></tr> : rates.map(x => <tr key={x.id}><td data-label="Başlangıç">{formatDate(x.validFrom)}</td><td data-label="Bitiş">{x.validUntilExclusive ? formatDate(x.validUntilExclusive) : "Süresiz"}</td><td data-label="Gecelik tutar"><strong>{x.nightlyRate.toLocaleString("tr-TR", { minimumFractionDigits: 2 })} {x.currency}</strong></td></tr>)}</tbody></table></div>
        {permissions.has("camp.rate.manage") ? <OperationDisclosure title="Yeni fiyat dönemi ekle" description="Bitiş tarihi boş bırakılırsa dönem süresiz devam eder."><form className="inline-form" onSubmit={createRate}><label className="field-label">Başlangıç<input name="validFrom" type="date" defaultValue={localDate()} required/></label><label className="field-label">Bitiş tarihi (hariç)<input name="validUntilExclusive" type="date"/></label><label className="field-label">Gecelik tutar<input name="nightlyRate" type="number" min="0.01" step="0.01" required/></label><label className="field-label">Para birimi<input name="currency" defaultValue="TRY" maxLength={3} required/></label><button className="primary-button" disabled={busy || !campId}>Fiyatı kaydet</button></form></OperationDisclosure> : null}
      </section> : null}

      {permissions.has("camp.stay.manage") ? <section className="panel">
        <OperationDisclosure title="Yeni konaklama başlat" description="Personeli seçili yatağa yerleştirin; tarih çakışmaları otomatik kontrol edilir."><form className="inline-form" onSubmit={createStay}><label className="field-label">Personel<select value={employeeId} onChange={e => setEmployeeId(e.target.value)} required><option value="">Personel seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label><label className="field-label">Giriş tarihi<input name="checkInDate" type="date" defaultValue={localDate()} required/></label><label className="field-label">Planlı çıkış (hariç)<input name="checkOutDateExclusive" type="date"/></label><label className="field-label">Not<input name="note" maxLength={2000}/></label><button className="primary-button" disabled={busy || !campId || !roomId || !bedId || !employeeId}>Konaklamayı başlat</button></form></OperationDisclosure>
        {!permissions.has("personnel.view") ? <p className="notice">Personel listesine erişim yetkiniz olmadığı için seçim yapılamıyor.</p> : null}
      </section> : null}

      {permissions.has("camp.stay.view") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Konaklama kayıtları</span><h2>Güncel ve geçmiş yerleşimler</h2><p>Aktif konaklamaları kapatabilir veya hatalı kaydı iptal edebilirsiniz.</p></div><strong>{stays.length}</strong></div>
        <div className="table-wrap responsive-table-wrap" role="region" aria-label="Konaklama kayıtları" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Personel</th><th>Kamp</th><th>Oda / yatak</th><th>Giriş</th><th>Çıkış</th><th>Gece</th><th>Maliyet</th><th>Durum</th><th>İşlem</th></tr></thead><tbody>{stays.length === 0 ? <tr><td className="empty-row" colSpan={9}>Henüz konaklama kaydı yok.</td></tr> : stays.map(x => <tr key={x.id}><td data-label="Personel"><strong>{x.employeeName}</strong><small>{x.employeeNo}</small></td><td data-label="Kamp">{x.campCode} · {x.campName}</td><td data-label="Oda / yatak">{x.roomCode} / {x.bedCode}</td><td data-label="Giriş">{formatDate(x.checkInDate)}</td><td data-label="Çıkış">{formatDate(x.checkOutDateExclusive)}</td><td data-label="Gece">{x.nights}</td><td data-label="Maliyet"><strong>{x.currentOrFinalCost.toLocaleString("tr-TR", { minimumFractionDigits: 2 })} {x.currencySnapshot}</strong><small>Gecelik {x.nightlyRateSnapshot.toLocaleString("tr-TR")}</small></td><td data-label="Durum"><span className={`status-badge ${x.status === "ACTIVE" ? "success" : x.status === "CANCELLED" ? "danger" : ""}`}>{stayStatus(x.status)}</span></td><td data-label="İşlem">{x.status === "ACTIVE" && permissions.has("camp.stay.manage") ? <div className="action-row"><button className="secondary-button button-success" type="button" disabled={busy} onClick={() => void closeStay(x)}>Çıkış yap</button><button className="secondary-button button-danger" type="button" disabled={busy} onClick={() => void cancelStay(x)}>İptal et</button></div> : "—"}</td></tr>)}</tbody></table></div>
      </section> : null}
    </div>
    {dialog}
  </main>;
}
