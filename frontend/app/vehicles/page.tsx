"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { Icon } from "../components/Icon";
import { PageHeader } from "../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type Permission = { code: string };
type Me = { permissions: Permission[] };
type Company = { id: string; code: string; name: string };
type Employee = { id: string; companyId: string; employeeNo: string; firstName: string; lastName: string; status: string };
type EmployeePage = { items: Employee[] };
type Vehicle = { id: string; companyId: string; plate: string; vin: string | null; brand: string; model: string; modelYear: number | null; status: string; insuranceValidUntil: string | null; inspectionValidUntil: string | null; currentOdometerKm: number | null; assignedEmployeeId: string | null; assignedEmployeeName: string | null; version: number };
type Assignment = { id: string; vehicleId: string; plate: string; employeeId: string; employeeNo: string; employeeName: string; validFrom: string; validUntilExclusive: string | null; status: string; version: number };
type Odometer = { id: string; odometerKm: number; occurredAt: string; source: string; note: string | null };
type Maintenance = { id: string; odometerKm: number; maintenanceType: string; description: string; cost: number; currency: string; serviceDate: string; nextDueDate: string | null; nextDueOdometerKm: number | null; vendor: string | null };
type Fuel = { id: string; odometerKm: number; liters: number; totalCost: number; currency: string; fueledAt: string; station: string | null; source: string };
type AuthResponse = { accessToken: string };

const today = () => new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 10);
const nowLocal = () => new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
const formatDate = (value: string | null) => value ? new Date(`${value}T00:00:00`).toLocaleDateString("tr-TR") : "—";
const vehicleStatus = (value: string) => value === "ACTIVE" ? "Aktif" : value === "MAINTENANCE" ? "Bakımda" : value === "OUT_OF_SERVICE" ? "Servis dışı" : value === "RETIRED" ? "Emekli" : value;
const assignmentStatus = (value: string) => value === "ACTIVE" ? "Devam ediyor" : value === "CLOSED" ? "Tamamlandı" : value;

