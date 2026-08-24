"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useParams } from "next/navigation";
import { useActionDialog } from "../../components/ActionDialog";
import { Icon } from "../../components/Icon";
import { PageHeader } from "../../components/PageHeader";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type Permission = { code: string };
type Me = { permissions: Permission[] };
type AuthResponse = { accessToken: string; accessTokenExpiresAt: string };
type DocumentDetail = {
  id: string; employeeId: string; documentTypeId: string; documentTypeCode: string; documentTypeName: string;
  documentNumber: string | null; issueDate: string | null; validFrom: string | null; validUntil: string | null;
  status: string; fileName: string | null; contentType: string | null; fileSizeBytes: number | null;
  replacesDocumentId: string | null; version: number;
};
type HistoryRow = {
  id: string; employeeDocumentId: string; action: string; fromStatus: string | null; toStatus: string;
  changedBy: string; changedAt: string; reason: string | null;
};

const documentStatuses: Record<string, { label: string; tone: string }> = {
  ACTIVE: { label: "Aktif", tone: "success" }, VALID: { label: "Geçerli", tone: "success" },
  EXPIRING: { label: "Süresi yaklaşıyor", tone: "warning" }, EXPIRED: { label: "Süresi doldu", tone: "danger" },
  ARCHIVED: { label: "Arşivlendi", tone: "" }, CANCELLED: { label: "İptal edildi", tone: "danger" }, REPLACED: { label: "Yenilendi", tone: "" },
};
const actionLabels: Record<string, string> = { CREATED: "Oluşturuldu", RENEWED: "Yenilendi", CANCELLED: "İptal edildi", ARCHIVED: "Arşivlendi", UPDATED: "Güncellendi" };
function statusOf(value: string) { return documentStatuses[value] ?? { label: value, tone: "" }; }
function formatDate(value: string | null) { return value ? new Date(value).toLocaleDateString("tr-TR") : "—"; }

