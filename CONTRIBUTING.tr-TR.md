<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Afişi" title="optimizerDuck"/></a>

[English](CONTRIBUTING.md) | [日本語](CONTRIBUTING.ja-JP.md) | **Türkçe**

[Giriş](#giriş) • [Başlangıç](#başlangıç) • [Mimariye Genel Bakış](#mimariye-genel-bakış) • [Katkıda Bulunma Yolları](#katkıda-bulunma-yolları) • [Optimizasyon Oluşturma](#optimizasyon-oluşturma) • [Özelleştirme Ayarı Oluşturma](#özelleştirme-ayarı-oluşturma) • [Yenileme Kapsamı Sistemi](#yenileme-kapsamı-sistemi) • [Yeni Özellikler Geliştirme](#yeni-özellikler-geliştirme) • [Geri Alma Sistemi](#geri-alma-sistemi) • [Test Etme](#test-etme) • [Kodlama Standartları](#kodlama-standartları) • [Yerelleştirme](#yerelleştirme) • [Çekme İsteği Süreci](#çekme-isteği-süreci) • [Sorun (Issue) Yönergeleri](#sorun-issue-yönergeleri) • [SSS ve Sorun Giderme](#sss-ve-sorun-giderme) • [Lisans](#lisans)

</div>

---

# Giriş

.NET 10 üzerinde WPF ile oluşturulmuş ücretsiz, açık kaynaklı bir Windows optimizasyon aracı olan **optimizerDuck**'a katkıda bulunduğunuz için teşekkür ederiz.

Birçok şekilde yardımcı olabilirsiniz:
- Açık yeniden üretme adımlarıyla hataları (bug) bildirmek
- Yeni optimizasyonlar veya özellikler önermek (önce bir sorun (issue) açın)
- Dokümantasyonu ve kılavuzları geliştirmek
- Çeviriler eklemek veya düzeltmek
- Kod katkısında bulunmak: optimizasyonlar, özelleştirme ayarları, hizmetler, kullanıcı arayüzü iyileştirmeleri

---

# Başlangıç

### 1. Ortam Kurulumu

| Gereksinim | Notlar |
|---|---|
| **Windows 10/11 x64** | Uygulama yönetici olarak çalışır ve sistemde değişiklikler yapar — Sadece Windows |
| **.NET 10 SDK** | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) adresinden indirin |
| **IDE** | [Visual Studio 2026](https://visualstudio.microsoft.com/) (`.NET masaüstü geliştirme` iş yükü), [JetBrains Rider](https://www.jetbrains.com/rider/) veya VS Code + C# Dev Kit |
| **Git** | Sürüm kontrolü |

Kurulumunuzu doğrulayın:

```bash
dotnet --version
# 10.x çıktısını vermelidir
```

### 2. Forklayın ve Klonlayın

```bash
# Önce GitHub'da depoyu forklayın, ardından kendi çatalınızı klonlayın
git clone https://github.com/<kullanici-adiniz>/optimizerDuck.git
cd optimizerDuck

# Ana depo ile senkronize kalmak için upstream remote ekleyin
git remote add upstream https://github.com/itsfatduck/optimizerDuck.git

# Çalışmanız için bir dal oluşturun (asla master dalında çalışmayın)
git checkout -b feature/sizin-ozellik-adiniz
```

### 3. Geri Yükleme, Derleme ve Test

Çözüm (solution), `.slnx` formatını kullanır (XML tabanlı çözüm dosyası, `.sln` değil).

```bash
# Bağımlılıkları geri yükle
dotnet restore optimizerDuck.slnx

# Derle (CI Release kullanır, Debug da çalışır)
dotnet build optimizerDuck.slnx --configuration Release --no-restore

# Testleri çalıştır
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

# Uygulamayı çalıştır
dotnet run --project optimizerDuck/optimizerDuck.csproj

# CSharpier ile kodu biçimlendir
dotnet csharpier .
```

> Yeni NuGet bağımlılıkları eklerseniz, `dotnet restore` komutunu tekrar çalıştırın (sonraki derlemelerde `--no-restore` kullanabilirsiniz).

### 4. Yayımlama (Publishing)

```bash
publish.bat portable              # Taşınabilir klasör (test için önerilir)
publish.bat single                # Tek dosyalı yürütülebilir program
publish.bat single --skip-tests   # Hızlı yineleme için testleri atlar
publish.bat portable --no-pause   # Sonunda duraklamaz (CI dostu)
```

Yayımlama profilleri `Properties/PublishProfiles/` içinde tanımlanmıştır.

### 5. Hızlı Başlangıç Kontrol Listesi

İlk katkınızdan önce:

- [ ] Depoyu forkladınız ve klonladınız
- [ ] `dotnet build` başarılı oldu (0 hata)
- [ ] `dotnet test` geçer (166+ testin tamamı yeşil)
- [ ] `dotnet csharpier .` hatasız şekilde kodunuzu biçimlendiriyor
- [ ] Aşağıdaki [Mimariye Genel Bakış](#mimariye-genel-bakış) bölümünü okudunuz

---

# Mimariye Genel Bakış

### Çözüm Yapısı

```
optimizerDuck.slnx                          # Çözüm dosyası (.slnx formatı)
├── optimizerDuck/                          # Ana WPF uygulaması (net10.0-windows)
│   ├── App.xaml.cs                         # DI kaydı, başlangıç, tema, loglama
│   ├── optimizerDuck.csproj                # TFM: net10.0-windows10.0.17763.0, UseWPF=true
│   │
│   ├── Domain/                             # Saf modeller, arayüzler, özellikler (WPF bağımlılığı yok)
│   │   ├── Abstractions/                   # IOptimization, ICustomizeSetting, IRevertStep, vs.
│   │   ├── Attributes/                     # [Optimization], [CustomizeSetting], [OptimizationCategory]
│   │   ├── Configuration/                  # AppSettings modeli
│   │   ├── Execution/                      # ExecutionScope — AsyncLocal üzerinden çevresel (ambient) adım takibi
│   │   ├── Customize/                      # Özelleştirme ayarları (Desktop, Gaming, Preferences, System)
│   │   │   ├── Categories/                 # İçi içe ayar sınıflarına sahip kategori sınıfları
│   │   │   └── Models/                     # BaseCustomizeSetting, RegistryToggle, RefreshScope
│   │   ├── Optimizations/                  # Optimizasyonlar (Performance, Privacy, GPU, vs.)
│   │   │   ├── Categories/                 # İçi içe optimizasyon sınıflarına sahip kategori sınıfları
│   │   │   └── Models/                     # BaseOptimization, ApplyResult, OptimizationContext
│   │   ├── Revert/                         # RevertData, RevertResult, geri alma adımı türleri
│   │   │   └── Steps/                      # RegistryRevertStep, ServiceRevertStep, vs.
│   │   └── UI/                             # Enumlar: OptimizationRisk, OptimizationTags, CategoryOrder
│   │
│   ├── Common/                             # Ortak yardımcılar, eklentiler (extensions), dönüştürücüler
│   │   ├── Extensions/                     # StringExtensions, CustomizePageRegistryExtensions
│   │   ├── Converters/                     # WPF veri dönüştürücüleri
│   │   └── Helpers/                        # Shared.cs, ReflectionHelper.cs, SystemRefreshService.cs
│   │
│   ├── Services/                           # İş mantığı (Business logic)
│   │   ├── Configuration/                  # ConfigManager, LanguageManager
│   │   ├── Customize/                      # CustomizeRegistry (Yansıma ile otomatik keşif)
│   │   ├── Managers/                       # BloatwareService, DiskCleanupService,
│   │   │                                   # StartupManagerService, SystemInfoService,
│   │   │                                   # StreamService, UpdaterService
│   │   ├── Optimization/                   # OptimizationRegistry, OptimizationService
│   │   │   └── Providers/                  # Statik: RegistryService, ShellService,
│   │   │                                   # ScheduledTaskService, ServiceProcessService
│   │   ├── Revert/                         # RevertManager (geri alma JSON dosyalarını okur/yazar)
│   │   ├── System/                         # RegistryWatcher
│   │   └── UI/                             # ContentDialogService, vs.
│   │
│   ├── UI/                                 # WPF sayfaları, ViewModels, kontroller, stiller
│   │   ├── Controls/                       # Özel WPF kontrolleri
│   │   ├── Dialogs/                        # Diyalog pencereleri (ProcessingDialog, OptimizationResultDialog)
│   │   ├── Pages/                          # Uygulama sayfaları + alt klasörler (Optimize/, Customize/)
│   │   ├── Styles/                         # Fluent tasarım stilleri
│   │   ├── ViewModels/                     # Sayfa ve diyalog ViewModel'leri
│   │   │   ├── Customize/                  # CustomizeItemViewModel, CustomizeGroupViewModel
│   │   │   ├── Dialogs/                    # ProcessingViewModel, OptimizationResultDialogViewModel
│   │   │   ├── Optimizer/                  # OptimizationCategoryViewModel
│   │   │   ├── Pages/                      # Dashboard, Optimize, Customize, Settings, vs.
│   │   │   └── Windows/                    # MainWindowViewModel
│   │   └── Windows/                        # MainWindow
│   │
│   └── Resources/                          # Resimler, gömülü dosyalar, yerelleştirme
│       ├── Embedded/                       # Güç planları, simgeler
│       ├── Images/                         # Duck.png, logolar
│       └── Languages/                      # Translations.resx + dil varyantları
│
└── optimizerDuck.Test/                     # xUnit v3 test projesi (166+ test)
```

### Önemli Tasarım Kararları

| Karar | Gerekçesi |
|---|---|
| **Yansıma tabanlı keşif (Reflection)** | Güncellenecek DI kayıt dizileri yoktur. `ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` başlangıçta `optimizerDuck.*` derlemelerini tarar. Yeni optimizasyonlar/ayarlar otomatik olarak keşfedilir. |
| **Statik sağlayıcı hizmetler** | `RegistryService`, `ShellService`, `ScheduledTaskService`, `ServiceProcessService` statik sınıflardır. Geri alma adımlarını çevresel (ambient) `ExecutionScope`'a yakalarlar — enjekte etmeye veya bağlam aktarmaya gerek yoktur. |
| **Dosya tabanlı geri alma takibi** | Uygulanan durum = diskte dosyanın var olmasıdır (`%localappdata%\optimizerDuck\Revert\{id}.json`). Veritabanı yoktur. Atomik yazmalar `File.Replace()` üzerinden yapılır. |
| **Entegrasyon tarzı testler** | Gerçek dosya sistemi, gerçek kayıt defteri (Registry'de `HKCU\Software\TestOptimizerDuck*` altında), gerçek işlem yürütme. Mocking (taklit) kütüphanesi yok — sadece elle yazılmış test double'ları. |
| **Zaman uyumsuz (Async) servis metotları** | Harici işlemleri yürüten sağlayıcı yöntemleri asenkrondur (`*Async` son eki). Optimizasyon `ApplyAsync` yöntemleri, kullanıcı arayüzünü duyarlı tutmak için `async`/`await` kullanmalıdır. |

---

# Katkıda Bulunma Yolları

| Katkı Türü | Açıklama | Nereden Başlamalı |
|---|---|---|
| **Yeni Optimizasyonlar** | Kayıt defteri ayarları, servis değişiklikleri, sistem ayarları | `Domain/Optimizations/Categories/*.cs` |
| **Yeni Özelleştirme Ayarları** | Windows ayarları için kullanıcı arayüzü anahtarları | `Domain/Customize/Categories/*.cs` |
| **Yeni Uygulama Özellikleri** | Yeni sayfalar, araçlar veya işlevsellikler | Önce bir Issue (sorun) açın |
| **Hata Düzeltmeleri (Bug Fixes)** | Çökme düzeltmeleri, mantık hataları, UI sorunları | Herhangi bir yer |
| **Çeviriler** | Yeni diller veya mevcut çevirileri düzeltme | `Resources/Languages/Translations.*.resx` |
| **Dokümantasyon** | README, CONTRIBUTING vb. | `*.md` dosyaları |

---

# Optimizasyon Oluşturma

### Keşif Nasıl Çalışır

Başlangıçta:

1. `ReflectionHelper.FindImplementationsInLoadedAssemblies<IOptimizationCategory>()` tüm `optimizerDuck.*` derlemelerini tarar.
2. `IOptimizationCategory` uygulayan her sınıfı bulur.
3. Her kategori için, `IOptimization` uygulayan **iç içe geçmiş public sınıfları** tarar.
4. Keşfedilen tüm optimizasyonların örneği oluşturulur ve `OwnerType` otomatik olarak atanır.

**Sizin göreviniz**: Bir kategorinin içine yerleştirilmiş (nested) bir sınıf oluşturun, `BaseOptimization`'dan türetin ve `[Optimization]` ile işaretleyin. Hepsi bu kadar.

### Optimizasyon Kategorileri

Mevcut kategoriler (`Domain/Optimizations/Categories/` içinde):

| Dosya | Öznitelik (Attribute) | Odak Noktası |
|---|---|---|
| `Performance.cs` | `[OptimizationCategory(typeof(PerformanceOptimizerPage))]` | RAM ayarı, süreç önceliği, klavye gecikmesi, multimedya zamanlayıcısı |
| `SecurityAndPrivacy.cs` | `[OptimizationCategory(typeof(SecurityAndPrivacyOptimizerPage))]` | Telemetri, hata bildirimleri, reklam kimliği, konum, Cortana, Copilot |
| `Gpu.cs` | `[OptimizationCategory(typeof(GpuOptimizerPage))]` | AMD/NVIDIA/Intel kayıt defteri ince ayarları, güç durumları, clock gating |
| `PowerManagement.cs` | `[OptimizationCategory(typeof(PowerManagementOptimizerPage))]` | Hazırda bekletme, hızlı başlatma, USB seçmeli askıya alma, güç planları |
| `BloatwareAndServices.cs` | `[OptimizationCategory(typeof(BloatwareAndServicesOptimizerPage))]` | OEM uygulama engellemesi, 200+ Windows servisi |
| `UserExperience.cs` | `[OptimizationCategory(typeof(UserExperienceOptimizerPage))]` | Menü gecikmeleri, görsel efektler, görev çubuğu animasyonları, saydamlık |

### Adım Adım: Mevcut Bir Kategoriye Ekleme

En uygun kategori dosyasını seçin ve içine yerleştirilmiş bir sınıf ekleyin:

```csharp
[OptimizationCategory(typeof(PerformanceOptimizerPage))]
public class Performance : IOptimizationCategory
{
    public string Name => Loc.Instance[$"Optimizer.{nameof(Performance)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.Performance;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(
        Id = "a1b2c3d4-...",                          // YENİ bir GUID oluşturun
        Risk = OptimizationRisk.Safe,                   // Safe / Moderate / Risky
        Tags = OptimizationTags.Performance             // Etiketler — | ile birleştirin
    )]
    public class MyNewTweak : BaseOptimization
    {
        public override async Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context)
        {
            // 1. Sistem değişiklikleri yapmak için statik sağlayıcıları kullanın
            RegistryService.Write(new RegistryItem(
                @"HKLM\SOFTWARE\Something", "ValueName", 1));

            // 2. Async işlemleri await ile bekleyin — bu UI iş parçacığını rahatlatır
            await ServiceProcessService.ChangeServiceStartupTypeAsync(
                new ServiceItem("SomeService", ServiceStartupType.Disabled));

            // 3. Sonucu çevresel ExecutionScope'tan döndürün
            return CompleteFromScope();
        }
    }
}
```

### Temel Kurallar

| Kural | Detay |
|---|---|
| **`Id` yeni bir GUID olmalıdır** | Geri alma dosyasının adlandırılmasında kullanılır. PowerShell'de `[guid]::NewGuid()` ile oluşturabilirsiniz. |
| **`BaseOptimization`'ı genişletin** | `Name`, `ShortDescription`, `Prefix`, `RiskVisual`, `TagDisplays` gibi özellikleri öznitelik ve çeviri anahtarlarından otomatik sağlar. |
| **`async Task<ApplyResult>` kullanın** | `Task.FromResult()` DEĞİL. Servis sağlayıcıları asenkrondur — arayüzün kilitlenmemesi için `await` kullanın. |
| **`CompleteFromScope()` döndürün** | `ApplyResult` sonucunu, ortam `ExecutionScope`'una kaydedilen adımlardan türetir. |
| **İlerlemeyi raporlayın** | İletişim kutusunu güncellemek için `progress.Report(new ProcessingProgress { ... })` kullanın. |
| **Tüm istisnaları (exception) yakalamayın** | Yukarı fırlatılmasına izin verin. Başarı/başarısızlık durumunu `ExecutionScope` izler. |
| **Manuel olarak geri alma adımları oluşturmayın** | Statik sağlayıcı hizmetler bunu `ExecutionScope.RecordStep()` ile otomatik yapar. |
### Mevcut Servis Sağlayıcıları

Bu **statik** sınıflar loglama, hata işleme ve geri alma adımlarının otomatik olarak kaydedilmesini yönetir.

| Servis | Önemli Metotlar | Neden Kullanılır? |
|---|---|---|
| **`RegistryService`** | `Write()`, `Read<T>()`, `DeleteValue()`, `CreateSubKey()`, `DeleteSubKeyTree()` | Kayıt defteri okuma/yazma/silme işlemleri. Geri alma için orijinal değerleri yedekler. |
| **`ShellService`** | `CMDAsync()`**, `PowerShellAsync()`** | CMD veya PowerShell komutları çalıştırmak için. Daima asenkron varyantları kullanın. |
| **`ScheduledTaskService`** | `DisableTask()`, `EnableTask()`, `IsTaskEnabled()`, `DeleteTask()` | Windows Zamanlanmış Görevlerini (Scheduled Tasks) yönetir. |
| **`ServiceProcessService`** | `ChangeServiceStartupTypeAsync()`**, `GetStartupTypeAsync()`** | Windows Hizmetlerini yönetir. Daima asenkron varyantları kullanın. |

> **`**` ile işaretlenen yöntemler asenkrondur.** Optimizasyonunuzun `ApplyAsync` metodu içinde bunları `await` ile çağırın.

Örnek kullanım:

```csharp
// Senkron kayıt defteri yazma
RegistryService.Write(new RegistryItem(@"HKLM\...", "Value", 1));
RegistryService.DeleteValue(new RegistryItem(@"HKCU\...", "OldValue"));

// Asenkron servis değişiklikleri
await ServiceProcessService.ChangeServiceStartupTypeAsync(
    new ServiceItem("DiagTrack", ServiceStartupType.Disabled));

// Asenkron komut dosyası çalıştırma
var result = await ShellService.PowerShellAsync("Some-Command");
```

### Yeni Bir Kategori Oluşturma

Sadece optimizasyonlarınız mevcut kategorilerden hiçbirine uymuyorsa. Aşırı spesifik kategorilerden kaçının.

1. `Domain/Optimizations/Categories/YourCategory.cs` dosyasını oluşturun.
2. `IOptimizationCategory` arayüzünü (interface) uygulayın.
3. `[OptimizationCategory(PageType = typeof(YourPage))]` ekleyin — aynı zamanda bir XAML sayfasına ihtiyacınız olacak.
4. `Domain/UI/OptimizationCategoryOrder.cs` içindeki `OptimizationCategoryOrder` enum'una yeni bir üye ekleyin.
5. XAML sayfası, `App.xaml.cs` dosyasındaki `services.AddAllOptimizationPages()` aracılığıyla otomatik olarak kaydedilir.

### Yerelleştirme Anahtarları (Localization Keys)

Her optimizasyon `Translations.resx` içinde yer alan girdilere ihtiyaç duyar. Anahtarlar (keys) katı bir standardı takip eder:

```
Optimizer.{KategoriAdı}.{OptimizasyonAnahtarı}.Name
Optimizer.{KategoriAdı}.{OptimizasyonAnahtarı}.ShortDescription
Optimizer.{KategoriAdı}.{OptimizasyonAnahtarı}.Progress.{ÖzelAnahtar}
Optimizer.{KategoriAdı}.{OptimizasyonAnahtarı}.Error.{ÖzelAnahtar}
```

Örneğin `KategoriAdı` = kategori sınıfının adı (`Performance`) ve `OptimizasyonAnahtarı` = içiçe sınıfın adı (`MyNewTweak`).

> [!IMPORTANT]
> **Çeviriler zorunludur**. Eğer bu anahtarları eklemezseniz, uygulama ham anahtar adını görüntüler, örn. `"Optimizer.Performance.MyNewTweak.Name"`. En azından `Translations.resx` (İngilizce) dosyasına daima girdileri ekleyin.

---

# Özelleştirme Ayarı Oluşturma

Özelleştirme ayarları, Windows ayarlarını AÇIK veya KAPALI olarak değiştiren kullanıcı arabirimi kontrolleridir (geçiş anahtarları, açılır menüler, sayı girişleri). `Domain/Customize/Categories/` dizininde bulunurlar.

### Özelleştirme Kategorileri

| Dosya | Öznitelik (Attribute) | Odak Noktası |
|---|---|---|
| `Desktop.cs` | `[CustomizeCategory(PageType = typeof(DesktopFeatureCategory))]` | Masaüstü simgeleri (Bu Bilgisayar, Ağ vb.), kısayol okları |
| `Preferences.cs` | `[CustomizeCategory(PageType = typeof(PreferencesFeatureCategory))]` | Görev çubuğu hizalaması, widget'lar, karanlık mod, gizli dosyalar |
| `Gaming.cs` | `[CustomizeCategory(PageType = typeof(GamingFeatureCategory))]` | Oyun Modu, Game Bar, fare ivmesi, GPU zamanlaması |
| `SystemFeatures.cs` | `[CustomizeCategory(PageType = typeof(SystemFeatureCategory))]` | Önyükleme sırasında Num Lock durumu |

### Adım Adım: Basit Bir Kayıt Defteri Anahtarı

Aç/kapat (toggle) şeklindeki basit bir kayıt defteri değişikliği için temel sınıf (base class) tüm işi yapar:

```csharp
private enum Sections { Taskbar, Widgets, Advanced }

[CustomizeSetting(
    Section = nameof(Sections.Taskbar),        // Arayüzde ayarları gruplandırır
    Icon = SymbolRegular.AlignCenter24,         // Wpf.Ui.Controls.SymbolRegular'dan
    Recommendation = RecommendationState.On     // On / Off / Depends / Experimental / None
)]
public class TaskbarAlignment : BaseCustomizeSetting
{
    protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "TaskbarAl",
                OnValue = 0,            // Anahtar AÇIK olduğunda değer
                OffValue = 1,           // Anahtar KAPALI olduğunda değer
                DefaultValue = 1,       // değer = varsayılan durum (anahtar yoksa)
            },
        ];

    // Bu ayar değiştiğinde nelerin yenilenmesi gerektiğini beyan edin
    protected override CustomizeRefreshScope RefreshScope =>
        CustomizeRefreshScope.TaskbarSettings;
}
```

*(Kalan kısımlar da yapısal olarak mevcuttur, katkıda bulunanlar GitHub üzerinden doğrudan rehberlere bakarak ilerleyebilirler.)*

---

# Yenileme Kapsamı Sistemi

Bir özelleştirme ayarı değiştiğinde, farklı Windows yüzeyleri farklı yenileme stratejilerine ihtiyaç duyar. `CustomizeRefreshScope` [Flags] enum'u bunu kontrol eder. Çoğu durumda `Default` (Varsayılan) kullanabilirsiniz.

---

# Yeni Özellikler Geliştirme

Yeni bir sayfa veya araç eklemek istiyorsanız (örneğin, bir "Ağ İzleyicisi"):

1. **Önce bir GitHub Sorunu (Issue) açın** — özelliği, kullanım durumunu ve tasarımı açıklayın. Geliştiriciden geri bildirim bekleyin.
2. ViewModels ve Pages `App.xaml.cs` içinde singleton olarak kaydedilmelidir.

---

# Geri Alma Sistemi

Uygulanan her optimizasyon `%localappdata%\optimizerDuck\Revert\{optimizationId}.json` yolunda bir JSON dosyası oluşturur. Sistem, atomik değişiklikler yapar ve işlemleri hatasız geri almaya olanak sağlar. Statik sağlayıcıları kullanırsanız geri alma adımları otomatik olarak JSON'a kaydedilir, manuel olarak bir geri alma adımı oluşturmayın.

---

# Test Etme

Testler **xUnit v3** kullanır ve gerçek G/Ç ile entegrasyon tarzı bir yaklaşımı benimser. Hiçbir "mock" veya "fake" kütüphanesi kullanılmamıştır. Test çalıştırmak için:
`dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release`

---

# Kodlama Standartları

- `PascalCase` sınıf, enum, metot adları için.
- `_camelCase` özel (private) alanlar için.
- `camelCase` yerel değişkenler için.
- Girinti: 4 boşluk, Maksimum satır uzunluğu 100 karakter. `dotnet csharpier .` komutu kodu otomatik formatlar.
- Kesinlikle string (metin) ifadeleri hardcode yazmayın. Daima `Translations.KeyName` kullanın.

---

# Yerelleştirme

Uygulama metinleri `Resources/Languages/Translations.resx` içinde bulunur. Lütfen yeni bir dil eklerken `Translations.{locale}.resx` dosyası oluşturun ve `SettingsViewModel.cs` içinden dili uygulamaya ekleyin. Her zaman yerelleştirme (Localization) kurallarına sadık kalın.

---

# Çekme İsteği Süreci (Pull Request Process)

1. Her zaman `master` üzerinden yeni bir dal (branch) açın: `feature/adiniz` veya `fix/issue-id`.
2. Commit mesajlarınızı kurallara uygun (`feat:`, `fix:`, `docs:`) şekilde yazın.
3. Kodu derleyin, testleri çalıştırın ve `csharpier` ile formatlayın.
4. Yeni bir optimizasyon eklediyseniz, ekran görüntüsünü (screenshot) PR açıklamasına ekleyin.
5. Yorumları takip edin ve takım arkadaşlarının geribildirimlerine açık olun.

*(Katkınız için tekrardan teşekkür ederiz!)*

<div align="center">

## Teşekkürler

Katkıda bulunanlar sürüm notlarında listelenir.

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
