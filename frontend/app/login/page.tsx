"use client";

import { FormEvent, useState } from "react";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";

type AuthResponse = { userId: string; username: string; email: string | null; accessToken: string; accessTokenExpiresAt: string };
type MfaRequiredResponse = { userId: string; username: string; challengeToken: string; purpose: string; enrollmentRequired: boolean; enrollmentSecret: string | null; otpAuthUri: string | null; expiresAt: string };
type ErrorResponse = { error?: { code?: string; message?: string } };

function isAuthResponse(value: AuthResponse | MfaRequiredResponse): value is AuthResponse { return "accessToken" in value; }

export default function LoginPage() {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [mfa, setMfa] = useState<MfaRequiredResponse | null>(null);
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  function completeSession(body: AuthResponse) {
    sessionStorage.setItem("pp_access_token", body.accessToken);
    sessionStorage.setItem("pp_access_token_expires_at", body.accessTokenExpiresAt);
    window.location.assign("/dashboard");
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(null); setSubmitting(true);
    try {
      const response = await fetch(`${apiBase}/api/v1/auth/login`, { method: "POST", credentials: "include", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ username, password }) });
      if (!response.ok) { const body = (await response.json().catch(() => ({}))) as ErrorResponse; setError(body.error?.message ?? "Giriş başarısız."); return; }
      const body = (await response.json()) as AuthResponse | MfaRequiredResponse;
      if (isAuthResponse(body)) { completeSession(body); return; }
      setMfa(body); setPassword("");
    } catch { setError("API bağlantısı kurulamadı. Servislerin çalıştığını kontrol edin."); }
    finally { setSubmitting(false); }
  }

  async function handleMfa(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!mfa) return; setError(null); setSubmitting(true);
    try {
      const response = await fetch(`${apiBase}/api/v1/auth/mfa/complete`, { method: "POST", credentials: "include", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ challengeToken: mfa.challengeToken, code }) });
      if (!response.ok) { const body = (await response.json().catch(() => ({}))) as ErrorResponse; setError(body.error?.message ?? "MFA doğrulaması başarısız."); return; }
      completeSession((await response.json()) as AuthResponse);
    } catch { setError("MFA doğrulaması için API bağlantısı kurulamadı."); }
    finally { setSubmitting(false); }
  }

  return <main className="shell auth-shell"><section className="auth-card">
    <a className="back" href="/">← Ana sayfa</a><span className="eyebrow dark">Güvenli erişim</span><h1>{mfa ? "Çok faktörlü doğrulama" : "Giriş yap"}</h1>
    <p className="muted">Personel ve İdari İşler Platformu çalışma alanınıza erişin.</p>
    {!mfa ? <form className="auth-form" onSubmit={handleSubmit}>
      <label>Kullanıcı adı<input autoComplete="username" value={username} onChange={e => setUsername(e.target.value)} maxLength={100} required /></label>
      <label>Parola<input type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} maxLength={512} required /></label>
      {error ? <div className="error-box" role="alert">{error}</div> : null}
      <button className="primary-button" type="submit" disabled={submitting}>{submitting ? "Giriş yapılıyor…" : "Giriş yap"}</button>
    </form> : <form className="auth-form" onSubmit={handleMfa}>
      {mfa.enrollmentRequired ? <div className="panel"><strong>Doğrulama uygulaması kurulumu gerekli</strong><p className="muted">Doğrulama uygulamanıza aşağıdaki kurulum anahtarını ekleyin. Bu değer yalnız kurulum sırasında gösterilir.</p><code className="breakable-code">{mfa.enrollmentSecret}</code>{mfa.otpAuthUri ? <details className="technical-details"><summary>Kurulum bağlantısını görüntüle</summary><code className="breakable-code">{mfa.otpAuthUri}</code></details> : null}</div> : <p className="muted">Doğrulama uygulamanızdaki 6 haneli kodu girin.</p>}
      <label>Doğrulama kodu<input inputMode="numeric" autoComplete="one-time-code" value={code} onChange={e => setCode(e.target.value.replace(/\D/g, "").slice(0, 6))} minLength={6} maxLength={6} required /></label>
      {error ? <div className="error-box" role="alert">{error}</div> : null}
      <button className="primary-button" type="submit" disabled={submitting || code.length !== 6}>{submitting ? "Doğrulanıyor…" : mfa.enrollmentRequired ? "MFA'yı etkinleştir ve giriş yap" : "Doğrula ve giriş yap"}</button>
      <button className="secondary-button" type="button" onClick={() => { setMfa(null); setCode(""); setError(null); }}>Başa dön</button>
    </form>}
    <p className="auth-hint">Kritik yetkilere sahip hesaplarda ek doğrulama zorunludur. Her doğrulama kodu yalnızca bir kez kullanılabilir.</p>
  </section></main>;
}
