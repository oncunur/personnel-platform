# Arayüz Tasarım Sistemi

Bu belge, Personel ve İdari İşler Platformu'nun ortak görsel dilini ve yeni ekranların uyması gereken temel kuralları tanımlar. Amaç; yoğun operasyon ekranlarında bilgiyi hızlı taranabilir, tutarlı ve erişilebilir şekilde sunmaktır.

## İlk denetim özeti

İlk arayüz denetiminde korumalı sayfaları bir arada tutan ortak bir uygulama çerçevesi bulunmadığı, ana sayfadaki modül bağlantılarının tek bir yoğun blokta toplandığı ve ekranlarda kullanılan çok sayıda sınıfın ortak stillerde karşılığının olmadığı görüldü. Özellikle form, panel, tablo, durum etiketi ve sayfa başlığı kalıpları ekranlar arasında farklılaşıyordu.

İlk dönüşüm aşağıdaki temel sorunları ele alır:

- Rol ve yetkilere göre sadeleşen, masaüstü ve mobil uyumlu ortak navigasyon
- Sayfa bağlamını gösteren sabit üst alan ve güvenli oturum göstergesi
- Kurumsal renk, yazı, boşluk, köşe ve gölge değişkenleri
- Panel, form, buton, tablo, durum etiketi ve boş durum kalıpları
- Ana ekranda özet metrikler ve öncelikli modüller
- Personel ekranında ayrı arama/filtreleme ve kayıt oluşturma akışları

## Tasarım ilkeleri

1. **Önce görev:** Dekorasyon, kullanıcının temel görevinden daha baskın olmamalıdır.
2. **Tek bakışta bağlam:** Her ekran; bulunduğu bölümü, ana eylemi ve mevcut durumu açıkça göstermelidir.
3. **Yoğun ama düzenli:** Kurumsal uygulamalarda veri yoğunluğu korunur; hiyerarşi boşluk, renk ve tipografiyle kurulur.
4. **Tutarlılık:** Aynı amaçtaki bileşen aynı görünüm ve davranışı kullanır.
5. **Erişilebilirlik:** Metin kontrastı, klavye odağı, dokunma alanları ve hareket azaltma tercihi tasarımın parçasıdır.

## Temel değişkenler

Değişkenler `frontend/app/globals.css` içinde tanımlıdır.

| Grup | Kullanım |
| --- | --- |
| `--brand-*` | Navigasyon, güçlü başlıklar ve kurumsal zemin |
| `--accent-*` | Ana eylem, seçili durum ve vurgu |
| `--surface*` | Sayfa, kart ve yükseltilmiş yüzeyler |
| `--ink-*` | Ana, ikincil ve soluk metin hiyerarşisi |
| `--success`, `--warning`, `--danger`, `--info` | Anlamsal durumlar |
| `--radius-*` | Kontrol, kart ve büyük yüzey köşeleri |
| `--shadow-*` | Yüzey seviyeleri; bilgi hiyerarşisini desteklemek için sınırlı kullanılır |

## Uygulama çerçevesi

`AppFrame`, giriş ve tanıtım ekranları dışındaki sayfaları ortak bir çerçeveye alır.

- Sol navigasyon yetkisi olmayan modülleri göstermez.
- Etkin menü öğesi hem renk hem yüzeyle ayırt edilir.
- Üst alan sayfa bağlamını ve oturum durumunu gösterir.
- 900 piksel altında navigasyon açılır mobil menüye dönüşür.
- İçerik genişliği okunabilirlik için sınırlandırılır; tablolar gerektiğinde yatay kayar.

## Bileşen kuralları

### Sayfa başlığı

Her operasyon ekranı ortak `PageHeader` bileşenini ve `page-header` kalıbını kullanır. Sol tarafta kısa açıklama, sağ tarafta en fazla bir birincil eylem bulunur. İkincil eylemler panel içine veya taşma menüsüne alınır. Dinamik işlem mesajı başlığın hemen altında, ekran okuyucularına duyurulan tek bir durum satırında gösterilir.

