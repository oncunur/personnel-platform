"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { useParams } from "next/navigation";

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

export default function DocumentDetailPage() {
  const params = useParams<{ id: string }>();
  const documentId = params.id;
  const [me, setMe] = useState<Me | null>(null);
  const [document, setDocument] = useState<DocumentDetail | null>(null);
  const [history, setHistory] = useState<HistoryRow[]>([]);
  const [message, setMessage] = useState("Belge yükleniyor…");
  const [busy, setBusy] = useState(false);
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
    if (!document || !window.confirm("Bu belge kaydını iptal etmek istiyor musunuz?")) return;
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

  if (!document) return <main className="shell"><a className="back" href="/documents">← Belge Merkezi</a><section className="panel"><p>{message}</p></section></main>;
  const terminal = document.status === "ARCHIVED" || document.status === "CANCELLED";

  return <main className="shell">
    <a className="back" href="/documents">← Belge Merkezi</a>
    <section className="hero compact"><span className="eyebrow">BELGE DETAYI</span><h1>{document.documentTypeName}</h1><p>{message}</p><div className="session-summary"><strong>{document.documentTypeCode}</strong><span>{document.documentNumber ?? "Belge no yok"}</span><span>{document.status}</span><span>v{document.version}</span></div><div className="actions action-row"><a className="primary" href={`/personnel/${document.employeeId}`}>Personel 360</a>{document.fileName && permissions.has("documents.file.view") ? <button className="secondary-button" disabled={busy} onClick={() => void openFile()}>Dosyayı Aç</button> : null}{!terminal && permissions.has("documents.employee.cancel") ? <button className="secondary-button" disabled={busy} onClick={() => void cancelDocument()}>Belgeyi İptal Et</button> : null}</div></section>

    <section className="security-grid">
      <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">MEVCUT KAYIT</span><h2>Belge Bilgileri</h2></div></div><div className="detail-grid"><Item label="Belge No" value={document.documentNumber}/><Item label="Düzenlenme" value={document.issueDate}/><Item label="Geçerlilik Başlangıç" value={document.validFrom}/><Item label="Geçerlilik Bitiş" value={document.validUntil}/><Item label="Dosya" value={document.fileName}/><Item label="Boyut" value={document.fileSizeBytes ? `${Math.round(document.fileSizeBytes / 1024)} KB` : null}/></div></article>

      {!terminal && permissions.has("documents.employee.renew") ? <article className="panel"><div className="panel-heading"><div><span className="eyebrow dark">YENİ VERSİYON</span><h2>Belgeyi Yenile</h2></div></div><form className="auth-form" onSubmit={renew}>
        <label>Belge No<input name="documentNumber" defaultValue={document.documentNumber ?? ""}/></label>
        <label>Düzenlenme<input name="issueDate" type="date"/></label>
        <label>Geçerlilik Başlangıç<input name="validFrom" type="date"/></label>
        <label>Geçerlilik Bitiş<input name="validUntil" type="date"/></label>
        <label>Düzenleyen Kurum<input name="issuingAuthority"/></label>
        <label>Ülke<input name="countryCode" maxLength={3}/></label>
        <label>Yeni Dosya<input name="file" type="file" accept="application/pdf,image/jpeg,image/png"/></label>
        <button className="primary-button" disabled={busy}>Yeni Versiyonu Oluştur</button>
      </form></article> : null}
    </section>

    <section className="panel audit-panel"><div className="panel-heading"><div><span className="eyebrow dark">VERSİYON / İŞLEM İZİ</span><h2>Belge Geçmişi</h2></div><strong>{history.length}</strong></div><div className="table-wrap"><table className="data-table"><thead><tr><th>Tarih</th><th>İşlem</th><th>Durum</th><th>Neden</th></tr></thead><tbody>{history.length === 0 ? <tr><td colSpan={4}>Henüz history kaydı yok.</td></tr> : history.map(x => <tr key={x.id}><td>{new Date(x.changedAt).toLocaleString("tr-TR")}</td><td>{x.action}</td><td>{x.fromStatus ? `${x.fromStatus} → ${x.toStatus}` : x.toStatus}</td><td>{x.reason ?? "—"}</td></tr>)}</tbody></table></div></section>
  </main>;
}

function Item({ label, value }: { label: string; value?: string | null }) { return <div className="detail-item"><small>{label}</small><strong>{value || "—"}</strong></div>; }
