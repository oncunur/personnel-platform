"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type Permission = { code: string };
type Me = { permissions: Permission[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
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

export default function CampPage() {
  const [me, setMe] = useState<Me | null>(null);
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
  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const selectedCamp = useMemo(() => camps.find(x => x.id === campId) ?? null, [camps, campId]);

  useEffect(() => { void initialize(); }, []);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const [campRows, stayPage, employeePage] = await Promise.all([
      current.permissions.some(x => x.code === "camp.site.view") ? json<Camp[]>("/api/v1/camp/sites") : Promise.resolve(null),
      current.permissions.some(x => x.code === "camp.stay.view") ? json<StayPage>("/api/v1/camp/stays?page=1&pageSize=100") : Promise.resolve(null),
      current.permissions.some(x => x.code === "personnel.view") ? json<EmployeePage>("/api/v1/personnel/employees?status=ACTIVE&pageSize=100") : Promise.resolve(null),
    ]);
    setCamps(campRows ?? []);
    setStays(stayPage?.items ?? []);
    setEmployees(employeePage?.items ?? []);
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
      const form = new FormData(event.currentTarget);
      const response = await authFetch("/api/v1/camp/sites", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ companyId: form.get("companyId"), code: form.get("code"), name: form.get("name"), address: form.get("address") || null }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Kamp oluşturulamadı.")); return; }
      event.currentTarget.reset();
      setCamps(await json<Camp[]>("/api/v1/camp/sites") ?? []); setMessage("Kamp oluşturuldu.");
    } finally { setBusy(false); }
  }

  async function createRoom(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!campId) return; setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const floorRaw = String(form.get("floor") ?? "");
      const response = await authFetch(`/api/v1/camp/sites/${campId}/rooms`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ code: form.get("code"), name: form.get("name"), floor: floorRaw ? Number(floorRaw) : null }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Oda oluşturulamadı.")); return; }
      event.currentTarget.reset(); setRooms(await json<Room[]>(`/api/v1/camp/sites/${campId}/rooms`) ?? []); setMessage("Oda oluşturuldu.");
    } finally { setBusy(false); }
  }

  async function createBed(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!roomId) return; setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const response = await authFetch(`/api/v1/camp/rooms/${roomId}/beds`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ code: form.get("code") }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Yatak oluşturulamadı.")); return; }
      event.currentTarget.reset(); setBeds(await json<Bed[]>(`/api/v1/camp/rooms/${roomId}/beds`) ?? []); setMessage("Yatak oluşturuldu.");
    } finally { setBusy(false); }
  }

  async function createRate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!campId) return; setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const response = await authFetch(`/api/v1/camp/sites/${campId}/rates`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ validFrom: form.get("validFrom"), validUntilExclusive: form.get("validUntilExclusive") || null, nightlyRate: Number(form.get("nightlyRate")), currency: form.get("currency") }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Fiyat kaydedilemedi.")); return; }
      event.currentTarget.reset(); setRates(await json<Rate[]>(`/api/v1/camp/sites/${campId}/rates`) ?? []); setMessage("Tarih-etkin konaklama fiyatı kaydedildi.");
    } finally { setBusy(false); }
  }

  async function createStay(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!campId || !roomId || !bedId || !employeeId) return; setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const response = await authFetch("/api/v1/camp/stays", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ employeeId, campId, roomId, bedId, checkInDate: form.get("checkInDate"), checkOutDateExclusive: form.get("checkOutDateExclusive") || null, note: form.get("note") || null }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Konaklama oluşturulamadı.")); return; }
      event.currentTarget.reset(); setMessage("Personel yatağa atandı. Tarih çakışmaları DB seviyesinde korunuyor."); await reloadStays();
    } finally { setBusy(false); }
  }

  async function closeStay(row: Stay) {
    const checkOut = window.prompt("Çıkış tarihi (YYYY-MM-DD, bu tarih konaklamaya dahil değildir):", localDate(1));
    if (!checkOut) return;
    const response = await authFetch(`/api/v1/camp/stays/${row.id}/close`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ checkOutDateExclusive: checkOut, version: row.version }) });
    if (!response?.ok) { setMessage(await errorMessage(response, "Konaklama kapatılamadı.")); return; }
    setMessage("Konaklama kapatıldı ve toplam maliyet snapshot olarak sabitlendi."); await reloadStays();
  }

  async function cancelStay(row: Stay) {
    if (!window.confirm("Bu konaklama kaydı iptal edilsin mi?")) return;
    const response = await authFetch(`/api/v1/camp/stays/${row.id}/cancel`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: row.version }) });
    if (!response?.ok) { setMessage(await errorMessage(response, "Konaklama iptal edilemedi.")); return; }
    setMessage("Konaklama iptal edildi."); await reloadStays();
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

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 6 · KAMP</span><h1>Kamp & Konaklama Merkezi</h1><p>{message}</p></section>

    <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">KAMP YAPISI</span><h2>Kamp → Oda → Yatak</h2></div></div>
      <div className="inline-form">
        <label className="field-label">Kamp<select value={campId} onChange={e => void selectCamp(e.target.value)}><option value="">Seçin</option>{camps.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
        <label className="field-label">Oda<select value={roomId} onChange={e => void selectRoom(e.target.value)} disabled={!campId}><option value="">Seçin</option>{rooms.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label>
        <label className="field-label">Yatak<select value={bedId} onChange={e => setBedId(e.target.value)} disabled={!roomId}><option value="">Seçin</option>{beds.map(x => <option key={x.id} value={x.id}>{x.code}</option>)}</select></label>
      </div>
    </section>

    {permissions.has("camp.site.manage") ? <section className="grid">
      <article className="panel"><h2>Kamp oluştur</h2><form onSubmit={createCamp} className="stack"><input name="companyId" placeholder="Company UUID" required/><input name="code" placeholder="Kamp kodu" required/><input name="name" placeholder="Kamp adı" required/><input name="address" placeholder="Adres"/><button className="primary-button" disabled={busy}>Kaydet</button></form></article>
      <article className="panel"><h2>Oda oluştur</h2><form onSubmit={createRoom} className="stack"><input name="code" placeholder="Oda kodu" required/><input name="name" placeholder="Oda adı" required/><input name="floor" type="number" placeholder="Kat"/><button className="primary-button" disabled={busy || !campId}>Kaydet</button></form></article>
      <article className="panel"><h2>Yatak oluştur</h2><form onSubmit={createBed} className="stack"><input name="code" placeholder="Yatak kodu" required/><button className="primary-button" disabled={busy || !roomId}>Kaydet</button></form></article>
    </section> : null}

    {permissions.has("camp.rate.view") ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">FİYAT</span><h2>{selectedCamp?.name ?? "Kamp"} konaklama fiyatları</h2></div><strong>{rates.length}</strong></div>
      {permissions.has("camp.rate.manage") ? <form className="inline-form" onSubmit={createRate}><label className="field-label">Başlangıç<input name="validFrom" type="date" defaultValue={localDate()} required/></label><label className="field-label">Bitiş [hariç]<input name="validUntilExclusive" type="date"/></label><label className="field-label">Gecelik<input name="nightlyRate" type="number" min="0.01" step="0.01" required/></label><label className="field-label">Döviz<input name="currency" defaultValue="TRY" maxLength={3} required/></label><button className="primary-button" disabled={busy || !campId}>Fiyat ekle</button></form> : null}
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Başlangıç</th><th>Bitiş [hariç]</th><th>Gecelik</th></tr></thead><tbody>{rates.length === 0 ? <tr><td colSpan={3}>Fiyat kaydı yok.</td></tr> : rates.map(x => <tr key={x.id}><td>{x.validFrom}</td><td>{x.validUntilExclusive ?? "∞"}</td><td>{x.nightlyRate.toFixed(2)} {x.currency}</td></tr>)}</tbody></table></div>
    </section> : null}

    {permissions.has("camp.stay.manage") ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">YERLEŞİM</span><h2>Personeli yatağa ata</h2></div></div>
      <form className="inline-form" onSubmit={createStay}><label className="field-label">Personel<select value={employeeId} onChange={e => setEmployeeId(e.target.value)} required><option value="">Seçin</option>{employees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label><label className="field-label">Giriş<input name="checkInDate" type="date" defaultValue={localDate()} required/></label><label className="field-label">Planlı çıkış [hariç]<input name="checkOutDateExclusive" type="date"/></label><label className="field-label">Not<input name="note" maxLength={2000}/></label><button className="primary-button" disabled={busy || !campId || !roomId || !bedId || !employeeId}>Konaklama aç</button></form>
      {!permissions.has("personnel.view") ? <p>Personel seçimi için mevcut sürümde ayrıca <code>personnel.view</code> yetkisi gerekir.</p> : null}
    </section> : null}

    {permissions.has("camp.stay.view") ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">KONAKLAMA</span><h2>Güncel & geçmiş kayıtlar</h2></div><strong>{stays.length}</strong></div>
      <div className="table-wrap"><table className="data-table"><thead><tr><th>Personel</th><th>Kamp</th><th>Oda/Yatak</th><th>Giriş</th><th>Çıkış [hariç]</th><th>Gün</th><th>Gecelik</th><th>Maliyet</th><th>Durum</th><th>İşlem</th></tr></thead><tbody>{stays.length === 0 ? <tr><td colSpan={10}>Konaklama kaydı yok.</td></tr> : stays.map(x => <tr key={x.id}><td>{x.employeeNo} · {x.employeeName}</td><td>{x.campCode} · {x.campName}</td><td>{x.roomCode}/{x.bedCode}</td><td>{x.checkInDate}</td><td>{x.checkOutDateExclusive ?? "Açık"}</td><td>{x.nights}</td><td>{x.nightlyRateSnapshot.toFixed(2)} {x.currencySnapshot}</td><td>{x.currentOrFinalCost.toFixed(2)} {x.currencySnapshot}</td><td><strong>{x.status}</strong></td><td>{x.status === "ACTIVE" && permissions.has("camp.stay.manage") ? <div className="actions"><button className="secondary-button" onClick={() => void closeStay(x)}>Kapat</button><button className="secondary-button" onClick={() => void cancelStay(x)}>İptal</button></div> : "—"}</td></tr>)}</tbody></table></div>
    </section> : null}
  </main>;
}