### Çalışma kapsamı

Şirket, dönem veya personel gibi ekranın geri kalanını etkileyen ana seçimler `workspace-panel` ya da `selection-bar` kalıbında gösterilir. Kapsam seçimi form içindeki sıradan bir alan gibi saklanmaz; seçimin etkilediği kayıtlar kısa bir metinle açıklanır.

### Form yüzeyi

Listeyle aynı panelde bulunan oluşturma veya güncelleme formu `form-surface` ile ayrılır. Böylece form alanları ile mevcut kayıtlar birbirine karışmaz. Formun kısa başlığı yapılan işlemi, yardımcı metni ise işlemin etkisini açıklar.

### Butonlar

- `primary-button`: Kullanıcının ekrandaki ana görevi
- `secondary-button`: Yardımcı veya güvenli ikincil görev
- `button-danger`: Geri alınması zor işlem
- `icon-button`: Yalnız ikon içeren, mutlaka erişilebilir adı bulunan kontrol

Aynı bölümde birden fazla birincil buton kullanılmaz. Devre dışı durum yalnız renkle anlatılmaz; kontrol davranış olarak da devre dışıdır.

### Formlar

Alanlar `field` ve `field-label` ile gruplanır. Etiket her zaman görünür kalır; yer tutucu metin etiket yerine geçmez. Uzun formlar anlamlı panellere bölünür ve ana gönderim eylemi formun sonunda sağa hizalanır.

### Tablolar

Tablolar liste taraması için kullanılır. Birincil kimlik ilk sütunda vurgulanır, durumlar `status-badge` ile gösterilir, tarih ve sayı biçimleri Türkçe yerel ayarına göre sunulur. Mobilde tablo kırılmaz; kontrollü yatay kaydırma sağlanır.

### Durumlar ve geri bildirim

- Başarılı, bekleyen, hatalı ve bilgi durumları anlamsal renklerle gösterilir.
- Renk tek başına anlam taşımaz; metin etiketi veya ikon eşlik eder.
- Yükleme sırasında düzenin sıçramasını azaltan iskelet yüzeyler kullanılır.
- Boş durum; ne olmadığını ve kullanıcının sonraki adımını açıklar.

### Onay kutusu ve dikkat panelleri

Karar bekleyen kayıtlar normal geçmiş listelerinden ayrı bir panelde gösterilir. Onay eylemi `button-success`, red veya geri alma eylemi `button-danger` kullanır; her iki eylem de metinle açıkça adlandırılır. `attention-panel` sol kenar vurgusuyla bilgilendirme, yaklaşan süre, kritik gecikme veya başarılı durum varyantı alabilir. Renk hiçbir zaman tek başına karar veya belge durumunu anlatmaz; durum etiketi ve açıklama eşlik eder.

## Erişilebilirlik tabanı

- Klavye odağı `:focus-visible` ile belirgindir.
- Etkileşimli kontroller en az 40 piksel hedef alanına sahiptir.
- Metin ve arka plan kontrastı WCAG AA hedefiyle seçilir.
- Yalnız ikon kullanan kontroller `aria-label` taşır.
- `prefers-reduced-motion` etkin olduğunda animasyon ve geçişler kaldırılır.
- Mobil menü açıkken arka plan ayrı bir kapatma yüzeyi sunar.

## Ekran taşıma sırası

Yeni ekranlar doğrudan bu kalıplarla oluşturulur. Mevcut ekranlar aşağıdaki sırayla taşınır:

1. En sık kullanılan insan kaynakları ekranları
2. İzin, puantaj ve bordro akışları
3. İdari operasyon ekranları
4. Raporlama, entegrasyon ve sistem yönetimi
5. Son erişilebilirlik, mobil uyum ve metin tutarlılığı turu

Her taşıma; masaüstü ve mobil görünüm, klavye kullanımı, boş/yükleme/hata durumları ve yetkiye göre görünürlük kontrolleriyle tamamlanır.