export default function VehiclesPage() {
  const [me, setMe] = useState<Me | null>(null);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [odometer, setOdometer] = useState<Odometer[]>([]);
  const [maintenance, setMaintenance] = useState<Maintenance[]>([]);
  const [fuel, setFuel] = useState<Fuel[]>([]);
  const [companyId, setCompanyId] = useState("");
  const [vehicleId, setVehicleId] = useState("");
  const [message, setMessage] = useState("Araç verileri yükleniyor…");
  const [busy, setBusy] = useState(false);
  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);
  const selectedVehicle = vehicles.find(x => x.id === vehicleId) ?? null;
  const companyEmployees = employees.filter(x => !companyId || x.companyId === companyId);
  const activeAssignments = assignments.filter(x => x.status === "ACTIVE");
  const complianceLimit = new Date(); complianceLimit.setDate(complianceLimit.getDate() + 30);
  const complianceAttention = vehicles.filter(x => [x.insuranceValidUntil, x.inspectionValidUntil].some(value => value && new Date(`${value}T23:59:59`) <= complianceLimit));

  useEffect(() => { void initialize(); }, []);
  useEffect(() => { if (companyId) void reloadVehicles(companyId); }, [companyId]);
  useEffect(() => { if (vehicleId) void loadVehicleHistory(vehicleId); else { setOdometer([]); setMaintenance([]); setFuel([]); } }, [vehicleId]);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const codes = new Set(current.permissions.map(x => x.code));
    const [companyRows, employeeRows, assignmentRows] = await Promise.all([
      codes.has("organization.company.view") ? json<Company[]>("/api/v1/organization/companies") : Promise.resolve(null),
      codes.has("personnel.view") ? json<EmployeePage>("/api/v1/personnel/employees?status=ACTIVE&pageSize=100") : Promise.resolve(null),
      codes.has("administration.vehicle.view") ? json<Assignment[]>("/api/v1/administration/vehicles/assignments") : Promise.resolve(null),
    ]);
    const cs = companyRows ?? [];
    setCompanies(cs); setEmployees(employeeRows?.items ?? []); setAssignments(assignmentRows ?? []);
    if (cs.length) setCompanyId(cs[0].id);
    setMessage("Araç, sürücü, kilometre, bakım, yakıt ve uygunluk tarihleri hazır.");
  }

  async function reloadVehicles(cid = companyId) {
    if (!permissions.has("administration.vehicle.view") || !cid) return;
    const rows = await json<Vehicle[]>(`/api/v1/administration/vehicles?companyId=${cid}`) ?? [];
    setVehicles(rows);
    setVehicleId(current => rows.some(x => x.id === current) ? current : rows[0]?.id ?? "");
    setAssignments(await json<Assignment[]>(`/api/v1/administration/vehicles/assignments?companyId=${cid}`) ?? []);
  }

  async function loadVehicleHistory(id: string) {
    if (!permissions.has("administration.vehicle.view")) return;
    const [km, service, fuelRows] = await Promise.all([
      json<Odometer[]>(`/api/v1/administration/vehicles/${id}/odometer?take=100`),
      json<Maintenance[]>(`/api/v1/administration/vehicles/${id}/maintenance?take=100`),
      json<Fuel[]>(`/api/v1/administration/vehicles/${id}/fuel?take=100`),
    ]);
    setOdometer(km ?? []); setMaintenance(service ?? []); setFuel(fuelRows ?? []);
  }

  async function submit(event: FormEvent<HTMLFormElement>, path: string, body: (fd: FormData) => unknown, success: string) {
    event.preventDefault(); setBusy(true);
    try {
      const form = event.currentTarget; const fd = new FormData(form);
      const response = await authFetch(path, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body(fd)) });
      if (!response?.ok) { setMessage(await errorMessage(response, "İşlem tamamlanamadı.")); return; }
      form.reset(); setMessage(success); await reloadVehicles(); if (vehicleId) await loadVehicleHistory(vehicleId);
    } finally { setBusy(false); }
  }

  async function changeStatus(status: string) {
    if (!selectedVehicle) return; setBusy(true);
    try {
      const response = await authFetch(`/api/v1/administration/vehicles/${selectedVehicle.id}/status`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: selectedVehicle.version, status }) });
      if (!response?.ok) { setMessage(await errorMessage(response, "Durum değiştirilemedi.")); return; }
      setMessage(`Araç durumu ${status} olarak güncellendi.`); await reloadVehicles();
    } finally { setBusy(false); }
  }

  async function updateCompliance(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selectedVehicle) return; setBusy(true);
    try {
      const fd = new FormData(event.currentTarget);
      const response = await authFetch(`/api/v1/administration/vehicles/${selectedVehicle.id}/compliance`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          version: selectedVehicle.version,
          insuranceValidUntil: fd.get("insurance") || null,
          inspectionValidUntil: fd.get("inspection") || null,
        }),
      });
      if (!response?.ok) { setMessage(await errorMessage(response, "Sigorta/muayene tarihleri güncellenemedi.")); return; }
      setMessage("Sigorta ve muayene geçerlilik tarihleri güncellendi."); await reloadVehicles();
    } finally { setBusy(false); }
  }

  async function closeAssignment(row: Assignment) {
    const end = window.prompt("Atama bitiş tarihi [hariç] (YYYY-MM-DD)", today()); if (!end) return;
    const response = await authFetch(`/api/v1/administration/vehicles/assignments/${row.id}/close`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: row.version, validUntilExclusive: end }) });
    setMessage(response?.ok ? "Araç ataması kapatıldı." : await errorMessage(response, "Atama kapatılamadı.")); await reloadVehicles();
  }

  async function json<T>(path: string): Promise<T | null> { const r = await authFetch(path); return r?.ok ? await r.json() as T : null; }
  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh(); if (!token) return null;
    let r = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (r.status !== 401) return r; token = await refresh(); if (!token) return r;
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> { try { const r = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!r.ok) return null; const b = await r.json() as AuthResponse; sessionStorage.setItem("pp_access_token", b.accessToken); return b.accessToken; } catch { return null; } }
  async function errorMessage(response: Response | null, fallback: string) { if (!response) return fallback; const body = await response.json().catch(() => null) as { error?: { code?: string; message?: string } } | null; return body?.error?.code ? `${body.error.code}: ${body.error.message ?? fallback}` : body?.error?.message ?? fallback; }

  return <main className="page-shell">
    <PageHeader eyebrow="Araç yönetimi" title="Filo operasyonu" description="Araç, sürücü, kilometre, bakım, yakıt ve yasal uygunluk kayıtlarını tek yerde yönetin." status={message}/>

    <section className="stat-grid" aria-label="Filo özeti">
      <article className="stat-card"><span className="stat-icon"><Icon name="box"/></span><span className="stat-copy"><strong>{vehicles.length}</strong><span>Kayıtlı araç</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="people"/></span><span className="stat-copy"><strong>{activeAssignments.length}</strong><span>Aktif sürücü ataması</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="settings"/></span><span className="stat-copy"><strong>{vehicles.filter(x=>x.status==="MAINTENANCE").length}</strong><span>Bakımda araç</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="bell"/></span><span className="stat-copy"><strong>{complianceAttention.length}</strong><span>30 gün içinde belge uyarısı</span></span></article>
    </section>

    <section className="panel workspace-panel">
      <div className="workspace-copy"><span className="eyebrow dark">Çalışma kapsamı</span><h2>Şirket ve araç seçimi</h2><p>Durum, uygunluk, sürücü ve operasyon kayıtları seçili araç üzerinden yürütülür.</p></div>
      <div className="workspace-select inline-form"><label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Şirket seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Araç<select value={vehicleId} onChange={e => setVehicleId(e.target.value)}><option value="">Araç seçin</option>{vehicles.map(x => <option key={x.id} value={x.id}>{x.plate} · {x.brand} {x.model}</option>)}</select></label></div>
      {selectedVehicle ? <div className="selected-summary"><div className="selected-summary-copy"><strong>{selectedVehicle.plate} · {selectedVehicle.brand} {selectedVehicle.model}</strong><small>{(selectedVehicle.currentOdometerKm??0).toLocaleString("tr-TR")} km · {selectedVehicle.assignedEmployeeName??"Sürücü atanmamış"}</small></div><span className={`status-badge ${selectedVehicle.status==="ACTIVE"?"success":selectedVehicle.status==="MAINTENANCE"?"warning":"danger"}`}>{vehicleStatus(selectedVehicle.status)}</span></div> : null}
    </section>

    <div className="content-stack">
      {permissions.has("administration.vehicle.manage") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Araç kartı</span><h2>Yeni araç veya durum değişikliği</h2><p>Filo kaydı oluşturun; seçili aracın operasyon durumunu yönetin.</p></div></div><div className="form-surface"><div className="form-surface-heading"><div><strong>Yeni araç ekle</strong><span>Plaka, marka ve model zorunludur.</span></div></div><form className="inline-form" onSubmit={e => submit(e, "/api/v1/administration/vehicles", fd => ({ companyId, plate: fd.get("plate"), vin: fd.get("vin") || null, brand: fd.get("brand"), model: fd.get("model"), modelYear: Number(fd.get("modelYear")) || null, insuranceValidUntil: fd.get("insurance") || null, inspectionValidUntil: fd.get("inspection") || null, note: fd.get("note") || null }), "Araç kartı oluşturuldu.")}><label className="field-label">Plaka<input name="plate" required/></label><label className="field-label">VIN / şasi no<input name="vin"/></label><label className="field-label">Marka<input name="brand" required/></label><label className="field-label">Model<input name="model" required/></label><label className="field-label">Model yılı<input name="modelYear" type="number"/></label><label className="field-label">Sigorta geçerlilik<input name="insurance" type="date"/></label><label className="field-label">Muayene geçerlilik<input name="inspection" type="date"/></label><label className="field-label">Not<input name="note"/></label><button className="primary-button" disabled={busy || !companyId}>Aracı kaydet</button></form></div>{selectedVehicle ? <div className="selection-bar"><div className="selection-context"><strong>{selectedVehicle.plate} durumunu değiştir</strong><span>Mevcut: {vehicleStatus(selectedVehicle.status)}</span></div><div className="action-row">{[["ACTIVE","Aktif"],["MAINTENANCE","Bakımda"],["OUT_OF_SERVICE","Servis dışı"],["RETIRED","Emekli"]].map(([value,label]) => <button key={value} className="secondary-button" disabled={busy || selectedVehicle.status === value} onClick={() => void changeStatus(value)}>{label}</button>)}</div></div> : null}</section> : null}

      {selectedVehicle && (permissions.has("administration.vehicle.manage")||permissions.has("administration.vehicle.assign")) ? <section className="organization-grid">
        {permissions.has("administration.vehicle.manage") ? <article className={`panel attention-panel ${complianceAttention.some(x=>x.id===selectedVehicle.id)?"warning":"success"}`}><div className="panel-heading"><div><span className="eyebrow dark">Yasal uygunluk</span><h2>Sigorta ve muayene</h2><p>Yaklaşan belge tarihlerini güncel tutun.</p></div></div><div className="detail-grid"><div className="detail-item"><span>Sigorta</span><strong>{formatDate(selectedVehicle.insuranceValidUntil)}</strong></div><div className="detail-item"><span>Muayene</span><strong>{formatDate(selectedVehicle.inspectionValidUntil)}</strong></div></div><div className="form-surface"><form key={`${selectedVehicle.id}-${selectedVehicle.version}`} className="stack" onSubmit={updateCompliance}><label className="field-label">Sigorta geçerlilik<input name="insurance" type="date" defaultValue={selectedVehicle.insuranceValidUntil ?? ""}/></label><label className="field-label">Muayene geçerlilik<input name="inspection" type="date" defaultValue={selectedVehicle.inspectionValidUntil ?? ""}/></label><button className="primary-button" disabled={busy}>Tarihleri güncelle</button></form></div></article> : null}
        {permissions.has("administration.vehicle.assign") ? <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Sürücü ataması</span><h2>Personeli araca atayın</h2><p>Başlangıç ve planlı bitiş tarihini belirleyin.</p></div></div><div className="form-surface"><form className="stack" onSubmit={e => submit(e, `/api/v1/administration/vehicles/${selectedVehicle.id}/assignments`, fd => ({ vehicleId: selectedVehicle.id, employeeId: fd.get("employeeId"), validFrom: fd.get("validFrom"), validUntilExclusive: fd.get("validUntil") || null, note: fd.get("note") || null }), "Araç personele atandı.")}><label className="field-label">Personel<select name="employeeId" required><option value="">Personel seçin</option>{companyEmployees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select></label><label className="field-label">Başlangıç<input name="validFrom" type="date" defaultValue={today()} required/></label><label className="field-label">Planlanan bitiş (hariç)<input name="validUntil" type="date"/></label><label className="field-label">Not<input name="note"/></label><button className="primary-button" disabled={busy}>Atamayı başlat</button></form></div></article> : null}
      </section> : null}

      {permissions.has("administration.vehicle.view") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Sürücü geçmişi</span><h2>Araç atamaları</h2><p>Aktif atamaları bitiş tarihiyle kapatabilirsiniz.</p></div><strong>{assignments.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Araç</th><th>Personel</th><th>Başlangıç</th><th>Bitiş</th><th>Durum</th><th>İşlem</th></tr></thead><tbody>{assignments.length?assignments.map(x => <tr key={x.id}><td><strong>{x.plate}</strong></td><td>{x.employeeNo} · {x.employeeName}</td><td>{formatDate(x.validFrom)}</td><td>{x.validUntilExclusive?formatDate(x.validUntilExclusive):"Süresiz"}</td><td><span className={`status-badge ${x.status==="ACTIVE"?"success":""}`}>{assignmentStatus(x.status)}</span></td><td>{x.status === "ACTIVE" && permissions.has("administration.vehicle.assign") ? <button className="secondary-button button-success" onClick={() => void closeAssignment(x)}>Atamayı bitir</button> : "—"}</td></tr>):<tr><td className="empty-row" colSpan={6}>Henüz sürücü ataması yok.</td></tr>}</tbody></table></div></section> : null}

      {selectedVehicle && permissions.has("administration.vehicle.odometer.record") ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Kilometre defteri</span><h2>{selectedVehicle.plate} kilometre kayıtları</h2><p>Yeni değer önceki kayıtlardan düşük olamaz.</p></div><strong>{odometer.length}</strong></div><div className="form-surface"><form className="inline-form" onSubmit={e => submit(e, `/api/v1/administration/vehicles/${selectedVehicle.id}/odometer`, fd => ({ odometerKm: Number(fd.get("km")), occurredAt: fd.get("occurredAt"), source: "MANUAL", externalEventId: null, note: fd.get("note") || null }), "Kilometre olayı kaydedildi.")}><label className="field-label">Kilometre<input name="km" type="number" min="0" defaultValue={selectedVehicle.currentOdometerKm ?? 0} required/></label><label className="field-label">Kayıt zamanı<input name="occurredAt" type="datetime-local" defaultValue={nowLocal()} required/></label><label className="field-label">Not<input name="note"/></label><button className="primary-button" disabled={busy}>Kilometreyi kaydet</button></form></div>{odometer.length?<div className="compact-list">{odometer.map(x => <div className="role-row" key={x.id}><strong>{x.odometerKm.toLocaleString("tr-TR")} km</strong><span>{new Date(x.occurredAt).toLocaleString("tr-TR")} · Manuel kayıt</span></div>)}</div>:<p className="panel-description">Henüz kilometre kaydı yok.</p>}</section> : null}

      {selectedVehicle && (permissions.has("administration.vehicle.maintenance.manage")||permissions.has("administration.vehicle.fuel.record")) ? <section className="organization-grid">
        {permissions.has("administration.vehicle.maintenance.manage") ? <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Bakım ve servis</span><h2>Yeni bakım kaydı</h2><p>Maliyet ve sonraki bakım planını birlikte kaydedin.</p></div><strong>{maintenance.length}</strong></div><div className="form-surface"><form className="stack" onSubmit={e => submit(e, `/api/v1/administration/vehicles/${selectedVehicle.id}/maintenance`, fd => ({ odometerKm: Number(fd.get("km")), occurredAt: fd.get("occurredAt"), maintenanceType: fd.get("type"), description: fd.get("description"), cost: Number(fd.get("cost")), currency: fd.get("currency"), serviceDate: fd.get("serviceDate"), nextDueDate: fd.get("nextDate") || null, nextDueOdometerKm: Number(fd.get("nextKm")) || null, vendor: fd.get("vendor") || null }), "Bakım/servis kaydı oluşturuldu.")}><label className="field-label">Kilometre<input name="km" type="number" defaultValue={selectedVehicle.currentOdometerKm ?? 0} required/></label><label className="field-label">Kayıt zamanı<input name="occurredAt" type="datetime-local" defaultValue={nowLocal()} required/></label><label className="field-label">Bakım türü<input name="type" required/></label><label className="field-label">Açıklama<input name="description" required/></label><label className="field-label">Maliyet<input name="cost" type="number" min="0" step="0.01" defaultValue="0" required/></label><label className="field-label">Para birimi<input name="currency" defaultValue="TRY" maxLength={3} required/></label><label className="field-label">Servis tarihi<input name="serviceDate" type="date" defaultValue={today()} required/></label><label className="field-label">Sonraki tarih<input name="nextDate" type="date"/></label><label className="field-label">Sonraki kilometre<input name="nextKm" type="number"/></label><label className="field-label">Servis / tedarikçi<input name="vendor"/></label><button className="primary-button" disabled={busy}>Bakımı kaydet</button></form></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>KM</th><th>Tür</th><th>Maliyet</th><th>Sonraki</th></tr></thead><tbody>{maintenance.length?maintenance.map(x => <tr key={x.id}><td>{formatDate(x.serviceDate)}</td><td>{x.odometerKm.toLocaleString("tr-TR")}</td><td>{x.maintenanceType}<small>{x.vendor ?? "Tedarikçi yok"}</small></td><td>{x.cost.toLocaleString("tr-TR",{minimumFractionDigits:2})} {x.currency}</td><td>{formatDate(x.nextDueDate)}<small>{x.nextDueOdometerKm?`${x.nextDueOdometerKm.toLocaleString("tr-TR")} km`:"Kilometre planı yok"}</small></td></tr>):<tr><td className="empty-row" colSpan={5}>Henüz bakım kaydı yok.</td></tr>}</tbody></table></div></article> : null}
        {permissions.has("administration.vehicle.fuel.record") ? <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Yakıt</span><h2>Yeni yakıt kaydı</h2><p>Litre, maliyet ve kilometre bilgisini kaydedin.</p></div><strong>{fuel.length}</strong></div><div className="form-surface"><form className="stack" onSubmit={e => submit(e, `/api/v1/administration/vehicles/${selectedVehicle.id}/fuel`, fd => ({ odometerKm: Number(fd.get("km")), fueledAt: fd.get("fueledAt"), liters: Number(fd.get("liters")), totalCost: Number(fd.get("cost")), currency: fd.get("currency"), station: fd.get("station") || null, source: "MANUAL", externalEventId: null }), "Yakıt kaydı oluşturuldu.")}><label className="field-label">Kilometre<input name="km" type="number" defaultValue={selectedVehicle.currentOdometerKm ?? 0} required/></label><label className="field-label">Yakıt zamanı<input name="fueledAt" type="datetime-local" defaultValue={nowLocal()} required/></label><label className="field-label">Litre<input name="liters" type="number" min="0.001" step="0.001" required/></label><label className="field-label">Toplam maliyet<input name="cost" type="number" min="0" step="0.01" required/></label><label className="field-label">Para birimi<input name="currency" defaultValue="TRY" maxLength={3} required/></label><label className="field-label">İstasyon<input name="station"/></label><button className="primary-button" disabled={busy}>Yakıtı kaydet</button></form></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Zaman</th><th>KM</th><th>Litre</th><th>Maliyet</th><th>İstasyon</th></tr></thead><tbody>{fuel.length?fuel.map(x => <tr key={x.id}><td>{new Date(x.fueledAt).toLocaleString("tr-TR")}</td><td>{x.odometerKm.toLocaleString("tr-TR")}</td><td>{x.liters.toLocaleString("tr-TR")}</td><td><strong>{x.totalCost.toLocaleString("tr-TR",{minimumFractionDigits:2})} {x.currency}</strong></td><td>{x.station ?? "—"}</td></tr>):<tr><td className="empty-row" colSpan={5}>Henüz yakıt kaydı yok.</td></tr>}</tbody></table></div></article> : null}
      </section> : null}
    </div>
  </main>;
}
