"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
type AuthResponse = { accessToken: string };
type Company = { id: string; code: string; name: string };
type SystemRow = { id: string; companyId: string; code: string; name: string; systemType: string; isActive: boolean; version: number };
type Device = { id: string; companyId: string; integrationSystemId: string; code: string; name: string; deviceType: string; scopedCampId: string | null; isActive: boolean; lastSeenAt: string | null; lastErrorAt: string | null; lastErrorMessage: string | null; version: number };
type Mapping = { id: string; companyId: string; integrationSystemId: string; entityType: string; externalCode: string; internalEntityId: string; isActive: boolean; version: number };
type Staging = { id: string; companyId: string; integrationSystemId: string; deviceId: string | null; eventType: string; externalEventId: string; status: string; attemptCount: number; nextRetryAt: string | null; errorCode: string | null; errorMessage: string | null; processedEntityType: string | null; processedEntityId: string | null; receivedAt: string; version: number };
type DeviceHealth = { deviceId: string; deviceCode: string; deviceName: string; deviceType: string; health: string; lastSeenAt: string | null; lastErrorAt: string | null; lastErrorMessage: string | null };
type SystemHealth = { systemId: string; systemCode: string; systemName: string; systemType: string; lastEventAt: string | null; lastProcessedAt: string | null; lastErrorAt: string | null; queue: { received: number; processing: number; businessError: number; technicalError: number; deadLetter: number; processed: number }; devices: DeviceHealth[] };
type Monitoring = { companyId: string; systems: SystemHealth[]; totalBacklog: number; totalErrors: number; totalDeadLetters: number };
type Credential = { device: Device; plaintextKey: string };

