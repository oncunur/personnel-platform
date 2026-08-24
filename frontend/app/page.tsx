import { Icon, IconName } from "./components/Icon";

const modules: { title: string; description: string; icon: IconName }[] = [
  { title: "Personel ve özlük", description: "Personel kartları, dijital belgeler ve organizasyon bağlantıları.", icon: "people" },
  { title: "İzin ve puantaj", description: "İzin talepleri, günlük devam, vardiya ve fazla mesai süreçleri.", icon: "calendar" },
  { title: "Bordro ve maliyet", description: "Bordro dönemleri, maliyet defteri, proje dağılımları ve ERP aktarımı.", icon: "wallet" },
  { title: "İdari operasyonlar", description: "Kamp, yemek, araç, demirbaş, stok ve sözleşme takibi.", icon: "building" },
];

export default function Home() {
  return (
    <main className="shell">
      <section className="overview-banner public-overview">
        <div className="overview-content">
          <span className="overview-kicker"><Icon name="building" size={15}/>Kurumsal operasyon merkezi</span>
          <h1>Personel ve idari süreçler tek, düzenli bir çalışma alanında.</h1>
          <p>İnsan kaynaklarından saha operasyonlarına, bordrodan maliyet ve entegrasyonlara kadar günlük işleri güvenli ve izlenebilir biçimde yönetin.</p>
          <div className="actions action-row"><a className="primary" href="/login">Platforma giriş yap <Icon name="arrow" size={17}/></a><a className="ghost-link" href="/api-health">Sistem durumunu kontrol et</a></div>
        </div>
        <div className="overview-profile"><span>Tek merkezden yönetim</span><strong>İnsan · Operasyon · Finans</strong><small>Yetkiye dayalı erişim, değiştirilemeyen işlem geçmişi ve ortak raporlama.</small><div className="role-chips"><span className="role-chip">İnsan Kaynakları</span><span className="role-chip">İdari İşler</span><span className="role-chip">Finans</span></div></div>
      </section>

      <section className="module-grid public-module-grid" aria-label="Platform yetenekleri">
        {modules.map((module) => (
          <article className="module-card" key={module.title}>
            <span className="module-icon"><Icon name={module.icon}/></span>
            <span className="module-card-copy"><strong>{module.title}</strong><span>{module.description}</span><small>Platform modülü</small></span>
          </article>
        ))}
      </section>
    </main>
  );
}