export default function DocumentDetailPage() {
  const params = useParams<{ id: string }>();
  const documentId = params.id;
  const [me, setMe] = useState<Me | null>(null);
  const [document, setDocument] = useState<DocumentDetail | null>(null);
  const [history, setHistory] = useState<HistoryRow[]>([]);
  const [message, setMessage] = useState("Belge yükleniyor…");
  const [busy, setBusy] = useState(false);
  const { ask, dialog } = useActionDialog();
  const permissions = useMemo(() => new Set(me?.permissions.map(x => x.code) ?? []), [me]);

  useEffect(() => { void initialize(); }, [documentId]);

  async function initialize() {
    const current = await json<Me>("/api/v1/auth/me");
    if (!current) { window.location.replace("/login"); return; }
    setMe(current);
    const [detail, historyRows] = await Promise.all([
      json<DocumentDetail>(`/api/v1/documents/employee-documents/${documentId}`),
      json<HistoryRow[]>(`/api/v1/documents/employee-documents/${documentId}/history`),
    ]);
    if (!detail) { setMessage("Belge bulunamadı veya erişim yok."); return; }
    setDocument(detail); setHistory(historyRows ?? []); setMessage("Belge güncel.");
  }

  async function renew(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!document) return; setBusy(true);
    try {
      const form = new FormData(event.currentTarget);
      const response = await authFetch(`/api/v1/documents/employee-documents/${document.id}/renew`, { method: "POST", body: form });
      if (!response?.ok) {
        const error = response ? await response.json().catch(() => null) as { error?: { message?: string } } | null : null;
        setMessage(error?.error?.message ?? "Belge yenilenemedi."); return;
      }
      const renewed = await response.json() as DocumentDetail;
      window.location.replace(`/documents/${renewed.id}`);
    } finally { setBusy(false); }
  }

  async function cancelDocument() {
    if (!document) return;
    const confirmed = await ask({
      title: "Belge iptal edilsin mi?",
      description: `${document.documentTypeName} kaydı iptal durumuna alınacak ve geçmişte izlenmeye devam edecek.`,
      confirmLabel: "Belgeyi iptal et",
      tone: "danger",
    });
    if (!confirmed) return;
    setBusy(true);
    try {
      const response = await authFetch(`/api/v1/documents/employee-documents/${document.id}/cancel`, { method: "POST" });
      if (!response?.ok) { setMessage("Belge iptal edilemedi."); return; }
      setDocument(await response.json() as DocumentDetail);
      setHistory((await json<HistoryRow[]>(`/api/v1/documents/employee-documents/${document.id}/history`)) ?? []);
      setMessage("Belge iptal edildi.");
    } finally { setBusy(false); }
  }

  async function openFile() {
    if (!document) return; setBusy(true);
    try {
      const response = await authFetch(`/api/v1/documents/employee-documents/${document.id}/file`);
      if (!response?.ok) { setMessage("Dosya açılamadı."); return; }
      const blob = await response.blob(); const url = URL.createObjectURL(blob);
      window.open(url, "_blank", "noopener,noreferrer"); window.setTimeout(() => URL.revokeObjectURL(url), 60000);
    } finally { setBusy(false); }
  }

  async function json<T>(path: string): Promise<T | null> { const response = await authFetch(path); return response?.ok ? await response.json() as T : null; }
  async function authFetch(path: string, init?: RequestInit): Promise<Response | null> {
    let token = sessionStorage.getItem("pp_access_token") ?? await refresh(); if (!token) return null;
    let response = await fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
    if (response.status !== 401) return response; token = await refresh(); if (!token) return response;
    return fetch(`${apiBase}${path}`, { ...init, headers: { ...(init?.headers ?? {}), Authorization: `Bearer ${token}` }, credentials: "include" });
  }
  async function refresh(): Promise<string | null> {
    try { const response = await fetch(`${apiBase}/api/v1/auth/refresh`, { method: "POST", credentials: "include" }); if (!response.ok) return null; const body = await response.json() as AuthResponse; sessionStorage.setItem("pp_access_token", body.accessToken); return body.accessToken; } catch { return null; }
  }

  if (!document) return <main className="page-shell"><PageHeader eyebrow="Belge detayı" title="Belge kaydı" description="Belge bilgileri yükleniyor veya erişim kontrol ediliyor." status={message} actions={<a className="secondary-button" href="/documents">Belge merkezine dön</a>}/></main>;
  const terminal = document.status === "ARCHIVED" || document.status === "CANCELLED";
  const status = statusOf(document.status);

  return <main className="page-shell">
    <PageHeader eyebrow="Belge detayı" title={document.documentTypeName} description={`${document.documentTypeCode} · ${document.documentNumber ?? "Belge numarası yok"}`} status={message} actions={<><a className="secondary-button" href="/documents">Belge merkezi</a><a className="secondary-button" href={`/personnel/${document.employeeId}`}>Personel 360</a>{document.fileName && permissions.has("documents.file.view") ? <button className="secondary-button" type="button" disabled={busy} onClick={() => void openFile()}><Icon name="box" size={17}/>Dosyayı aç</button> : null}{!terminal && permissions.has("documents.employee.cancel") ? <button className="secondary-button button-danger" type="button" disabled={busy} onClick={() => void cancelDocument()}>Belgeyi iptal et</button> : null}</>}/>

    <section className="stat-grid" aria-label="Belge özeti">
      <article className="stat-card"><span className="stat-icon"><Icon name="box"/></span><span className="stat-copy"><strong><span className={`status-badge ${status.tone}`}>{status.label}</span></strong><span>Belge durumu</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="calendar"/></span><span className="stat-copy"><strong>{formatDate(document.validUntil)}</strong><span>Geçerlilik bitişi</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="workflow"/></span><span className="stat-copy"><strong>v{document.version}</strong><span>Kayıt sürümü</span></span></article>
      <article className="stat-card"><span className="stat-icon"><Icon name="chart"/></span><span className="stat-copy"><strong>{history.length}</strong><span>Geçmiş hareketi</span></span></article>
    </section>

    <div className="content-stack">
      <section className="security-grid">
      <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Mevcut kayıt</span><h2>Belge bilgileri</h2><p>Belgenin kimlik, geçerlilik ve dosya ayrıntıları.</p></div></div><div className="detail-grid"><Item label="Belge no" value={document.documentNumber}/><Item label="Düzenlenme" value={formatDate(document.issueDate)}/><Item label="Geçerlilik başlangıcı" value={formatDate(document.validFrom)}/><Item label="Geçerlilik bitişi" value={formatDate(document.validUntil)}/><Item label="Dosya" value={document.fileName}/><Item label="Boyut" value={document.fileSizeBytes ? `${Math.round(document.fileSizeBytes / 1024)} KB` : null}/></div>{document.replacesDocumentId ? <div className="selected-summary"><span className="selected-summary-copy"><strong>Bu kayıt önceki bir belgenin yenilenmiş sürümüdür.</strong><small>Eski kayıt numarası: {document.replacesDocumentId.slice(0, 8)}</small></span></div> : null}</article>

      {!terminal && permissions.has("documents.employee.renew") ? <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Yeni sürüm</span><h2>Belgeyi yenile</h2><p>Yeni bilgilerle bir sürüm oluşturulur; mevcut kayıt geçmişte korunur.</p></div></div><form className="stack" onSubmit={renew}>
        <label className="field-label">Belge no<input name="documentNumber" defaultValue={document.documentNumber ?? ""}/></label>
        <label className="field-label">Düzenlenme<input name="issueDate" type="date"/></label>
        <label className="field-label">Geçerlilik başlangıcı<input name="validFrom" type="date"/></label>
        <label className="field-label">Geçerlilik bitişi<input name="validUntil" type="date"/></label>
        <label className="field-label">Düzenleyen kurum<input name="issuingAuthority"/></label>
        <label className="field-label">Ülke kodu<input name="countryCode" maxLength={3} defaultValue="TR"/></label>
        <label className="field-label">Yeni dosya<input name="file" type="file" accept="application/pdf,image/jpeg,image/png"/></label>
        <button className="primary-button" disabled={busy}><Icon name="plus" size={17}/>Yeni sürümü oluştur</button>
      </form></article> : null}
      </section>

      <section className="panel"><div className="panel-heading"><div><span className="eyebrow dark">Sürüm ve işlem izi</span><h2>Belge geçmişi</h2><p>Belge üzerinde yapılan tüm durum ve sürüm değişiklikleri.</p></div><strong>{history.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>İşlem</th><th>Durum değişimi</th><th>Neden</th></tr></thead><tbody>{history.length === 0 ? <tr><td className="empty-row" colSpan={4}>Bu belge için geçmiş hareketi bulunmuyor.</td></tr> : history.map(x => <tr key={x.id}><td>{new Date(x.changedAt).toLocaleString("tr-TR")}<small>{x.changedBy}</small></td><td>{actionLabels[x.action] ?? x.action}</td><td>{x.fromStatus ? `${statusOf(x.fromStatus).label} → ${statusOf(x.toStatus).label}` : statusOf(x.toStatus).label}</td><td>{x.reason ?? "—"}</td></tr>)}</tbody></table></div></section>
    </div>
    {dialog}
  </main>;
}

function Item({ label, value }: { label: string; value?: string | null }) { return <div className="detail-item"><span>{label}</span><strong>{value || "—"}</strong></div>; }
