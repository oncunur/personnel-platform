"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useActionDialog } from "../components/ActionDialog";
import { Icon } from "../components/Icon";
import { OperationDisclosure } from "../components/OperationDisclosure";
import { PageHeader } from "../components/PageHeader";

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

const systemTypeLabel = (value: string) => value === "PDKS" ? "Puantaj sistemi" : value === "MEAL" ? "Yemek sistemi" : value === "ERP" ? "ERP" : value === "IMPORT" ? "Dosya aktarımı" : value;
const deviceTypeLabel = (value: string) => value === "PDKS_TERMINAL" ? "Puantaj terminali" : value === "MEAL_TERMINAL" ? "Yemek terminali" : value === "GENERIC" ? "Genel cihaz" : value;
const entityLabel = (value: string) => value === "EMPLOYEE" ? "Personel" : value === "CAMP" ? "Kamp" : value === "MEAL_TYPE" ? "Öğün" : value === "PROJECT" ? "Proje" : value === "COST_CENTER" ? "Maliyet merkezi" : value === "COST_CATEGORY" ? "Maliyet kategorisi" : value;
const queueStatus = (value: string) => value === "RECEIVED" ? "Alındı" : value === "PROCESSING" ? "İşleniyor" : value === "PROCESSED" ? "Tamamlandı" : value === "BUSINESS_ERROR" ? "İş kuralı hatası" : value === "TECHNICAL_ERROR" ? "Teknik hata" : value === "DEAD_LETTER" ? "İşlenemedi" : value;
const healthLabel = (value: string) => value === "HEALTHY" ? "Sağlıklı" : value === "STALE" ? "Gecikmeli" : value === "ERROR" ? "Hatalı" : value;

