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
  const [setupMessage, setSetupMessage] = useState<string | null>(null);
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
      if (!response.ok) {
        const body = (await response.json().catch(() => ({}))) as ErrorResponse;
        setError(body.error?.code === "AUTH_ACCOUNT_LOCKED" ? "Çok sayıda başarısız deneme nedeniyle hesabınız geçici olarak kilitlendi. Lütfen bir süre sonra yeniden deneyin." : body.error?.message ?? "Kullanıcı adı veya parola doğrulanamadı.");
        return;
      }
      const body = (await response.json()) as AuthResponse | MfaRequiredResponse;
      if (isAuthResponse(body)) { completeSession(body); return; }
      setMfa(body); setPassword(""); setCode(""); setSetupMessage(null);
    } catch { setError("Giriş hizmetine şu anda ulaşılamıyor. Lütfen kısa bir süre sonra yeniden deneyin."); }
    finally { setSubmitting(false); }
  }

  async function handleMfa(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!mfa) return; setError(null); setSubmitting(true);
    try {
      const response = await fetch(`${apiBase}/api/v1/auth/mfa/complete`, { method: "POST", credentials: "include", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ challengeToken: mfa.challengeToken, code }) });
      if (!response.ok) {
        const body = (await response.json().catch(() => ({}))) as ErrorResponse;
        setError(body.error?.code === "AUTH_MFA_CHALLENGE_STALE" ? "Kurulum bilgileri yenilendi. Farklı hesapla giriş yap seçeneğiyle yeniden başlayın." : "Doğrulama kodu geçersiz veya süresi dolmuş. Uygulamadaki güncel kodla yeniden deneyin.");
        return;
      }
      completeSession((await response.json()) as AuthResponse);
    } catch { setError("Doğrulama hizmetine şu anda ulaşılamıyor. Lütfen kısa bir süre sonra yeniden deneyin."); }
    finally { setSubmitting(false); }
  }

  async function copyEnrollmentSecret() {
    if (!mfa?.enrollmentSecret) return;
    try {
      await navigator.clipboard.writeText(mfa.enrollmentSecret);
      setSetupMessage("Kurulum anahtarı kopyalandı.");
    } catch {
      setSetupMessage("Otomatik kopyalama kullanılamadı. Anahtarı seçip kopyalayabilirsiniz.");
    }
  }

  function restartLogin() {
    setMfa(null);
    setCode("");
    setError(null);
    setSetupMessage(null);
  }

  return <main className="shell auth-shell"><section className={`auth-card${mfa?.enrollmentRequired ? " auth-card-wide" : ""}`} aria-labelledby="auth-title">
    <div className="auth-brand"><span className="auth-logo" aria-hidden="true">Pİ</span><span className="auth-brand-copy"><strong>Personel &amp; İdari İşler</strong><small>Güvenli çalışma alanı</small></span></div>
    <a className="back" href="/">← Ana sayfaya dön</a><span className="eyebrow dark">{mfa ? "Hesap güvenliği" : "Güvenli erişim"}</span><h1 id="auth-title">{mfa ? (mfa.enrollmentRequired ? "Ek doğrulamayı kurun" : "Hesabınızı doğrulayın") : "Giriş yap"}</h1>
    <p className="muted">{mfa ? (mfa.enrollmentRequired ? "Hesabınızı korumak için doğrulama uygulamanızı bir kez bağlayın." : `${mfa.username} hesabıyla devam etmek için uygulamanızdaki kodu girin.`) : "Çalışma alanınıza kullanıcı bilgilerinizle güvenle erişin."}</p>
    {!mfa ? <form className="auth-form" onSubmit={handleSubmit}>
      <label>Kullanıcı adı<input autoFocus autoComplete="username" value={username} onChange={e => setUsername(e.target.value)} maxLength={100} required /></label>
      <label>Parola<input type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} maxLength={512} required /></label>
      {error ? <div className="error-box" role="alert">{error}</div> : null}
      <button className="primary-button" type="submit" disabled={submitting}>{submitting ? "Giriş yapılıyor…" : "Giriş yap"}</button>
    </form> : <form className="auth-form" onSubmit={handleMfa}>
      {mfa.enrollmentRequired ? <div className="auth-setup-panel">
        <div className="auth-setup-heading"><strong>3 adımda tamamlayın</strong><span>Bu anahtar yalnızca ilk kurulumda gösterilir.</span></div>
        <ol className="auth-step-list">
          <li><span aria-hidden="true">1</span><div><strong>Doğrulama uygulamanızı açın</strong><small>Microsoft Authenticator, Google Authenticator veya kullandığınız eşdeğer uygulamayı açın.</small></div></li>
          <li><span aria-hidden="true">2</span><div><strong>Hesabı uygulamanıza ekleyin</strong><small>Mobil cihazdaysanız doğrudan açabilir veya aşağıdaki anahtarı uygulamaya ekleyebilirsiniz.</small></div></li>
        </ol>
        {mfa.enrollmentSecret ? <div className="auth-setup-key"><span>Kurulum anahtarı</span><code className="breakable-code">{mfa.enrollmentSecret}</code></div> : <div className="error-box" role="alert">Kurulum anahtarı alınamadı. Başa dönüp yeniden deneyin.</div>}
        <div className="auth-inline-actions">
          <button className="secondary-button" type="button" onClick={() => void copyEnrollmentSecret()} disabled={!mfa.enrollmentSecret}>Anahtarı kopyala</button>
          {mfa.otpAuthUri ? <a className="secondary-button" href={mfa.otpAuthUri}>Uygulamada aç</a> : null}
        </div>
        {setupMessage ? <p className="auth-setup-status" role="status" aria-live="polite">{setupMessage}</p> : null}
        <ol className="auth-step-list auth-step-list-final" start={3}>
          <li><span aria-hidden="true">3</span><div><strong>6 haneli kodu girin</strong><small>Uygulamanızın oluşturduğu güncel kodu aşağıdaki alana yazın.</small></div></li>
        </ol>
      </div> : <div className="auth-code-notice"><strong>Doğrulama uygulamanızı kontrol edin</strong><span>Uygulamada görünen güncel 6 haneli kodu kullanın.</span></div>}
      <label>6 haneli doğrulama kodu<input autoFocus={!mfa.enrollmentRequired} inputMode="numeric" pattern="[0-9]{6}" autoComplete="one-time-code" value={code} onChange={e => { setCode(e.target.value.replace(/\D/g, "").slice(0, 6)); setError(null); }} minLength={6} maxLength={6} placeholder="000000" aria-describedby="auth-code-help" required /></label>
      <small className="auth-code-help" id="auth-code-help">Kod her 30 saniyede bir yenilenebilir ve yalnızca bir kez kullanılabilir.</small>
      {error ? <div className="error-box" role="alert">{error}</div> : null}
      <button className="primary-button" type="submit" disabled={submitting || code.length !== 6}>{submitting ? "Doğrulanıyor…" : mfa.enrollmentRequired ? "Kurulumu tamamla ve giriş yap" : "Doğrula ve giriş yap"}</button>
      <button className="secondary-button" type="button" onClick={restartLogin}>Farklı hesapla giriş yap</button>
    </form>}
    <p className="auth-hint">Yetkili hesaplarda ek doğrulama zorunludur. Giriş bilgileriniz ve kurulum anahtarınız kimseyle paylaşılmamalıdır.</p>
  </section></main>;
}