export default function IntegrationsPage() {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [companyId, setCompanyId] = useState("");
  const [systems, setSystems] = useState<SystemRow[]>([]);
  const [systemId, setSystemId] = useState("");
  const [devices, setDevices] = useState<Device[]>([]);
  const [mappings, setMappings] = useState<Mapping[]>([]);
  const [queue, setQueue] = useState<Staging[]>([]);
  const [monitor, setMonitor] = useState<Monitoring | null>(null);
  const [message, setMessage] = useState("Integration Center yükleniyor…");
  const [oneTimeKey, setOneTimeKey] = useState("");
  const [systemForm, setSystemForm] = useState({ code: "", name: "", systemType: "PDKS" });
  const [deviceForm, setDeviceForm] = useState({ code: "", name: "", deviceType: "PDKS_TERMINAL", scopedCampId: "" });
  const [mappingForm, setMappingForm] = useState({ entityType: "EMPLOYEE", externalCode: "", internalEntityId: "" });
  const selectedSystem = useMemo(() => systems.find(x => x.id === systemId) ?? null, [systems, systemId]);

  useEffect(() => { void bootstrap(); }, []);
  useEffect(() => { if (systemId) void loadSystemDetails(systemId); else { setDevices([]); setMappings([]); } }, [systemId]);
  useEffect(() => { if (companyId) void loadCompanyData(companyId); }, [companyId]);

  async function bootstrap() {
    const [companyResponse, systemResponse] = await Promise.all([authFetch("/api/v1/organization/companies"), authFetch("/api/v1/integrations/systems")]);
    const cs = companyResponse?.ok ? await companyResponse.json() as Company[] : [];
    const ss = systemResponse?.ok ? await systemResponse.json() as SystemRow[] : [];
    setCompanies(cs); setSystems(ss);
    const firstCompany = cs[0]?.id ?? ss[0]?.companyId ?? "";
    if (firstCompany) setCompanyId(firstCompany);
    if (ss[0]) setSystemId(ss[0].id);
    setMessage("Sistem/device, mapping, staging queue ve monitoring tek merkezde yönetilir.");
  }

  async function loadCompanyData(id: string) {
    const [systemsResponse, queueResponse, monitorResponse] = await Promise.all([
      authFetch(`/api/v1/integrations/systems?companyId=${id}`),
      authFetch(`/api/v1/integrations/queue?companyId=${id}&take=300`),
      authFetch(`/api/v1/integrations/monitoring?companyId=${id}`),
    ]);
    if (systemsResponse?.ok) {
      const rows = await systemsResponse.json() as SystemRow[]; setSystems(rows);
      if (!rows.some(x => x.id === systemId)) setSystemId(rows[0]?.id ?? "");
    }
    setQueue(queueResponse?.ok ? await queueResponse.json() as Staging[] : []);
    setMonitor(monitorResponse?.ok ? await monitorResponse.json() as Monitoring : null);
  }

  async function loadSystemDetails(id: string) {
    const [deviceResponse, mappingResponse] = await Promise.all([
      authFetch(`/api/v1/integrations/systems/${id}/devices`),
      authFetch(`/api/v1/integrations/systems/${id}/mappings`),
    ]);
    setDevices(deviceResponse?.ok ? await deviceResponse.json() as Device[] : []);
    setMappings(mappingResponse?.ok ? await mappingResponse.json() as Mapping[] : []);
    const system = systems.find(x => x.id === id);
    if (system) setDeviceForm(x => ({ ...x, deviceType: system.systemType === "MEAL" ? "MEAL_TERMINAL" : system.systemType === "PDKS" ? "PDKS_TERMINAL" : "GENERIC" }));
  }

  async function createSystem(event: FormEvent) {
    event.preventDefault(); if (!companyId) return;
    const response = await authFetch("/api/v1/integrations/systems", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ companyId, ...systemForm }) });
    if (!response?.ok) return setMessage(await apiError(response, "Sistem oluşturulamadı."));
    const row = await response.json() as SystemRow; setSystemForm({ code: "", name: "", systemType: "PDKS" }); setSystemId(row.id); setMessage("Entegrasyon sistemi oluşturuldu."); await loadCompanyData(companyId);
  }

  async function createDevice(event: FormEvent) {
    event.preventDefault(); if (!systemId) return;
    const response = await authFetch("/api/v1/integrations/devices", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ integrationSystemId: systemId, code: deviceForm.code, name: deviceForm.name, deviceType: deviceForm.deviceType, scopedCampId: deviceForm.scopedCampId || null }) });
    if (!response?.ok) return setMessage(await apiError(response, "Cihaz oluşturulamadı."));
    const credential = await response.json() as Credential; setOneTimeKey(credential.plaintextKey); setDeviceForm(x => ({ ...x, code: "", name: "", scopedCampId: "" })); setMessage("Cihaz oluşturuldu. Anahtarı şimdi güvenli yere kaydedin; tekrar gösterilmeyecek."); await loadSystemDetails(systemId); await loadCompanyData(companyId);
  }

  async function rotateKey(device: Device) {
    if (!confirm(`${device.code} cihaz anahtarı yenilensin mi? Eski anahtar hemen geçersiz olur.`)) return;
    const response = await authFetch(`/api/v1/integrations/devices/${device.id}/rotate-key`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: device.version }) });
    if (!response?.ok) return setMessage(await apiError(response, "Anahtar yenilenemedi."));
    const credential = await response.json() as Credential; setOneTimeKey(credential.plaintextKey); setMessage("Yeni cihaz anahtarı üretildi. Bu değer yalnız bu yanıtta gösterilir."); await loadSystemDetails(systemId);
  }

  async function createMapping(event: FormEvent) {
    event.preventDefault(); if (!systemId) return;
    const response = await authFetch("/api/v1/integrations/mappings", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ integrationSystemId: systemId, ...mappingForm }) });
    if (!response?.ok) return setMessage(await apiError(response, "Mapping oluşturulamadı."));
    setMappingForm(x => ({ ...x, externalCode: "", internalEntityId: "" })); setMessage("External entity mapping oluşturuldu."); await loadSystemDetails(systemId);
  }

  async function reprocess(row: Staging) {
    const response = await authFetch(`/api/v1/integrations/queue/${row.id}/reprocess`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: row.version }) });
    if (!response?.ok) return setMessage(await apiError(response, "Kayıt yeniden kuyruğa alınamadı."));
    setMessage("Staging kaydı yeniden RECEIVED durumuna alındı; worker tekrar işleyecek."); await loadCompanyData(companyId);
  }

  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh();
    if (!token) { window.location.replace("/login"); return null; }
    let response = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status !== 401) return response;
    token = await refresh(); if (!token) { window.location.replace("/login"); return response; }
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> { try { const r = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!r.ok) return null; const b = await r.json() as AuthResponse; sessionStorage.setItem("pp_access_token", b.accessToken); return b.accessToken; } catch { return null; } }
  async function apiError(response: Response, fallback: string) { const body = await response.json().catch(() => null) as { error?: { code?: string; message?: string } } | null; return body?.error?.code ? `${body.error.code}: ${body.error.message ?? fallback}` : body?.error?.message ?? fallback; }
  const dateTime = (value: string | null) => value ? new Date(value).toLocaleString("tr-TR") : "—";
  const healthClass = (health: string) => health === "HEALTHY" ? "status-ok" : health === "STALE" ? "status-warn" : "status-error";

  return <main className="shell">
    <a className="back" href="/dashboard">← Dashboard</a>
    <section className="hero compact"><span className="eyebrow">SPRINT 14 · INTEGRATIONS</span><h1>Integration Center</h1><p>{message}</p></section>
    <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">KAPSAM</span><h2>Şirket & Sistem</h2></div></div><div className="inline-form"><label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Şirket seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}{companies.length === 0 && companyId ? <option value={companyId}>{companyId}</option> : null}</select></label><label className="field-label">Sistem<select value={systemId} onChange={e => setSystemId(e.target.value)}><option value="">Sistem seçin</option>{systems.map(x => <option key={x.id} value={x.id}>{x.code} · {x.systemType}</option>)}</select></label></div></section>

    {monitor ? <section className="grid"><article className="card"><span>Queue Backlog</span><h2>{monitor.totalBacklog}</h2></article><article className="card"><span>Aktif Hata</span><h2>{monitor.totalErrors}</h2></article><article className="card"><span>Dead Letter</span><h2>{monitor.totalDeadLetters}</h2></article><article className="card"><span>Sistem</span><h2>{monitor.systems.length}</h2></article></section> : null}

    <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">INT-001</span><h2>Yeni Entegrasyon Sistemi</h2></div></div><form className="inline-form" onSubmit={createSystem}><input placeholder="Kod (örn. PDKS_MAIN)" value={systemForm.code} onChange={e => setSystemForm({ ...systemForm, code: e.target.value })} required /><input placeholder="Ad" value={systemForm.name} onChange={e => setSystemForm({ ...systemForm, name: e.target.value })} required /><select value={systemForm.systemType} onChange={e => setSystemForm({ ...systemForm, systemType: e.target.value })}><option>PDKS</option><option>MEAL</option><option>ERP</option><option>IMPORT</option></select><button className="primary-button" disabled={!companyId}>Oluştur</button></form></section>

    {selectedSystem ? <>
      <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">DEVICE IDENTITY</span><h2>{selectedSystem.code} Cihazları</h2></div><strong>{devices.length}</strong></div>{oneTimeKey ? <div className="notice"><strong>Tek seferlik cihaz anahtarı</strong><code style={{ wordBreak: "break-all" }}>{oneTimeKey}</code><button className="secondary-button" type="button" onClick={() => navigator.clipboard.writeText(oneTimeKey)}>Kopyala</button><button className="secondary-button" type="button" onClick={() => setOneTimeKey("")}>Gizle</button></div> : null}<form className="inline-form" onSubmit={createDevice}><input placeholder="Cihaz kodu" value={deviceForm.code} onChange={e => setDeviceForm({ ...deviceForm, code: e.target.value })} required /><input placeholder="Cihaz adı" value={deviceForm.name} onChange={e => setDeviceForm({ ...deviceForm, name: e.target.value })} required /><select value={deviceForm.deviceType} onChange={e => setDeviceForm({ ...deviceForm, deviceType: e.target.value })}><option>PDKS_TERMINAL</option><option>MEAL_TERMINAL</option><option>GENERIC</option></select><input placeholder="Camp UUID (meal terminal)" value={deviceForm.scopedCampId} onChange={e => setDeviceForm({ ...deviceForm, scopedCampId: e.target.value })} /><button className="primary-button">Cihaz oluştur</button></form><div className="table-wrap"><table className="data-table"><thead><tr><th>Cihaz</th><th>Tür</th><th>Camp Scope</th><th>Son Görülme</th><th>Son Hata</th><th></th></tr></thead><tbody>{devices.map(x => <tr key={x.id}><td><strong>{x.code}</strong><small>{x.name}</small></td><td>{x.deviceType}</td><td>{x.scopedCampId ?? "—"}</td><td>{dateTime(x.lastSeenAt)}</td><td>{x.lastErrorMessage ?? "—"}<small>{dateTime(x.lastErrorAt)}</small></td><td><button className="secondary-button" type="button" onClick={() => rotateKey(x)}>Key rotate</button></td></tr>)}</tbody></table></div></section>

      <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">ENTITY MAPPING</span><h2>External → Core</h2></div><strong>{mappings.length}</strong></div><form className="inline-form" onSubmit={createMapping}><select value={mappingForm.entityType} onChange={e => setMappingForm({ ...mappingForm, entityType: e.target.value })}><option>EMPLOYEE</option><option>CAMP</option><option>MEAL_TYPE</option><option>PROJECT</option><option>COST_CENTER</option><option>COST_CATEGORY</option></select><input placeholder="External code" value={mappingForm.externalCode} onChange={e => setMappingForm({ ...mappingForm, externalCode: e.target.value })} required /><input placeholder="Internal entity UUID" value={mappingForm.internalEntityId} onChange={e => setMappingForm({ ...mappingForm, internalEntityId: e.target.value })} required /><button className="primary-button">Mapping ekle</button></form><div className="table-wrap"><table className="data-table"><thead><tr><th>Tür</th><th>External</th><th>Internal UUID</th><th>Durum</th></tr></thead><tbody>{mappings.map(x => <tr key={x.id}><td>{x.entityType}</td><td><strong>{x.externalCode}</strong></td><td><code>{x.internalEntityId}</code></td><td>{x.isActive ? "ACTIVE" : "PASSIVE"}</td></tr>)}</tbody></table></div></section>
    </> : null}

    <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">INT-002</span><h2>Raw / Staging Queue</h2></div><strong>{queue.length} kayıt</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Alınma</th><th>Event</th><th>External ID</th><th>Status</th><th>Attempt</th><th>Hata</th><th>Core</th><th></th></tr></thead><tbody>{queue.length === 0 ? <tr><td colSpan={8}>Queue boş.</td></tr> : queue.map(x => <tr key={x.id}><td>{dateTime(x.receivedAt)}</td><td>{x.eventType}</td><td><code>{x.externalEventId}</code></td><td><strong>{x.status}</strong></td><td>{x.attemptCount}<small>{x.nextRetryAt ? `Retry: ${dateTime(x.nextRetryAt)}` : ""}</small></td><td>{x.errorCode ?? "—"}<small>{x.errorMessage ?? ""}</small></td><td>{x.processedEntityType ?? "—"}<small>{x.processedEntityId ?? ""}</small></td><td>{["BUSINESS_ERROR", "TECHNICAL_ERROR", "DEAD_LETTER"].includes(x.status) ? <button className="secondary-button" type="button" onClick={() => reprocess(x)}>Reprocess</button> : null}</td></tr>)}</tbody></table></div></section>

    {monitor ? <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">INT-008</span><h2>System & Device Health</h2></div></div>{monitor.systems.map(s => <article className="card" key={s.systemId}><span>{s.systemType}</span><h2>{s.systemCode} · {s.systemName}</h2><p>Last event: {dateTime(s.lastEventAt)} · Last processed: {dateTime(s.lastProcessedAt)} · Business error: {s.queue.businessError} · Technical error: {s.queue.technicalError} · Dead letter: {s.queue.deadLetter}</p><div className="table-wrap"><table className="data-table"><thead><tr><th>Cihaz</th><th>Health</th><th>Son Seen</th><th>Son Error</th></tr></thead><tbody>{s.devices.length === 0 ? <tr><td colSpan={4}>Cihaz yok.</td></tr> : s.devices.map(d => <tr key={d.deviceId}><td>{d.deviceCode}<small>{d.deviceName}</small></td><td><strong className={healthClass(d.health)}>{d.health}</strong></td><td>{dateTime(d.lastSeenAt)}</td><td>{d.lastErrorMessage ?? "—"}<small>{dateTime(d.lastErrorAt)}</small></td></tr>)}</tbody></table></div></article>)}</section> : null}
  </main>;
}