export default function IntegrationsPage() {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [companyId, setCompanyId] = useState("");
  const [systems, setSystems] = useState<SystemRow[]>([]);
  const [systemId, setSystemId] = useState("");
  const [devices, setDevices] = useState<Device[]>([]);
  const [mappings, setMappings] = useState<Mapping[]>([]);
  const [queue, setQueue] = useState<Staging[]>([]);
  const [monitor, setMonitor] = useState<Monitoring | null>(null);
  const [message, setMessage] = useState("Entegrasyon merkezi yükleniyor…");
  const [oneTimeKey, setOneTimeKey] = useState("");
  const [systemForm, setSystemForm] = useState({ code: "", name: "", systemType: "PDKS" });
  const [deviceForm, setDeviceForm] = useState({ code: "", name: "", deviceType: "PDKS_TERMINAL", scopedCampId: "" });
  const [mappingForm, setMappingForm] = useState({ entityType: "EMPLOYEE", externalCode: "", internalEntityId: "" });
  const { ask, dialog } = useActionDialog();
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
    setMessage("Sistemler, cihazlar, veri eşlemeleri, işlem kuyruğu ve bağlantı sağlığı hazır.");
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
    const confirmed = await ask({
      title: "Cihaz anahtarı yenilensin mi?",
      description: `${device.code} cihazının mevcut anahtarı hemen geçersiz olacak. Yeni anahtar yalnız bir kez gösterilecek.`,
      confirmLabel: "Anahtarı yenile",
      tone: "danger",
    });
    if (!confirmed) return;
    const response = await authFetch(`/api/v1/integrations/devices/${device.id}/rotate-key`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: device.version }) });
    if (!response?.ok) return setMessage(await apiError(response, "Anahtar yenilenemedi."));
    const credential = await response.json() as Credential; setOneTimeKey(credential.plaintextKey); setMessage("Yeni cihaz anahtarı üretildi. Bu değer yalnız bu yanıtta gösterilir."); await loadSystemDetails(systemId);
  }

  async function createMapping(event: FormEvent) {
    event.preventDefault(); if (!systemId) return;
    const response = await authFetch("/api/v1/integrations/mappings", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ integrationSystemId: systemId, ...mappingForm }) });
    if (!response?.ok) return setMessage(await apiError(response, "Veri eşleştirmesi oluşturulamadı."));
    setMappingForm(x => ({ ...x, externalCode: "", internalEntityId: "" })); setMessage("Harici sistem kodu platform kaydıyla eşleştirildi."); await loadSystemDetails(systemId);
  }

  async function reprocess(row: Staging) {
    const response = await authFetch(`/api/v1/integrations/queue/${row.id}/reprocess`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ version: row.version }) });
    if (!response?.ok) return setMessage(await apiError(response, "Kayıt yeniden kuyruğa alınamadı."));
    setMessage("Kuyruk kaydı yeniden alındı durumuna geçirildi; arka plan işlemi tekrar deneyecek."); await loadCompanyData(companyId);
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
  async function apiError(response: Response | null, fallback: string) { if (!response) return fallback; const body = await response.json().catch(() => null) as { error?: { code?: string; message?: string } } | null; return body?.error?.code ? `${body.error.code}: ${body.error.message ?? fallback}` : body?.error?.message ?? fallback; }
  const dateTime = (value: string | null) => value ? new Date(value).toLocaleString("tr-TR") : "—";
  return <main className="page-shell">
    <PageHeader eyebrow="Entegrasyonlar" title="Bağlantı ve kuyruk merkezi" description="Harici sistemleri, cihaz kimliklerini, veri eşleştirmelerini ve işlem hatalarını tek yerden yönetin." status={message}/>

    <section className="stat-grid" aria-label="Entegrasyon özeti">
      <article className="stat-card"><span className="stat-icon"><Icon name="plug"/></span><span className="stat-copy"><strong>{monitor?.systems.length??systems.length}</strong><span>Bağlı sistem</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>{monitor?.totalBacklog??queue.filter(x=>x.status!=="PROCESSED").length}</strong><span>Bekleyen kayıt</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="bell"/></span><span className="stat-copy"><strong>{monitor?.totalErrors??queue.filter(x=>x.status.includes("ERROR")).length}</strong><span>Aktif hata</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="box"/></span><span className="stat-copy"><strong>{monitor?.totalDeadLetters??queue.filter(x=>x.status==="DEAD_LETTER").length}</strong><span>İşlenemeyen kayıt</span></span></article>
    </section>

    <section className="panel workspace-panel"><div className="workspace-copy"><span className="eyebrow dark">Çalışma kapsamı</span><h2>Şirket ve entegrasyon sistemi</h2><p>Cihazlar ve eşleştirmeler seçili sisteme; kuyruk ve sağlık bilgileri seçili şirkete göre gösterilir.</p></div><div className="workspace-select inline-form"><label className="field-label">Şirket<select value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Şirket seçin</option>{companies.map(x => <option key={x.id} value={x.id}>{x.code} · {x.name}</option>)}{companies.length === 0 && companyId ? <option value={companyId}>{companyId}</option> : null}</select></label><label className="field-label">Sistem<select value={systemId} onChange={e => setSystemId(e.target.value)}><option value="">Sistem seçin</option>{systems.map(x => <option key={x.id} value={x.id}>{x.code} · {systemTypeLabel(x.systemType)}</option>)}</select></label></div>{selectedSystem?<div className="selected-summary"><div className="selected-summary-copy"><strong>{selectedSystem.code} · {selectedSystem.name}</strong><small>{systemTypeLabel(selectedSystem.systemType)} · {selectedSystem.isActive?"Bağlantı aktif":"Bağlantı pasif"}</small></div><span className={`status-badge ${selectedSystem.isActive?"success":"danger"}`}>{selectedSystem.isActive?"Aktif":"Pasif"}</span></div>:null}</section>

    <div className="content-stack">
      <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Sistem tanımı</span><h2>Entegrasyon sistemleri</h2><p>Harici kaynak bağlantılarını seçili şirket kapsamında yönetin.</p></div><strong>{systems.length}</strong></div><OperationDisclosure title="Yeni entegrasyon sistemi ekle" description="Harici kaynağın türünü ve kurum içindeki adını tanımlayın."><form className="inline-form" onSubmit={createSystem}><label className="field-label">Sistem kodu<input value={systemForm.code} onChange={e => setSystemForm({ ...systemForm, code: e.target.value })} required /></label><label className="field-label">Sistem adı<input value={systemForm.name} onChange={e => setSystemForm({ ...systemForm, name: e.target.value })} required /></label><label className="field-label">Sistem türü<select value={systemForm.systemType} onChange={e => setSystemForm({ ...systemForm, systemType: e.target.value })}><option value="PDKS">Puantaj sistemi</option><option value="MEAL">Yemek sistemi</option><option value="ERP">ERP</option><option value="IMPORT">Dosya aktarımı</option></select></label><button className="primary-button" disabled={!companyId}>Sistemi kaydet</button></form></OperationDisclosure></section>

      {selectedSystem ? <>
        <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Cihaz kimlikleri</span><h2>{selectedSystem.code} cihazları</h2><p>Terminal veya servis kimliği oluşturun; cihaz anahtarı yalnız bir kez gösterilir.</p></div><strong>{devices.length}</strong></div>{oneTimeKey ? <div className="credential-notice" role="status"><div><strong>Tek seferlik cihaz anahtarı</strong><span>Bu değeri güvenli yere kaydedin; kapatıldıktan sonra tekrar gösterilemez.</span></div><code>{oneTimeKey}</code><div className="action-row"><button className="secondary-button button-success" type="button" onClick={() => navigator.clipboard.writeText(oneTimeKey)}>Anahtarı kopyala</button><button className="secondary-button" type="button" onClick={() => setOneTimeKey("")}>Gizle</button></div></div> : null}<OperationDisclosure title="Yeni cihaz kimliği oluştur" description="Cihaz anahtarı oluşturulduktan sonra yalnız bir kez gösterilir."><form className="inline-form" onSubmit={createDevice}><label className="field-label">Cihaz kodu<input value={deviceForm.code} onChange={e => setDeviceForm({ ...deviceForm, code: e.target.value })} required /></label><label className="field-label">Cihaz adı<input value={deviceForm.name} onChange={e => setDeviceForm({ ...deviceForm, name: e.target.value })} required /></label><label className="field-label">Cihaz türü<select value={deviceForm.deviceType} onChange={e => setDeviceForm({ ...deviceForm, deviceType: e.target.value })}><option value="PDKS_TERMINAL">Puantaj terminali</option><option value="MEAL_TERMINAL">Yemek terminali</option><option value="GENERIC">Genel cihaz</option></select></label><label className="field-label">Kamp kimliği (opsiyonel)<input value={deviceForm.scopedCampId} onChange={e => setDeviceForm({ ...deviceForm, scopedCampId: e.target.value })} /></label><button className="primary-button">Cihazı oluştur</button></form></OperationDisclosure><div className="table-wrap responsive-table-wrap" role="region" aria-label={`${selectedSystem.code} cihazları`} tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Cihaz</th><th>Tür</th><th>Kamp kapsamı</th><th>Son bağlantı</th><th>Son hata</th><th>İşlem</th></tr></thead><tbody>{devices.length?devices.map(x => <tr key={x.id}><td data-label="Cihaz"><strong>{x.code}</strong><small>{x.name}</small></td><td data-label="Tür">{deviceTypeLabel(x.deviceType)}</td><td data-label="Kamp kapsamı"><code>{x.scopedCampId ?? "—"}</code></td><td data-label="Son bağlantı">{dateTime(x.lastSeenAt)}</td><td data-label="Son hata">{x.lastErrorMessage ?? "—"}<small>{dateTime(x.lastErrorAt)}</small></td><td data-label="İşlem"><button className="secondary-button button-danger" type="button" onClick={() => rotateKey(x)}>Anahtarı yenile</button></td></tr>):<tr><td className="empty-row" colSpan={6}>Bu sisteme bağlı cihaz yok.</td></tr>}</tbody></table></div></section>

        <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Veri eşleştirme</span><h2>Harici kod → platform kaydı</h2><p>Harici sistem kodlarını platformdaki personel, kamp, proje veya maliyet kayıtlarıyla eşleştirin.</p></div><strong>{mappings.length}</strong></div><OperationDisclosure title="Yeni veri eşleştirmesi" description="Harici kodu ilgili platform kaydıyla ilişkilendirin."><form className="inline-form" onSubmit={createMapping}><label className="field-label">Kayıt türü<select value={mappingForm.entityType} onChange={e => setMappingForm({ ...mappingForm, entityType: e.target.value })}><option value="EMPLOYEE">Personel</option><option value="CAMP">Kamp</option><option value="MEAL_TYPE">Öğün</option><option value="PROJECT">Proje</option><option value="COST_CENTER">Maliyet merkezi</option><option value="COST_CATEGORY">Maliyet kategorisi</option></select></label><label className="field-label">Harici sistem kodu<input value={mappingForm.externalCode} onChange={e => setMappingForm({ ...mappingForm, externalCode: e.target.value })} required /></label><label className="field-label">Platform kayıt kimliği<input value={mappingForm.internalEntityId} onChange={e => setMappingForm({ ...mappingForm, internalEntityId: e.target.value })} required /></label><button className="primary-button">Eşleştirmeyi kaydet</button></form></OperationDisclosure><div className="table-wrap responsive-table-wrap" role="region" aria-label="Veri eşleştirmeleri" tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Tür</th><th>Harici kod</th><th>Platform kayıt kimliği</th><th>Durum</th></tr></thead><tbody>{mappings.length?mappings.map(x => <tr key={x.id}><td data-label="Tür">{entityLabel(x.entityType)}</td><td data-label="Harici kod"><strong>{x.externalCode}</strong></td><td data-label="Platform kaydı"><code>{x.internalEntityId}</code></td><td data-label="Durum"><span className={`status-badge ${x.isActive?"success":"danger"}`}>{x.isActive?"Aktif":"Pasif"}</span></td></tr>):<tr><td className="empty-row" colSpan={4}>Henüz veri eşleştirmesi yok.</td></tr>}</tbody></table></div></section>
      </> : null}

      <section className={`panel attention-panel ${(monitor?.totalErrors??0)>0?"danger":"success"}`}><div className="panel-heading"><div><span className="eyebrow dark">İşlem kuyruğu</span><h2>Gelen entegrasyon kayıtları</h2><p>Hatalı veya işlenemeyen kayıtları inceleyip yeniden işleme alabilirsiniz.</p></div><strong>{queue.length}</strong></div><div className="table-wrap" role="region" aria-label="Entegrasyon işlem kuyruğu" tabIndex={0}><table className="data-table"><thead><tr><th>Alınma</th><th>Olay</th><th>Harici kayıt</th><th>Durum</th><th>Deneme</th><th>Hata</th><th>Platform kaydı</th><th>İşlem</th></tr></thead><tbody>{queue.length === 0 ? <tr><td className="empty-row" colSpan={8}>İşlem kuyruğu boş.</td></tr> : queue.map(x => <tr key={x.id}><td>{dateTime(x.receivedAt)}</td><td>{x.eventType}</td><td><code>{x.externalEventId}</code></td><td><span className={`status-badge ${x.status==="PROCESSED"?"success":x.status.includes("ERROR")||x.status==="DEAD_LETTER"?"danger":"warning"}`}>{queueStatus(x.status)}</span></td><td>{x.attemptCount}<small>{x.nextRetryAt ? `Sonraki: ${dateTime(x.nextRetryAt)}` : ""}</small></td><td>{x.errorCode ?? "—"}<small>{x.errorMessage ?? ""}</small></td><td>{x.processedEntityType ? entityLabel(x.processedEntityType) : "—"}<small>{x.processedEntityId ?? ""}</small></td><td>{["BUSINESS_ERROR", "TECHNICAL_ERROR", "DEAD_LETTER"].includes(x.status) ? <button className="secondary-button button-success" type="button" onClick={() => reprocess(x)}>Yeniden işle</button> : "—"}</td></tr>)}</tbody></table></div></section>

      {monitor ? <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Bağlantı sağlığı</span><h2>Sistem ve cihaz durumu</h2><p>Son olay, işleme zamanı ve cihaz bağlantılarını sistem bazında izleyin.</p></div><strong>{monitor.systems.length}</strong></div><div className="system-health-grid">{monitor.systems.map(s => <article className={`health-card ${s.queue.technicalError||s.queue.deadLetter?"has-error":""}`} key={s.systemId}><div className="panel-heading"><div><span className="eyebrow dark">{systemTypeLabel(s.systemType)}</span><h2>{s.systemCode} · {s.systemName}</h2><p>Son olay: {dateTime(s.lastEventAt)} · Son işleme: {dateTime(s.lastProcessedAt)}</p></div><span className={`status-badge ${s.queue.technicalError||s.queue.deadLetter?"danger":"success"}`}>{s.queue.technicalError+s.queue.deadLetter?`${s.queue.technicalError+s.queue.deadLetter} hata`:"Sağlıklı"}</span></div><div className="detail-grid"><div className="detail-item"><span>İş kuralı hatası</span><strong>{s.queue.businessError}</strong></div><div className="detail-item"><span>Teknik hata</span><strong>{s.queue.technicalError}</strong></div><div className="detail-item"><span>İşlenemeyen</span><strong>{s.queue.deadLetter}</strong></div></div><div className="table-wrap responsive-table-wrap" role="region" aria-label={`${s.systemCode} cihaz sağlığı`} tabIndex={0}><table className="data-table responsive-table"><thead><tr><th>Cihaz</th><th>Sağlık</th><th>Son bağlantı</th><th>Son hata</th></tr></thead><tbody>{s.devices.length === 0 ? <tr><td className="empty-row" colSpan={4}>Bağlı cihaz yok.</td></tr> : s.devices.map(d => <tr key={d.deviceId}><td data-label="Cihaz"><strong>{d.deviceCode}</strong><small>{d.deviceName}</small></td><td data-label="Sağlık"><span className={`status-badge ${d.health==="HEALTHY"?"success":d.health==="STALE"?"warning":"danger"}`}>{healthLabel(d.health)}</span></td><td data-label="Son bağlantı">{dateTime(d.lastSeenAt)}</td><td data-label="Son hata">{d.lastErrorMessage ?? "—"}<small>{dateTime(d.lastErrorAt)}</small></td></tr>)}</tbody></table></div></article>)}</div></section> : null}
    </div>
    {dialog}
  </main>;
}
