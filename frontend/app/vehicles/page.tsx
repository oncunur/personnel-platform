"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

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

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 10 · ARAÇ</span><h1>Araç Yönetim Merkezi</h1><p>{message}</p></section>

    <section className="panel audit-panel"><div className="inline-form"><label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}</select></label><label className="field-label">Araç<select value={vehicleId} onChange={e => setVehicleId(e.target.value)}><option value="">Seçin</option>{vehicles.map(x => <option key={x.id} value={x.id}>{x.plate} · {x.brand} {x.model}</option>)}</select></label>{selectedVehicle ? <div><strong>{selectedVehicle.currentOdometerKm ?? 0} km</strong> · {selectedVehicle.status} · {selectedVehicle.assignedEmployeeName ?? "Atanmamış"}<br/><span>Sigorta: {selectedVehicle.insuranceValidUntil ?? "—"} · Muayene: {selectedVehicle.inspectionValidUntil ?? "—"}</span></div> : null}</div></section>

    {permissions.has("administration.vehicle.manage") ? <section className="panel audit-panel"><h2>Araç kartı</h2><form className="inline-form" onSubmit={e => submit(e, "/api/v1/administration/vehicles", fd => ({ companyId, plate: fd.get("plate"), vin: fd.get("vin") || null, brand: fd.get("brand"), model: fd.get("model"), modelYear: Number(fd.get("modelYear")) || null, insuranceValidUntil: fd.get("insurance") || null, inspectionValidUntil: fd.get("inspection") || null, note: fd.get("note") || null }), "Araç kartı oluşturuldu.")}><input name="plate" placeholder="Plaka" required/><input name="vin" placeholder="VIN/Şasi"/><input name="brand" placeholder="Marka" required/><input name="model" placeholder="Model" required/><input name="modelYear" type="number" placeholder="Model yılı"/><input name="insurance" type="date" title="Sigorta geçerlilik"/><input name="inspection" type="date" title="Muayene geçerlilik"/><input name="note" placeholder="Not"/><button className="primary-button" disabled={busy || !companyId}>Araç ekle</button></form>{selectedVehicle ? <div className="actions action-row">{["ACTIVE","MAINTENANCE","OUT_OF_SERVICE","RETIRED"].map(x => <button key={x} className="secondary-button" disabled={busy || selectedVehicle.status === x} onClick={() => void changeStatus(x)}>{x}</button>)}</div> : null}</section> : null}

    {selectedVehicle && permissions.has("administration.vehicle.manage") ? <section className="panel audit-panel"><h2>Sigorta / Muayene Yenileme</h2><form key={`${selectedVehicle.id}-${selectedVehicle.version}`} className="inline-form" onSubmit={updateCompliance}><label className="field-label">Sigorta geçerlilik<input name="insurance" type="date" defaultValue={selectedVehicle.insuranceValidUntil ?? ""}/></label><label className="field-label">Muayene geçerlilik<input name="inspection" type="date" defaultValue={selectedVehicle.inspectionValidUntil ?? ""}/></label><button className="primary-button" disabled={busy}>Tarihleri güncelle</button></form></section> : null}

    {selectedVehicle && permissions.has("administration.vehicle.assign") ? <section className="panel audit-panel"><h2>Personel / sürücü ataması</h2><form className="inline-form" onSubmit={e => submit(e, `/api/v1/administration/vehicles/${selectedVehicle.id}/assignments`, fd => ({ vehicleId: selectedVehicle.id, employeeId: fd.get("employeeId"), validFrom: fd.get("validFrom"), validUntilExclusive: fd.get("validUntil") || null, note: fd.get("note") || null }), "Araç personele atandı.")}><select name="employeeId" required><option value="">Personel seçin</option>{companyEmployees.map(x => <option key={x.id} value={x.id}>{x.employeeNo} · {x.firstName} {x.lastName}</option>)}</select><input name="validFrom" type="date" defaultValue={today()} required/><input name="validUntil" type="date"/><input name="note" placeholder="Not"/><button className="primary-button" disabled={busy}>Ata</button></form></section> : null}

    {permissions.has("administration.vehicle.view") ? <section className="panel audit-panel"><h2>Atama geçmişi</h2><div className="table-wrap"><table className="data-table"><thead><tr><th>Araç</th><th>Personel</th><th>Başlangıç</th><th>Bitiş [hariç]</th><th>Durum</th><th></th></tr></thead><tbody>{assignments.map(x => <tr key={x.id}><td>{x.plate}</td><td>{x.employeeNo} · {x.employeeName}</td><td>{x.validFrom}</td><td>{x.validUntilExclusive ?? "∞"}</td><td>{x.status}</td><td>{x.status === "ACTIVE" && permissions.has("administration.vehicle.assign") ? <button className="secondary-button" onClick={() => void closeAssignment(x)}>Kapat</button> : null}</td></tr>)}</tbody></table></div></section> : null}

    {selectedVehicle && permissions.has("administration.vehicle.odometer.record") ? <section className="panel audit-panel"><h2>Kilometre defteri</h2><form className="inline-form" onSubmit={e => submit(e, `/api/v1/administration/vehicles/${selectedVehicle.id}/odometer`, fd => ({ odometerKm: Number(fd.get("km")), occurredAt: fd.get("occurredAt"), source: "MANUAL", externalEventId: null, note: fd.get("note") || null }), "Kilometre olayı kaydedildi.")}><input name="km" type="number" min="0" defaultValue={selectedVehicle.currentOdometerKm ?? 0} required/><input name="occurredAt" type="datetime-local" defaultValue={nowLocal()} required/><input name="note" placeholder="Not"/><button className="primary-button" disabled={busy}>KM kaydet</button></form><div className="compact-list">{odometer.map(x => <div key={x.id}><strong>{x.odometerKm} km</strong> · {new Date(x.occurredAt).toLocaleString()} · {x.source}</div>)}</div></section> : null}

    {selectedVehicle && permissions.has("administration.vehicle.maintenance.manage") ? <section className="panel audit-panel"><h2>Bakım / Servis</h2><form className="inline-form" onSubmit={e => submit(e, `/api/v1/administration/vehicles/${selectedVehicle.id}/maintenance`, fd => ({ odometerKm: Number(fd.get("km")), occurredAt: fd.get("occurredAt"), maintenanceType: fd.get("type"), description: fd.get("description"), cost: Number(fd.get("cost")), currency: fd.get("currency"), serviceDate: fd.get("serviceDate"), nextDueDate: fd.get("nextDate") || null, nextDueOdometerKm: Number(fd.get("nextKm")) || null, vendor: fd.get("vendor") || null }), "Bakım/servis kaydı oluşturuldu.")}><input name="km" type="number" defaultValue={selectedVehicle.currentOdometerKm ?? 0} required/><input name="occurredAt" type="datetime-local" defaultValue={nowLocal()} required/><input name="type" placeholder="Bakım türü" required/><input name="description" placeholder="Açıklama" required/><input name="cost" type="number" min="0" step="0.01" defaultValue="0" required/><input name="currency" defaultValue="TRY" maxLength={3} required/><input name="serviceDate" type="date" defaultValue={today()} required/><input name="nextDate" type="date"/><input name="nextKm" type="number" placeholder="Sonraki km"/><input name="vendor" placeholder="Servis/Tedarikçi"/><button className="primary-button" disabled={busy}>Bakım kaydet</button></form><div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>KM</th><th>Tür</th><th>Maliyet</th><th>Sonraki</th></tr></thead><tbody>{maintenance.map(x => <tr key={x.id}><td>{x.serviceDate}</td><td>{x.odometerKm}</td><td>{x.maintenanceType} · {x.vendor ?? "—"}</td><td>{x.cost.toFixed(2)} {x.currency}</td><td>{x.nextDueDate ?? "—"} / {x.nextDueOdometerKm ?? "—"} km</td></tr>)}</tbody></table></div></section> : null}

    {selectedVehicle && permissions.has("administration.vehicle.fuel.record") ? <section className="panel audit-panel"><h2>Yakıt</h2><form className="inline-form" onSubmit={e => submit(e, `/api/v1/administration/vehicles/${selectedVehicle.id}/fuel`, fd => ({ odometerKm: Number(fd.get("km")), fueledAt: fd.get("fueledAt"), liters: Number(fd.get("liters")), totalCost: Number(fd.get("cost")), currency: fd.get("currency"), station: fd.get("station") || null, source: "MANUAL", externalEventId: null }), "Yakıt kaydı oluşturuldu.")}><input name="km" type="number" defaultValue={selectedVehicle.currentOdometerKm ?? 0} required/><input name="fueledAt" type="datetime-local" defaultValue={nowLocal()} required/><input name="liters" type="number" min="0.001" step="0.001" placeholder="Litre" required/><input name="cost" type="number" min="0" step="0.01" placeholder="Toplam maliyet" required/><input name="currency" defaultValue="TRY" maxLength={3} required/><input name="station" placeholder="İstasyon"/><button className="primary-button" disabled={busy}>Yakıt kaydet</button></form><div className="table-wrap"><table className="data-table"><thead><tr><th>Zaman</th><th>KM</th><th>Litre</th><th>Maliyet</th><th>İstasyon</th></tr></thead><tbody>{fuel.map(x => <tr key={x.id}><td>{new Date(x.fueledAt).toLocaleString()}</td><td>{x.odometerKm}</td><td>{x.liters}</td><td>{x.totalCost.toFixed(2)} {x.currency}</td><td>{x.station ?? "—"}</td></tr>)}</tbody></table></div></section> : null}
  </main>;
}
