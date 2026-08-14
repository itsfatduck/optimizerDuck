<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="../assets/optimizerDuck.png" alt="optimizerDuck Afişi" title="optimizerDuck"/></a>

[English](../../CONTRIBUTING.md) | [日本語](CONTRIBUTING.ja-JP.md) | **Türkçe**

[Giriş](#introduction) • [Başlangıç](#getting-started) • [Mimariye Genel Bakış](#architecture-overview) • [Katkıda Bulunma Yolları](#ways-to-contribute) • [Optimizasyon Oluşturma](#creating-an-optimization) • [Özelleştirme Ayarı Oluşturma](#creating-a-customize-setting) • [Koşul Sistemi](#the-condition-system) • [Yenileme Kapsamı Sistemi](#the-refresh-scope-system) • [Yeni Özellikler Geliştirme](#building-new-features) • [Geri Alma Sistemi](#revert-system) • [Test Etme](#testing) • [Kodlama Standartları](#coding-standards) • [Yerelleştirme](#localization) • [Çekme İsteği Süreci](#pull-request-process) • [Sorun (Issue) Yönergeleri](#issue-guidelines) • [SSS ve Sorun Giderme](#faq--troubleshooting) • [Lisans](#license)

</div>

---

<h1 id="introduction">Giriş</h1>

**.NET 10 üzerinde WPF ile oluşturulmuş ücretsiz, açık kaynaklı bir Windows optimizasyon aracı** olan **optimizerDuck**'a katkıda bulunduğunuz için teşekkür ederiz.

Birçok şekilde yardımcı olabilirsiniz:

- Açık yeniden üretme adımlarıyla hataları bildirmek
- Yeni optimizasyonlar veya özellikler önermek (önce bir sorun açın)
- Dokümantasyonu ve kılavuzları geliştirmek
- Çeviriler eklemek veya düzeltmek
- Kod katkısında bulunmak: optimizasyonlar, özelleştirme ayarları, hizmetler, kullanıcı arayüzü iyileştirmeleri
- Test eklemek veya mevcut testleri gözden geçirmek

> **Burada yeni misiniz?** Önce [Başlangıç](#getting-started) bölümünü, ardından [Mimariye Genel Bakış](#architecture-overview) bölümünü okuyun. En yaygın iki kod katkısı [Optimizasyon Oluşturma](#creating-an-optimization) ve [Özelleştirme Ayarı Oluşturma](#creating-a-customize-setting) bölümleridir.

---

<h1 id="getting-started">Başlangıç</h1>

<h3 id="environment-setup">1. Ortam Kurulumu</h3>

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

<h3 id="fork-and-clone">2. Forklayın ve Klonlayın</h3>

```bash
# Önce GitHub'da depoyu forklayın, ardından kendi çatalınızı klonlayın
git clone https://github.com/<kullanici-adiniz>/optimizerDuck.git
cd optimizerDuck

# Ana depo ile senkronize kalmak için upstream remote ekleyin
git remote add upstream https://github.com/itsfatduck/optimizerDuck.git

# Çalışmanız için bir dal oluşturun (asla master dalında çalışmayın)
git checkout -b feature/sizin-ozellik-adiniz
```

<h3 id="restore-build-test">3. Geri Yükleme, Derleme ve Test</h3>

Çözüm, `.slnx` formatını kullanır (XML tabanlı çözüm dosyası, `.sln` değil).

```bash
# Bağımlılıkları geri yükle
dotnet restore optimizerDuck.slnx

# Derle (CI Release kullanır, Debug da çalışır)
dotnet build optimizerDuck.slnx --configuration Release --no-restore

# Testleri çalıştır
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

# Uygulamayı çalıştır (sistem ayarlarını değiştirdiği için yükseltilmiş komut istemi gerekir)
dotnet run --project optimizerDuck/optimizerDuck.csproj

# CSharpier ile kodu biçimlendir
dotnet csharpier .
```

> Yeni NuGet bağımlılıkları eklerseniz, `dotnet restore` komutunu tekrar çalıştırın (sonraki derlemelerde `--no-restore` kullanabilirsiniz).

<h3 id="publishing">4. Yayımlama (Publishing)</h3>

```bash
publish.bat portable              # Taşınabilir klasör (test için önerilir)
publish.bat single                # Tek dosyalı yürütülebilir program
publish.bat single --skip-tests   # Hızlı yineleme için testleri atlar
publish.bat portable --no-pause   # Sonunda duraklamaz (CI dostu)
```

`publish.bat` önce test paketini çalıştırır (`--skip-tests` geçilmediyse), ardından seçilen profille (`Portable` veya `Single`) `dotnet publish` çağırır.

<h3 id="quick-start-checklist">5. Hızlı Başlangıç Kontrol Listesi</h3>

İlk katkınızdan önce:

- [ ] Depoyu forkladınız ve klonladınız
- [ ] `dotnet build` başarılı oldu (0 hata)
- [ ] `dotnet test` geçer (tüm testler yeşil)
- [ ] `dotnet csharpier .` hatasız şekilde kodunuzu biçimlendiriyor
- [ ] Aşağıdaki [Mimariye Genel Bakış](#architecture-overview) bölümünü okudunuz

---

<h1 id="architecture-overview">Mimariye Genel Bakış</h1>

<h3 id="solution-structure">Çözüm Yapısı</h3>

```
optimizerDuck.slnx                          # Çözüm dosyası (.slnx formatı)
├── optimizerDuck/                          # Ana WPF uygulaması (net10.0-windows)
│   ├── App.xaml.cs                         # DI kaydı, başlangıç, tema, loglama
│   ├── optimizerDuck.csproj                # TFM: net10.0-windows10.0.17763.0, UseWPF=true
│   ├── app.manifest                        # requireAdministrator UAC seviyesi
│   │
│   ├── Domain/                             # Saf modeller, arayüzler, özellikler (WPF bağımlılığı yok)
│   │   ├── Abstractions/                   # IOptimization, ICustomizeSetting, IRevertStep, IWindow,
│   │   │                                   #   ICustomizeCategory, IOptimizationCategory
│   │   ├── Attributes/                     # [Optimization], [CustomizeSetting],
│   │   │                                   #   [OptimizationCategory], [CustomizeCategory]
│   │   ├── Conditions/                     # Uyumluluk koşul sistemi ("Koşul Sistemi" bölümüne bakın)
│   │   │   ├── BuiltIn/                    # Hazır koşullar (Windows sürümü, GPU/CPU markası,
│   │   │   │                               #   minimum RAM, kayıt defteri anahtarı / hizmet varlığı, ...)
│   │   │   ├── ICondition.cs               # Koşul sözleşmesi
│   │   │   ├── ConditionBase.cs            # Paylaşılan yardımcılar (örn. OS derleme ayrıştırma)
│   │   │   ├── ConditionResult.cs          # Available / Unsupported / Error sonucu
│   │   │   ├── ConditionState.cs           # Sonuç enum'u
│   │   │   ├── ConditionValidation.cs      # Keşif sırasında meta veri doğrulama
│   │   │   └── WindowsBuilds.cs            # OS derleme numarası sabitleri
│   │   ├── Configuration/                  # AppSettings modeli
│   │   ├── Exceptions/                     # StepExecutionException
│   │   ├── Execution/                      # ExecutionScope — AsyncLocal ile çevresel adım takibi
│   │   ├── Customize/                      # Özelleştirme ayarları
│   │   │   ├── Categories/                 # İç içe ayar sınıflarına sahip kategori sınıfları
│   │   │   └── Models/                     # BaseCustomizeSetting, RegistryToggle, RegistryBinding,
│   │   │                                   #   CustomizeRefreshScope, SettingOption,
│   │   │                                   #   CustomizeControlType, RecommendationState, ...
│   │   ├── Optimizations/                  # Optimizasyonlar
│   │   │   ├── Categories/                 # İç içe optimizasyon sınıflarına sahip kategori sınıfları
│   │   │   └── Models/                     # BaseOptimization, ApplyResult, OptimizationContext,
│   │   │       ├── Bloatware/              # Önceden yüklü uygulamalar için AppXPackage modeli
│   │   │       ├── Cleanup/                # Disk temizliği için CleanupItem
│   │   │       ├── ScheduledTask/          # ScheduledTaskModel
│   │   │       ├── Services/               # RegistryItem, ServiceItem, ShellResult, ServiceStartupType
│   │   │       └── StartupManager/         # StartupApp, StartupTask modelleri
│   │   ├── Revert/                         # RevertData, RevertResult, geri alma adımı türleri
│   │   │   └── Steps/                      # RegistryRevertStep, ServiceRevertStep,
│   │   │                                   #   ScheduledTaskRevertStep, ShellRevertStep, UsbPowerRevertStep
│   │   └── UI/                             # Enumlar: OptimizationRisk, OptimizationTags,
│   │                                       #   OptimizationCategoryOrder, CustomizeOrder,
│   │                                       #   LanguageOption, OptimizationState, RiskVisual,
│   │                                       #   ProcessingProgress, ...
│   │
│   ├── Common/                             # Ortak yardımcılar, eklentiler, dönüştürücüler
│   │   ├── Converters/                     # WPF değer dönüştürücüleri (BooleanToVisibility, MBToGB, ...)
│   │   ├── Extensions/                     # StringExtensions, sayfa-kayıt eklentileri,
│   │   │                                   #   LanguageExtensions
│   │   └── Helpers/                        # Shared.cs, ReflectionHelper.cs, SystemRefreshService.cs,
│   │                                       #   EmbeddedResourceHelper.cs, WmiHelper.cs,
│   │                                       #   GitHubSourceHelper.cs, ThemeResource.cs, ...
│   │
│   ├── Services/                           # İş mantığı katmanı
│   │   ├── Conditions/                     # ConditionEvaluator (statik değerlendirme giriş noktası)
│   │   ├── Configuration/                  # ConfigManager, LanguageManager
│   │   ├── Customize/                      # CustomizeRegistry (yansıma tabanlı keşif)
│   │   ├── Optimization/                   # OptimizationRegistry, OptimizationService
│   │   │   └── Providers/                  # Statik: RegistryService, ShellService (+ ShellPolicy),
│   │   │                                   #   ScheduledTaskService, ServiceProcessService
│   │   ├── Revert/                         # RevertManager (geri alma JSON'unun atomik okuma/yazması)
│   │   ├── System/                         # RegistryWatcher (+ IRegistryWatcher), SystemInfoService,
│   │   │                                   #   StreamService, UpdaterService, CrossPageEventBus
│   │   └── UI/                             # BloatwareService, DiskCleanupService, StartupManagerService
│   │
│   ├── UI/                                 # WPF sayfaları, ViewModel'ler, kontroller, stiller
│   │   ├── Behaviors/                      # SmoothScrollBehavior
│   │   ├── Controls/                       # FilledNavigationViewItem, EmptyBadge
│   │   ├── Dialogs/                        # ProcessingDialog, OptimizationDetailsDialog,
│   │   │                                   #   OptimizationResultDialog, RestorePointDialog, LegalDialog,
│   │   │                                   #   BloatwareConfirmationDialog, ScheduledTask diyalogları, ...
│   │   ├── Pages/                          # Dashboard, Optimize, Customize, Settings, Bloatware,
│   │   │   ├── Customize/                  # CustomizePage + Categories/ (otomatik kayıtlı sayfalar)
│   │   │   ├── Optimize/                   # OptimizePage + Categories/ (otomatik kayıtlı sayfalar)
│   │   │   ├── DiskCleanupPage
│   │   │   ├── StartupManagerPage
│   │   │   └── ScheduledTasksPage
│   │   ├── Styles/                         # FluentDesign.xaml, NavigationViewOverride.xaml, ToolTipOverride.xaml
│   │   ├── ViewModels/                     # Sayfa, diyalog ve pencere ViewModel'leri
│   │   └── Windows/                        # MainWindow
│   │
│   └── Resources/                          # Resimler, gömülü dosyalar, yerelleştirme
│       ├── Embedded/                       # Icons/ ve PowerPlans/ (optimizerDuck.pow)
│       ├── Images/                         # Duck.png, GitHub logoları, Discord logosu
│       └── Languages/                      # Translations.resx (varsayılan) + yerel ayar sürümleri
│
└── optimizerDuck.Test/                     # xUnit v3 test projesi (InternalsVisibleTo)
```

> **Yukarıdaki ağacı katı bir referans olarak almayın.** Bu bir haritadır, şartname değildir — klasörler ve dosyalar gelişir. Şüpheye düştüğünüzde gerçek klasörlere bakın. Bu bölümün sonundaki [Proje Yapısı](#project-structure) notuna da bakın.

<h3 id="key-design-decisions">Temel Tasarım Kararları</h3>

| Karar | Gerekçe |
|---|---|
| **Yansıma tabanlı keşif** | Güncellenecek DI kayıt dizisi yok. `ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` `optimizerDuck.*` derlemelerini tarar. Yeni optimizasyonlar/ayarlar otomatik keşfedilir. |
| **Statik sağlayıcı hizmetler** | `RegistryService`, `ShellService`, `ScheduledTaskService`, `ServiceProcessService` statik sınıflardır. Geri alma adımlarını çevresel `ExecutionScope`'a kaydeder — bağlam enjekte etmek veya geçirmek gerekmez. |
| **Dosya tabanlı geri alma takibi** | Uygulanma durumu = diskte dosya var (`%localappdata%\optimizerDuck\Revert\{id}.json`). Veritabanı yok. `File.Replace()` ile atomik yazma. |
| **Koşul sistemi (açık kalma)** | Optimizasyonlar ve ayarlar uyumluluk koşulları bildirebilir. Değerlendirme hataları asla bir öğeyi gizlemez — [Koşul Sistemi](#the-condition-system) bölümüne bakın. |
| **Entegrasyon tarzı testler** | Gerçek dosya sistemi, gerçek kayıt defteri (`HKCU\Software\TestOptimizerDuck*` altında), gerçek süreç yürütme. Mock kütüphanesi yok — yalnızca elle yazılmış test ikizleri. |
| **Asenkron hizmet metotları** | Harici süreç çalıştıran sağlayıcı metotları asenkrondur (`*Async` eki). Optimizasyon `ApplyAsync` metotları, UI'nın duyarlı kalması için `async`/`await` kullanmalıdır. |
| **Statik WMI yardımcısı** | `WmiHelper.Initialize()` başlangıçta çalışır ve anormal sonlanma için WMI temizleme işleyicilerini kaydeder. |
| **Bekleyen değişiklik takibi** | `App.HasPendingChanges`, geri alınmamış optimizasyonları izler. Uygulama kapanırken PC/Explorer'ı yeniden başlatma veya çıkış seçenekleri sunar. |

<h3 id="project-structure">Proje Yapısı</h3>

Yetkili yapı üç yerde bulunur; bir şeyi taşıdığınızda veya yeniden adlandırdığınızda bunların hepsi senkron tutulmalıdır:

1. **Diskteki klasörler** — `optimizerDuck/` (uygulama) ve `optimizerDuck.Test/` (testler). Başka üst düzey proje dizini yoktur.
2. **`optimizerDuck.csproj`** — gömülü kaynaklar, resimler, paket referansları.
3. **`App.xaml.cs`** — DI kayıtları ve başlangıç sırası.

Bu iki proje klasörünün dışında üst düzey dizin oluşturmayın.

---

<h1 id="ways-to-contribute">Katkıda Bulunma Yolları</h1>

| Katkı Türü | Açıklama | Nereden Başlamalı |
|---|---|---|
| **Yeni Optimizasyonlar** | Kayıt defteri ayarları, hizmet değişiklikleri, sistem ayarları | `Domain/Optimizations/Categories/*.cs` |
| **Yeni Özelleştirme Ayarları** | Windows ayarları için UI anahtarları (Oyun Modu, Fare Hızlandırma, Görev Çubuğu, vb.) | `Domain/Customize/Categories/*.cs` |
| **Yeni Koşullar** | Optimizasyon/ayarlar için uyumluluk kapıları (Windows sürümü, donanım, ...) | `Domain/Conditions/` |
| **Yeni Uygulama Özellikleri** | Yeni sayfalar, araçlar veya işlevler | Önce bir sorun açın |
| **Hata Düzeltmeleri** | Çökme düzeltmeleri, mantık hataları, UI sorunları | Herhangi bir yer |
| **Çeviriler** | Yeni diller veya mevcut çevirileri düzeltme | `Resources/Languages/Translations.*.resx` |
| **Dokümantasyon** | README, CONTRIBUTING vb. | `*.md` dosyaları |
| **Test Etme** | Mevcut veya yeni optimizasyonlar için test ekleme/gözden geçirme | `optimizerDuck.Test/` |

---

<h1 id="creating-an-optimization">Optimizasyon Oluşturma</h1>

<h3 id="how-discovery-works">Keşif Nasıl Çalışır</h3>

Başlangıçta uygulama `OptimizationRegistry.PreloadOptimizationsAsync()` çağırır. Bu, yansıma işini arka plan iş parçacığında çalıştırır:

1. `ReflectionHelper.FindImplementationsInLoadedAssemblies<IOptimizationCategory>()` her kategori sınıfını bulur.
2. Her kategori için `IOptimization` uygulayan **iç içe public sınıfları** tarar.
3. Her optimizasyon örneklenir, `OwnerType` atanır ve `[Optimization]` meta verisi (`Condition` dahil) doğrulanır.
4. `OptimizationService.UpdateOptimizationStateAsync` diskteki geri alma dosyalarını tarar ve her optimizasyonu Uygulandı/Uygulanmadı olarak işaretler.
5. Optimize sayfası bağlamadan önce `EnsurePreloadedAsync()` çağırır (önceden yüklendiyse hiçbir şey yapmaz).

**Sizin işiniz**: Bir kategorinin içinde iç içe bir sınıf oluşturun, `BaseOptimization`'dan türetin ve `[Optimization]` ile işaretleyin. Hepsi bu — güncellenecek kayıt yok.

<h3 id="optimization-categories">Optimizasyon Kategorileri</h3>

Kategoriler `Domain/Optimizations/Categories/` içinde, her dosyada bir kategori olacak şekilde bulunur. Kesin küme zamanla değişir — yetkili liste için o klasöre bakın. Bu yazı yazıldığı sırada kategoriler şunlardır:

| Dosya | Odak |
|---|---|
| `Performance.cs` | RAM ayarı, süreç önceliği, klavye gecikmesi, multimedya zamanlayıcı, erişilebilirlik kısayol tuşları |
| `SecurityAndPrivacy.cs` | Telemetri, hata raporlama, reklam kimliği, konum, Copilot, etkinlik geçmişi, teslimat optimizasyonu vb. |
| `Gpu.cs` | AMD/NVIDIA/Intel kayıt defteri ayarları, güç durumları, saat geçitleme, ASPM, asenkron çevirme |
| `PowerManagement.cs` | Hazırda bekletme, hızlı başlangıç, USB seçici askıya alma, özel güç planı kurulumu |
| `BloatwareAndServices.cs` | OEM önceden yüklü uygulama engelleme, Windows hizmeti başlangıç türü optimizasyonu |
| `UserExperience.cs` | Menü gecikmeleri, görsel efektler, görev çubuğu animasyonları, saydamlık, Başlat Menüsü web araması |
| `AI.cs` | Windows AI özellikleri (Recall, Click To Do) |

Her kategori sınıfı, onu UI sayfasına bağlayan bir `[OptimizationCategory(typeof(SomePage))]` özniteliği taşır.

<h3 id="step-by-step-add-to-existing-category">Adım Adım: Mevcut Kategoriye Ekleme</h3>

En uygun kategori dosyasını seçin ve iç içe bir sınıf ekleyin:

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
        Tags = OptimizationTags.Performance,            // Bayraklar — | ile birleştirin
        Condition = typeof(Windows11Condition)          // İsteğe bağlı ("Koşul Sistemi"ne bakın)
    )]
    public class MyNewTweak : BaseOptimization
    {
        public override async Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context)
        {
            // 1. Sistem değişiklikleri için statik sağlayıcıları kullanın
            RegistryService.Write(new RegistryItem(
                @"HKLM\SOFTWARE\Something", "ValueName", 1));

            // 2. Asenkron işlemleri await edin — bu UI iş parçacığını serbest bırakır
            await ServiceProcessService.ChangeServiceStartupTypeAsync(
                new ServiceItem("SomeService", ServiceStartupType.Disabled));

            // 3. Çevresel ExecutionScope'tan sonucu döndürün
            return CompleteFromScope();
        }
    }
}
```

<h3 id="key-rules">Temel Kurallar</h3>

| Kural | Detay |
|---|---|
| **`Id` yeni bir GUID olmalı** | Geri alma dosyası adlandırma ve uygulanma durumu takibi için kullanılır. PowerShell'de `[guid]::NewGuid()` ile oluşturun. |
| **`BaseOptimization`'dan türetin** | Öznitelik + yerelleştirme anahtarlarından `Name`, `ShortDescription`, `RiskVisual`, `TagDisplays` sağlar. |
| **`OwnerType` otomatik atanır** | Keşif bunu ayarlar — kendiniz ayarlamayın. |
| **`async Task<ApplyResult>` kullanın** | Hizmet sağlayıcıları asenkrondur — UI'nın duyarlı kalması için `await` edin. |
| **`CompleteFromScope()` döndürün** | Çevresel `ExecutionScope`'a kaydedilen adımlardan `ApplyResult` türetir. `ApplyResult`'ı elle oluşturmayın. |
| **İlerlemeyi bildirin** | UI diyaloğunu güncellemek için `progress.Report(new ProcessingProgress { ... })` kullanın. |
| **Tüm istisnaları yakalamayın** | Yukarı yayılsın. `ExecutionScope` başarı/başarısızlığı izler; `OptimizationService` istisnaları işler. |
| **Geri alma adımlarını elle oluşturmayın** | Statik sağlayıcı hizmetler bunu `ExecutionScope.RecordStep()` ile otomatik yapar. |
| **`context.Logger` kullanın** | Önemli tanılama bilgileri için günlük kaydı sağlar. |
| **`context.Snapshot` kullanın** | `OptimizationContext.Snapshot` (`SystemSnapshot`) RAM, GPU, CPU, OS bilgisi verir. Koşullu mantık için kullanın. |
| **`context.StreamService` kullanın** | Uzak kaynakları (örn. güç planları) indiren optimizasyonlar için. |
| **Gerekirse `Condition` bildirin** | Windows sürümü veya donanımla kapılayın — [Koşul Sistemi](#the-condition-system) bölümüne bakın. |

<h3 id="available-service-providers">Mevcut Hizmet Sağlayıcılar</h3>

Bu **statik** sınıflar günlük kaydı, hata işleme ve otomatik geri alma adımı kaydını yönetir.

| Hizmet | Temel Metotlar | Neden Kullanılır |
|---|---|---|
| **`RegistryService`** | `Write()`, `Read<T>()`, `DeleteValue()`, `CreateSubKey()`, `DeleteSubKeyTree()`, `KeyExists()`, `CleanupEmptyKeys()` | Kayıt defteri anahtarlarını oku/yaz/sil. Geri alma için orijinal değerleri yedekler. params dizisiyle toplu yazmayı destekler. |
| **`ShellService`** | `CMDAsync()`, `PowerShellAsync()`, `CMD()` (senkron), `PowerShell()` (senkron) | CMD veya PowerShell komutları çalıştırır. Asenkron sürümleri tercih edin. Geri alma için isteğe bağlı `revertCommand` parametresi. Standart dışı çıkış kodları için `ShellPolicy`'ye bakın. |
| **`ScheduledTaskService`** | `DisableTask()`, `EnableTask()`, `IsTaskEnabled()`, `DeleteTask()`, `GetAllTasks()`, `RegisterTask()`, `RunTask()`, `StopTask()` | Windows Zamanlanmış Görevlerini yönetir. |
| **`ServiceProcessService`** | `ChangeServiceStartupTypeAsync()`, `GetStartupTypeAsync()` | Windows Hizmetlerini yönetir. Her zaman asenkron sürümleri kullanın. params dizisiyle toplu değişikliği destekler. |

> **params ile birden çok öğe kabul eden metotlar**: Çoğu yazma/değiştirme metodu bir params dizisi kabul eder (örn. `RegistryService.Write(item1, item2, item3)`). Bu, birden çok ayrı çağrıdan daha verimlidir.

Örnek kullanım:

```csharp
// Senkron kayıt defteri yazma — birden çok öğeyi tek seferde
RegistryService.Write(
    new RegistryItem(@"HKLM\...", "Value1", 1),
    new RegistryItem(@"HKLM\...", "Value2", 0)
);
RegistryService.DeleteValue(new RegistryItem(@"HKCU\...", "OldValue"));

// Asenkron hizmet değişiklikleri — birden çok hizmeti tek seferde
await ServiceProcessService.ChangeServiceStartupTypeAsync(
    new ServiceItem("DiagTrack", ServiceStartupType.Disabled),
    new ServiceItem("dmwappushservice", ServiceStartupType.Disabled)
);

// Geri alma komutuyla asenkron kabuk komutu
var result = await ShellService.CMDAsync(
    "powercfg /h off",
    "powercfg /h on"     // geri alma komutu saklanır
);

// Asenkron PowerShell
var usbStates = await ShellService.PowerShellAsync(
    "Get-CimInstance -Namespace root\\wmi -ClassName MSPower_DeviceEnable"
);
```

<h3 id="handling-async">Asenkron İşlemleri Yönetme</h3>

Tüm optimizasyonların `async`/`await`'e ihtiyacı yoktur. Optimizasyonunuz yalnızca senkron kayıt defteri yazma yapıyorsa (asenkron çağrı yok), `Task.FromResult()` döndürebilirsiniz:

```csharp
public override Task<ApplyResult> ApplyAsync(
    IProgress<ProcessingProgress> progress,
    OptimizationContext context)
{
    RegistryService.Write(new RegistryItem(@"HKLM\...", "Value", 1));
    context.Logger.LogInformation("Applied tweak");
    return Task.FromResult(CompleteFromScope());
}
```

Ancak herhangi bir asenkron sağlayıcı (hizmet, kabuk, görev) kullanıyorsanız her zaman `await` edin:

```csharp
public override async Task<ApplyResult> ApplyAsync(...)
{
    await ServiceProcessService.ChangeServiceStartupTypeAsync(...);
    return CompleteFromScope();
}
```

<h3 id="new-category">Yeni Kategori Oluşturma</h3>

Yalnızca optimizasyonlarınız hiçbir mevcut kategoriye uymuyorsa. Aşırı spesifik kategorilerden kaçının.

1. `Domain/Optimizations/Categories/YourCategory.cs` oluşturun.
2. `IOptimizationCategory` uygulayın.
3. `[OptimizationCategory(typeof(YourPage))]` uygulayın — bir XAML sayfasına da ihtiyacınız olacak ([Yeni Özellikler Geliştirme](#building-new-features) bölümüne bakın).
4. Kategorinin doğru sıralanması için `Domain/UI/OptimizationCategoryOrder.cs` içindeki `OptimizationCategoryOrder` enum'una bir üye ekleyin.
5. XAML sayfası `App.xaml.cs` içindeki `services.AddAllOptimizationPages()` ile otomatik kaydedilir.

<h3 id="helper-base-class">Optimizasyon Yardımcı Taban Sınıfı Oluşturma</h3>

Birkaç optimizasyon aynı yapıyı paylaşıyorsa (algılanan GPU'lar üzerinde yinelenen GPU ayarları gibi), soyut bir ara sınıf oluşturun:

```csharp
public abstract class GpuRegistryOptimization : BaseOptimization
{
    protected abstract GpuVendor Vendor { get; }
    protected abstract IReadOnlyList<RegistryItem> CreateItems(string registryPath);

    public override Task<ApplyResult> ApplyAsync(...)
    {
        foreach (var gpu in context.Snapshot.Gpus.Where(g => g.Vendor == Vendor))
        {
            var path = $@"HKLM\...\{index:D4}";
            RegistryService.Write(CreateItems(path).ToArray());
        }
        return Task.FromResult(CompleteFromScope());
    }
}
```

AMD, NVIDIA ve Intel alt sınıflarıyla gerçek bir örnek için `Domain/Optimizations/Categories/Gpu.cs` dosyasına bakın.

<h3 id="localization-keys-optimization">Yerelleştirme Anahtarları</h3>

Her optimizasyonun `Translations.resx` içinde girdileri olmalıdır. Anahtarlar katı bir kuralı izler:

```
Optimizer.{CategoryName}.{OptimizationKey}.Name
Optimizer.{CategoryName}.{OptimizationKey}.ShortDescription
Optimizer.{CategoryName}.{OptimizationKey}.Progress.{CustomKey}
Optimizer.{CategoryName}.{OptimizationKey}.Error.{CustomKey}
```

Burada `CategoryName` = kategori sınıf adı (örn. `Performance`), `OptimizationKey` = iç içe sınıf adı.

> [!IMPORTANT]
> **Çeviriler zorunludur**. Bu anahtarları eklemeyi atlarsanız, uygulama `"Optimizer.Performance.MyNewTweak.Name"` gibi ham anahtar dizeleri gösterir. En azından `Translations.resx` (İngilizce) içine girdi ekleyin.

---

<h1 id="creating-a-customize-setting">Özelleştirme Ayarı Oluşturma</h1>

Özelleştirme ayarları, Windows ayarlarını AÇIK/KAPALI duruma getiren UI kontrolleridir (anahtar düğmeler, açılır menüler, sayı girişleri). `Domain/Customize/Categories/` içinde bulunurlar.

<h3 id="customize-categories">Özelleştirme Kategorileri</h3>

Kategoriler `Domain/Customize/Categories/` içinde, her dosyada bir kategori olacak şekilde bulunur — yetkili liste için oraya bakın. Bu yazı yazıldığı sırada:

| Dosya | Odak |
|---|---|
| `Desktop.cs` | Masaüstü simgeleri (Bu PC, Geri Dönüşüm Kutusu, Ağ, Kullanıcı Dosyaları, Denetim Masası), genel göster/gizle, kısayol oku görünürlüğü |
| `Preferences.cs` | Görev çubuğu hizalaması, widget'lar, görev görünümü, görevi sonlandırma, koyu mod, dosya uzantıları, gizli dosyalar, pano geçmişi, arama modu, saatte saniye, Bing araması, klasik bağlam menüsü |
| `Gaming.cs` | Oyun Modu, Oyun Çubuğu, arka plan kaydı, fare hızlandırma, tam ekran optimizasyonları, GPU zamanlaması |
| `SystemFeatures.cs` | Önyüklemede Num Lock, Geliştirici Modu, uzun yollar, pil yüzdesi |

Her kategori sınıfı, onu UI sayfasına bağlayan bir `[CustomizeCategory(PageType = typeof(SomePage))]` özniteliği taşır.

<h3 id="step-by-step-simple-registry-toggle">Adım Adım: Basit Kayıt Defteri Anahtarı</h3>

Basit bir aç/kapat kayıt defteri anahtarı için taban sınıf tüm işi yapar:

```csharp
private enum Sections { Taskbar, Widgets, Advanced }

[CustomizeSetting(
    Section = nameof(Sections.Taskbar),        // UI'da ayarları gruplar
    Icon = SymbolRegular.AlignCenter24,         // Wpf.Ui.Controls.SymbolRegular'dan
    Recommendation = RecommendationState.On,    // On / Off / Depends / Experimental / None
    Condition = typeof(Windows11Condition)      // İsteğe bağlı uyumluluk koşulu
)]
public class TaskbarAlignment : BaseCustomizeSetting
{
    protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [
            new()
            {
                Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                Name = "TaskbarAl",
                OnValues = [0],       // anahtar AÇIK olduğundaki değer(ler)
                OffValues = [1],      // anahtar KAPALI olduğundaki değer(ler)
                DefaultValue = 1,     // varsayılan durum değeri (anahtar yokken kullanılır)
            },
        ];

    // Bu ayar değiştikten sonra neyin yenilenmesi gerektiğini bildirin
    protected override CustomizeRefreshScope RefreshScope =>
        CustomizeRefreshScope.TaskbarSettings;
}
```

<h3 id="registrytoggle-properties">RegistryToggle Özellikleri</h3>

| Özellik | Tür | Varsayılan | Açıklama |
|---|---|---|---|
| `Path` | `string` | zorunlu | Tam kayıt defteri anahtarı yolu (örn. `@"HKCU\Software\..."`) |
| `Name` | `string` | zorunlu | Kayıt defteri değer adı |
| `OnValues` | `IReadOnlyList<object?>` | `[1]` | "Açık" durumunu temsil eden değerler. Listede `null` "anahtar yok = açık" demektir. |
| `OffValues` | `IReadOnlyList<object?>` | `[0]` | "Kapalı" durumunu temsil eden değerler. Listede `null` "anahtar yok = kapalı" demektir. |
| `DefaultValue` | `object?` | `0` | Anahtar yokken varsayılan durum değeri (Varsayılana Sıfırla için kullanılır). |
| `IsOptional` | `bool` | `false` | `true` ise durum algılama için gerekli değildir. |
| `ValueKind` | `RegistryValueKind` | `DWord` | Kayıt defteri değer türü (DWord, String, vb.). |

**Durum algılama mantığı**: `GetState()` (`BaseCustomizeSetting` içinde) zorunlu olmayan `RegistryToggles`'ı toplar ve yalnızca **her** zorunlu anahtar `OnValues`'tan biriyle eşleştiğinde `true` döndürür.

<h3 id="control-types">Kontrol Türleri</h3>

| Tür | Görüntülenen | Kullanım |
|---|---|---|
| `Toggle` | Aç/kapat anahtarı | Çoğu ayar (varsayılan) |
| `Dropdown` | ComboBox | Çoklu seçim (örn. güç planı, arama kutusu modu, görev çubuğu hizalaması) |
| `Option` | Radyo düğmesi grubu | Birbirini dışlayan görsel seçenekler (örn. sol/orta hizalama) |
| `NumberInt` | Tam sayı metin girişi | Sayısal değerler (örn. saniyeler) |
| `NumberFloat` | Ondalık metin girişi | Hassas değerler |
| `String` | Metin girişi | Serbest biçimli metin |

UI kontrolünü değiştirmek için `ControlType`'ı geçersiz kılın:

```csharp
public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;
```

<h3 id="dropdown-with-options">Seçenekli Açılır Menü</h3>

Açılır menü seçenekleri bir `RegistryBinding` bildirir, böylece taban sınıf mevcut değeri otomatik okuyabilir ve seçimde otomatik yazabilir. `Option()` yardımcısını kullanın:

```csharp
[CustomizeSetting(Section = nameof(Sections.Taskbar), Icon = SymbolRegular.AlignCenter24)]
public class TaskbarAlignment : BaseCustomizeSetting
{
    private const string RegPath =
        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string RegName = "TaskbarAl";

    public override CustomizeControlType ControlType => CustomizeControlType.Dropdown;

    // Option(key, regPath, regName, value) etiketini şuradan okur:
    //   Customize.{Category}.{Feature}.Options.{key}
    protected override IReadOnlyList<SettingOption>? GetOptions() =>
        [Option("Center", RegPath, RegName, 1), Option("Left", RegPath, RegName, 0)];

    protected override CustomizeRefreshScope RefreshScope =>
        CustomizeRefreshScope.TaskbarSettings;
}
```

**Birden çok** kayıt defteri değerine dokunan seçenekler için açık bağlamalar geçirin:

```csharp
protected override IReadOnlyList<SettingOption>? GetOptions() =>
[
    Option("On", 1,
        new RegistryBinding(@"HKCU\...\Key1", "ValueA", 1),
        new RegistryBinding(@"HKCU\...\Key2", "ValueB", "enabled", RegistryValueKind.String)),
    Option("Off", 0,
        new RegistryBinding(@"HKCU\...\Key1", "ValueA", 0),
        new RegistryBinding(@"HKCU\...\Key2", "ValueB", "disabled", RegistryValueKind.String)),
];
```

Taban sınıf mevcut değeri bağlamalardan okur, canlı değer hiçbir bildirilen seçenekle eşleşmediğinde yalnızca bellek içi bir **"Özel"** (veya **"Ayarlanmadı"**) yedeği gösterir ve seçilen seçeneğin tüm bağlamalarını yazar. Yaygın durum için özel bir `ApplyAsync` gerekmez.

<h3 id="dynamic-options">Dinamik Seçenekler (Platforma Duyarlı)</h3>

Seçenekleri Windows sürümüne göre koşullu gösterebilirsiniz:

```csharp
protected override IReadOnlyList<SettingOption>? GetOptions()
{
    if (Shared.IsWindows11OrGreater)
        return
        [
            Option("Hidden", RegPath, RegName, 0),
            Option("Icon", RegPath, RegName, 1),
            Option("IconAndLabel", RegPath, RegName, 2),
            Option("SearchBox", RegPath, RegName, 3),
        ];
    return
    [
        Option("Hidden", RegPath, RegName, 0),
        Option("Icon", RegPath, RegName, 1),
        Option("SearchBox", RegPath, RegName, 2),
    ];
}
```

<h3 id="custom-logic-override">Özel Mantık (GetStateAsync / ApplyAsync'i Geçersiz Kılma)</h3>

Basit kayıt defteri anahtarı olmayan ayarlar için (örn. fare hızlandırma 3 kayıt defteri değerini birleştirir):

```csharp
[CustomizeSetting(
    Section = nameof(Sections.Input),
    Icon = SymbolRegular.Cursor24,
    Recommendation = RecommendationState.Off
)]
public class MouseAcceleration : BaseCustomizeSetting
{
    private const string Path = @"HKCU\Control Panel\Mouse";

    // İzlenen yollar, dış değişiklikler olduğunda UI'nın otomatik yenilenmesini sağlar
    protected override IReadOnlyList<string> GetWatchedRegistryPaths() => [Path];

    public override Task<bool> GetStateAsync()
    {
        return Task.Run(() =>
        {
            var speed = RegistryService.Read<string>(new RegistryItem(Path, "MouseSpeed"));
            var t1 = RegistryService.Read<string>(new RegistryItem(Path, "MouseThreshold1"));
            var t2 = RegistryService.Read<string>(new RegistryItem(Path, "MouseThreshold2"));
            return (int.TryParse(speed, out var s) && s != 0)
                || (int.TryParse(t1, out var a) && a != 0)
                || (int.TryParse(t2, out var b) && b != 0);
        });
    }

    public override async Task ApplyAsync(object? value)
    {
        var isOn = value is bool b && b;
        RegistryService.Write(new RegistryItem(Path, "MouseSpeed", isOn ? "1" : "0"));
        RegistryService.Write(new RegistryItem(Path, "MouseThreshold1", isOn ? "6" : "0"));
        RegistryService.Write(new RegistryItem(Path, "MouseThreshold2", isOn ? "10" : "0"));

        if (NeedsPostAction)
            await ExecutePostActionAsync();
    }

    protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Default;
}
```

> `ApplyAsync`'i geçersiz kıldığınızda, **kendiniz** `await ExecutePostActionAsync()` çağırmalısınız (`NeedsPostAction` ile korunur). Taban sınıf bunu yalnızca varsayılan `RegistryToggles` tabanlı ve açılır menü bağlama tabanlı yollar için otomatik yapar.

<h3 id="state-detection-retry">Yeniden Denemeli Durum Algılama</h3>

Bir değer uygulandıktan sonra UI `GetStateWithRetryAsync()` çağırır (`GetStateAsync()` değil). Bu metot:

1. Durumu `maxRetries` (varsayılan 3) kez, denemeler arasında `delayMs` (varsayılan 80ms) bekleyerek okur.
2. Ardışık iki okuma aynı değerde anlaştığında (yakınsama kontrolü) döner.
3. Denemeler tükendiğinde son okunan değere düşer.

Bu, yazma sonrası kayıt defteri otururken UI'nın bayat değerler göstermesini önler.

<h3 id="non-registry-deps">Kayıt Defteri Dışı Bağımlılıklarla Özel Mantık</h3>

Gömülü kaynak çıkarmayı içeren ayarlar için (kısayol oklarını boş bir simgeyle değiştirmek gibi):

```csharp
public override async Task ApplyAsync(object? value)
{
    var isOn = value is bool b && b;
    if (isOn)
    {
        RegistryService.DeleteValue(new RegistryItem(Path, "29"));
    }
    else
    {
        var outputPath = Path.Combine(Shared.AssetsDirectory, nameof(Desktop), "blank.ico");
        EmbeddedResourceHelper.TryExtract("Icons.blank.ico", outputPath);
        RegistryService.Write(new RegistryItem(Path, "29", outputPath));
    }
    await ExecutePostActionAsync();
}
```

Gömülü kaynakları derlemeden diske çıkarmak için `EmbeddedResourceHelper.TryExtract(resourceName, outputPath)` kullanın.

<h3 id="recommendation-system">Öneri Sistemi</h3>

Her özelleştirme ayarı bir öneri bildirebilir:

```csharp
[CustomizeSetting(..., Recommendation = RecommendationState.On)]
// Mevcut: On, Off, Depends, Experimental, None
```

- **`On`**: AÇIK önerilir — sistemi iyileştirir
- **`Off`**: KAPALI önerilir — sistemi iyileştirir
- **`Depends`**: Kullanıcının özel ihtiyaçlarına/yapılandırmasına bağlıdır
- **`Experimental`**: Kararsız olabilir, dikkatli kullanın
- **`None`** (varsayılan): Öneri gösterilmez

Yerelleştirme anahtarıyla isteğe bağlı bir neden ekleyin: `Customize.{Category}.{Feature}.Recommendation.Reason`.

<h3 id="what-to-override-per-pattern">Desene Göre Geçersiz Kılınacaklar</h3>

| Senaryo | Geçersiz Kılma |
|---|---|
| Basit kayıt defteri anahtarı | `RegistryToggles` + `RefreshScope` |
| Birden çok kayıt defteri anahtarı (örn. Oyun Modu: 2 değer) | `RegistryToggles` (hepsini listeleyin) |
| Açılır menü / seçenekler | `ControlType` → `Dropdown` + `Option(...)` bağlamalarıyla `GetOptions()` |
| Çok değerli mantık (örn. fare hızlandırma: 3 değer) | `GetStateAsync()` + `ApplyAsync()` + `GetWatchedRegistryPaths()` |
| Kayıt defteri etkileşimi olmayan ayar | `GetStateAsync()` + `ApplyAsync()` (tamamen özel) |
| Özel yenileme davranışı | `RefreshScope` (yalnızca bayraklar) veya `ExecutePostActionAsync()` (tam geçersiz kılma) |
| Yakınsamalı durum algılama | `GetStateWithRetryAsync()` (yerleşik — geçersiz kılmayın) |
| Windows sürümüne göre dinamik seçenekler | `GetOptions()`'ı koşullu mantıkla geçersiz kılın |
| Gömülü kaynak çıkarma | Özel `ApplyAsync` içinde `EmbeddedResourceHelper.TryExtract()` |
| Uyumluluk kapısı | `[CustomizeSetting]` özniteliğinde `Condition = typeof(...)` |

<h3 id="create-a-new-customize-category">Yeni Kategori Oluşturma</h3>

1. `Domain/Customize/Categories/YourCategory.cs` oluşturun.
2. `[CustomizeCategory(PageType = typeof(YourPage))]` ile `ICustomizeCategory` uygulayın.
3. `Domain/UI/CustomizeOrder.cs` içindeki `CustomizeOrder` enum'una bir üye ekleyin.
4. XAML sayfasını oluşturun (`UI/Pages/Customize/Categories/` içinde yeni bir sınıf).
5. Sayfa `App.xaml.cs` içindeki `services.AddAllCustomizeCategoryPages()` ile otomatik kaydedilir.

<h3 id="localization-keys-customize">Özelleştirme Ayarları için Yerelleştirme Anahtarları</h3>

```
Customize.{CategoryName}.{SettingKey}.Name
Customize.{CategoryName}.{SettingKey}.Description
Customize.{CategoryName}.{SettingKey}.Options.{OptionKey}    (SettingOption kullanılıyorsa)
Customize.{CategoryName}.{SettingKey}.Recommendation.Reason   (Recommendation != None ise)
Customize.{CategoryName}.Section.{SectionName}                (bölüm başlıkları için)
```

---

<h1 id="the-condition-system">Koşul Sistemi</h1>

Koşullar, bir optimizasyonu veya özelleştirme ayarını bir uyumluluk kontrolünün arkasına kapılamanıza olanak tanır; böylece UI, çalışmayacak bir şeyi uygulamak yerine "bu, sisteminizde desteklenmiyor" diyebilir.

<h3 id="core-concepts">Temel Kavramlar</h3>

Koşullar `Domain/Conditions/` içinde bulunur ve `Services/Conditions/` içindeki statik `ConditionEvaluator` tarafından değerlendirilir.

| Parça | Amaç |
|---|---|
| `ICondition` | Sözleşme: `ConditionResult Evaluate(SystemSnapshot snapshot)`. Uygulamaların public parametresiz yapıcıya ihtiyacı vardır (yansıma ile örneklenirler). |
| `ConditionBase` | Paylaşılan yardımcılara sahip isteğe bağlı taban sınıf (örn. OS derleme numarası ayrıştırmak için `TryGetOsBuild`). |
| `ConditionResult` | Sonuç: `Available`, `Unsupported(title, description)` veya `Error()`. Yerelleştirilmiş metin sağlayıcılar üzerinden tembel çözülür. |
| `ConditionState` | `Available`, `Unsupported`, `Error`. |
| `ConditionValidation` | Keşif sırasında `Condition = typeof(...)` meta verisini doğrular; yanlış yapılandırmalar başlangıçta hızlıca başarısız olur. |
| `WindowsBuilds` | OS derleme numarası sabitleri (örn. `Windows11`, `Windows11_24H2`). |

**Açık kalma ilkesi**: bir öğeyi yalnızca `Unsupported` engeller, o da yalnızca zaten uygulanmadıysa (veya kullanıcı gizlemediyse). `Error` ve doldurulmamış bir anlık görüntü hiçbir şeyi asla gizlemez — eksik donanım algılaması, kullanıcının hâlâ kullanabileceği seçenekleri kaldırmamalıdır.

<h3 id="declaring-a-condition">Koşul Bildirme</h3>

Her iki öznitelik de isteğe bağlı bir `Condition` kabul eder:

```csharp
// Optimizasyon
[Optimization(Id = "...", Risk = OptimizationRisk.Safe,
    Tags = OptimizationTags.Privacy | OptimizationTags.System,
    Condition = typeof(Windows11_24H2OrGreaterCondition))]
public class DisableRecall : BaseOptimization { ... }

// Özelleştirme ayarı
[CustomizeSetting(Section = ..., Icon = SymbolRegular.Grid24,
    Condition = typeof(Windows11Condition))]
public class TaskbarWidgets : BaseCustomizeSetting { ... }
```

<h3 id="built-in-conditions">Yerleşik Koşullar</h3>

Hazır koşullar `Domain/Conditions/BuiltIn/` içinde bulunur — tam ve güncel liste için o klasöre bakın. Örnekler:

| Koşul | Eşleştiği |
|---|---|
| `Windows10Condition` / `Windows11Condition` | Derleme numarasına göre OS sürümü |
| `Windows11_24H2OrGreaterCondition` | Windows 11 24H2 (derleme 26100) veya üstü |
| `CpuBrandCondition` / `GpuBrandCondition` | CPU/GPU satıcısı (Intel, AMD, NVIDIA) |
| `MinimumRamCondition` (taban) / `SixteenGbRamCondition` | Minimum kurulu RAM |
| `RegistryKeyExistsCondition` | Bir kayıt defteri anahtarının varlığı |
| `ServiceExistsCondition` | Bir Windows hizmetinin varlığı |
| `RecallInstalledCondition` | Windows Recall'ın varlığı |

<h3 id="writing-a-custom-condition">Özel Koşul Yazma</h3>

```csharp
public sealed class MyCondition : ConditionBase
{
    public override ConditionResult Evaluate(SystemSnapshot snapshot)
    {
        // ConditionBase.TryGetOsBuild "22631.xxxx" -> 22631 olarak ayrıştırır
        if (TryGetOsBuild(snapshot, out var build) && build >= 22000)
            return ConditionResult.Available;

        return ConditionResult.Unsupported(
            () => Loc.Instance["Condition.MyCondition.Title"],
            () => Loc.Instance["Condition.MyCondition.Description"]);
    }
}
```

Yönergeler:

- Sistem geçtiğinde **`Available`**; geçmediğinde **`Unsupported(title, description)`** döndürün.
- Beklenmeyen hatalar için `ConditionResult.Error()` kullanın (veya fırlatın — `ConditionEvaluator` yakalar ve `Error`'a eşler). Hatalar engellemez.
- Kullanıcıya dönük metni yerelleştirme sağlayıcılarının arkasına koyun (`() => Loc.Instance[...]`), böylece okuma sırasında güncel kültür kazanır.
- Sınıfa bir **public parametresiz yapıcı** verin (hiç eklemeyin veya açık boş bir yapıcı ekleyin).

<h3 id="how-conditions-evaluated">Nasıl Değerlendirilir</h3>

1. Keşif, bildirilen türün `ICondition` uyguladığını ve oluşturulabilir olduğunu doğrulamak için `ConditionValidation.Validate(...)` çağırır.
2. UI, `ConditionEvaluator.Evaluate(conditionType, snapshot, logger)` çağırır; bu, koşul örneklerini önbelleğe alır ve asla fırlatmaz.
3. `Unsupported` sonucu olan öğeler engellenmiş/desteklenmeyen durumda gösterilir (kullanıcı oturum için gizleyebilir). Zaten uygulanmış öğeler asla yeniden engellenmez.

---

<h1 id="the-refresh-scope-system">Yenileme Kapsamı Sistemi</h1>

Bir özelleştirme ayarı durum değiştirdiğinde, farklı Windows yüzeyleri farklı yenileme stratejilerine ihtiyaç duyar. `CustomizeRefreshScope` `[Flags]` enum'u bunu ayrıntılı olarak kontrol eder.

<h3 id="available-flags">Mevcut Bayraklar</h3>

| Üye | Değer | Etki | P/Invoke |
|---|---|---|---|
| `None` | `0` | Yenileme yok | — |
| `Settings` | `1 << 0` | Uygulamalar kayıt defterini yeniden okusun diye `WM_SETTINGCHANGE` yayınla | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE)` |
| `Associations` | `1 << 1` | Dosya ilişkilendirmelerinin veya simge önbelleğinin değiştiğini kabuğa bildir | `SHChangeNotify(SHCNE_ASSOCCHANGED)` |
| `Desktop` | `1 << 2` | Masaüstü simge listesinin (`SysListView32`) yeniden çizilmesini zorla | `LVM_REFRESH` + `LVM_UPDATE` |
| `Taskbar` | `1 << 3` | Görev çubuğu hedefli `WM_SETTINGCHANGE` ("TraySettings") yayınla | `SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, "TraySettings")` |
| `PolicyUpdate` | `1 << 4` | Kullanıcı başına parametreler için `SPIF_SENDCHANGE` ile `SystemParametersInfo` it | `SystemParametersInfo` |
| `Theme` | `1 << 5` | Tema/görsel ayarlar için `WM_THEMECHANGED` yayınla | `SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED)` |
| `DesktopIconCache` | `1 << 6` | HideIcons kayıt defterini değiştir + `WM_COMMAND 0x7402` masaüstüne gönder | Kayıt defteri okuma + `SendMessage(Progman, WM_COMMAND)` |

<h3 id="named-composites">Adlandırılmış Bileşimler</h3>

| Ad | Bileşim | Kullanım |
|---|---|---|
| `Default` | `Settings \| Associations` | Genel gezgin düzeyi ayarlar |
| `DesktopIcons` | `Settings \| Desktop` | Bireysel masaüstü simgelerini göster/gizle (Bu PC, Geri Dönüşüm Kutusu) |
| `HideDesktopIcons` | `Settings \| DesktopIconCache` | Genel "Tüm masaüstü simgelerini gizle" anahtarı |
| `TaskbarSettings` | `Settings \| Taskbar` | Görev çubuğu hizalaması, widget'lar, görev görünümü, görevi sonlandır |
| `ExplorerView` | `Settings \| Associations \| PolicyUpdate` | Dosya uzantıları, gizli dosyalar, kompakt görünüm |

<h3 id="how-refresh-flows">Yenileme Akışı</h3>

```
Ayar anahtarı → BaseCustomizeSetting.ApplyAsync(value)
  ├─ RegistryToggles'ı yazar (varsa) veya seçilen seçeneğin bağlamalarını uygular
  ├─ NeedsPostAction'ı kontrol eder (RefreshScope != None ise true)
  └─ Task.Run → ExecutePostActionAsync()
       ├─ Her CustomizeRefreshScope bayrağını kontrol eder
       ├─ SystemRefreshService metotlarını (P/Invoke) çağırır
       └─ Win32 bildirimlerini Windows'a gönderir
```

`ApplyAsync`'i geçersiz kılarsanız, **kendiniz** `await ExecutePostActionAsync()` çağırmalısınız (yukarıdaki örneklere bakın). Taban sınıf bunu yalnızca varsayılan `RegistryToggles` tabanlı ve açılır menü bağlama tabanlı uygulama için otomatik yapar.

---

<h1 id="building-new-features">Yeni Özellikler Geliştirme</h1>

Yeni bir sayfa veya araç eklemek istiyorsanız (örn. bir "Ağ Monitörü"):

1. **Önce bir GitHub Sorunu açın** — özelliği, kullanım durumunu ve tasarımı açıklayın. Bakımcı geri bildirimini bekleyin.
2. **Uygulama sırası**:

```csharp
// 1. Hizmet katmanı: Services/UI veya Services/System/YourService.cs
public class YourService(ILogger<YourService> logger) { ... }

// 2. ViewModel: UI/ViewModels/Pages/YourViewModel.cs
//    ViewModel'i genişletir (ObservableValidator + INavigationAware)

// 3. XAML Sayfası: UI/Pages/YourPage.xaml (+ kod arkası)

// 4. App.xaml.cs'e singleton olarak kaydet
services.AddSingleton<YourViewModel>();
services.AddSingleton<YourPage>();
```

- ViewModel'ler ve Sayfalar `App.xaml.cs` içinde **singleton olarak kaydedilmelidir**.
- Gezinme WPF UI (`INavigationService`) tarafından yönetilir.
- Mevcut desenleri izleyin — `DashboardPage`, `OptimizePage`, `BloatwarePage`, `DiskCleanupPage`, `ScheduledTasksPage`, `StartupManagerPage` vb.'ye bakın.

<h3 id="di-registration-pattern">DI Kayıt Deseni (App.xaml.cs'ten)</h3>

```csharp
// Sayfalar + ViewModel'ler — özellik başına bir çift
services.AddSingleton<DashboardViewModel>();
services.AddSingleton<DashboardPage>();

services.AddSingleton<OptimizeViewModel>();
services.AddSingleton<OptimizePage>();

services.AddSingleton<SettingsViewModel>();
services.AddSingleton<SettingsPage>();

services.AddSingleton<BloatwareViewModel>();
services.AddSingleton<BloatwarePage>();

services.AddSingleton<DiskCleanupViewModel>();
services.AddSingleton<DiskCleanupPage>();

services.AddSingleton<StartupManagerViewModel>();
services.AddSingleton<StartupManagerPage>();

services.AddSingleton<ScheduledTasksViewModel>();
services.AddSingleton<ScheduledTasksPage>();

// Customize
services.AddSingleton<CustomizeViewModel>();
services.AddSingleton<CustomizePage>();

// Otomatik sayfa kaydı (yansıma kullanan kategori sayfaları)
services.AddAllCustomizeCategoryPages();   // [CustomizeCategory] özniteliklerini tarar
services.AddAllOptimizationPages();        // [OptimizationCategory] özniteliklerini tarar

// Yöneticiler
services.AddSingleton<ConfigManager>();
services.AddSingleton<RevertManager>();

// Hizmetler
services.AddSingleton<OptimizationRegistry>();
services.AddSingleton<CustomizeRegistry>();
services.AddSingleton<OptimizationService>();
services.AddSingleton<BloatwareService>();
services.AddSingleton<DiskCleanupService>();
services.AddSingleton<StartupManagerService>();
services.AddSingleton<SystemInfoService>();
services.AddSingleton<StreamService>();
services.AddSingleton<UpdaterService>();
services.AddSingleton<IRegistryWatcher, RegistryWatcher>();
```

> Bu, oryantasyon için bir anlık görüntüdür — güncel kayıtlar için `App.xaml.cs` kaynaktır. Ayrıca başlangıç çağrılarına dikkat edin: `ShellService.Init(appOptionsMonitor)` ve `WmiHelper.Initialize()`.

<h3 id="system-services">Sistem Hizmetleri Referansı</h3>

| Hizmet | Amaç |
|---|---|
| `SystemInfoService` | `OptimizationContext` ve koşul sisteminin kullandığı `SystemSnapshot`'ı (CPU, RAM, GPU, OS, disk) sağlar. |
| `StreamService` | Uzak kaynakları (örn. güncellenmiş güç planı dosyaları) indirir. `OptimizationContext.StreamService` ile kullanılır. |
| `UpdaterService` | Güncellemeler için GitHub sürümlerini kontrol eder. Dashboard'da güncelleme istemi gösterir. |
| `RegistryWatcher` | Kayıt defteri anahtarlarını dış değişiklikler için izler ve UI'ya yenileme bildirir. `IRegistryWatcher` uygular. |
| `BloatwareService` | Önceden yüklü AppX paketlerini listeler, Güvenli/Dikkatli/Tehlikeli olarak sınıflandırır. |
| `DiskCleanupService` | Diskleri temizlik fırsatları için tarar (geçici dosyalar, önbellekler, günlükler). |
| `StartupManagerService` | Başlangıç uygulamalarını ve zamanlanmış görevleri listeler ve yönetir. |
| `ConditionEvaluator` | Uyumluluk koşullarını değerlendirmek için statik giriş noktası ([Koşul Sistemi](#the-condition-system) bölümüne bakın). |

---

<h1 id="revert-system">Geri Alma Sistemi</h1>

Uygulanan her optimizasyon `%localappdata%\optimizerDuck\Revert\{optimizationId}.json` konumunda bir JSON dosyası oluşturur.

<h3 id="how-it-works-tr">Nasıl Çalışır</h3>

```
ApplyAsync()
  │
  ├─ ExecutionScope.Begin(optimization, logger)    ← çevresel AsyncLocal kapsamı oluşturur
  │
  ├─ RegistryService.Write(...)                     ← RegistryRevertStep'i otomatik kaydeder
  ├─ ServiceProcessService.ChangeServiceStartupTypeAsync(...)  ← ServiceRevertStep'i otomatik kaydeder
  ├─ ShellService.CMDAsync(...)                     ← ShellRevertStep'i otomatik kaydeder
  │
  ├─ CompleteFromScope() → ApplyResult              ← kaydedilen adımlardan türetilir
  │
  └─ ExecutionScope dispose → RevertManager.SaveRevertDataAsync()
```

<h3 id="scope-variants-tr">Kapsam Varyantları</h3>

| Metot | Amaç |
|---|---|
| `ExecutionScope.Begin(optimization, logger)` | Gerçek bir uygulama için kalıcı kapsam oluşturur. |
| `ExecutionScope.BeginForLogging(logger)` | Yalnızca günlük — adımları kaydeder ama geri alma verisini asla kalıcılaştırmaz. |
| `ExecutionScope.BeginForCapture(logger)` | Yeniden deneme için: `OptimizationId = Guid.Empty` ile adımları yakalar, daha sonra gerçek kapsama yeniden atanır. |

<h3 id="step-types-tr">Adım Türleri</h3>

| Adım Türü | Kaydettiği | Otomatik Oluşturan |
|---|---|---|
| **`RegistryRevertStep`** | Değişiklikten önceki orijinal kayıt defteri değeri | `RegistryService.Write()`, `DeleteValue()`, `CreateSubKey()`, `DeleteSubKeyTree()` |
| **`ServiceRevertStep`** | Orijinal hizmet başlangıç türü | `ServiceProcessService.ChangeServiceStartupTypeAsync()` |
| **`ScheduledTaskRevertStep`** | Orijinal görev durumu (etkin/devre dışı) | `ScheduledTaskService.DisableTask()`, `EnableTask()` |
| **`ShellRevertStep`** | Değişikliği tersine çeviren kabuk komutu | `ShellService.CMDAsync()`, `PowerShellAsync()` — bir `revertCommand` parametresi geçirin |
| **`UsbPowerRevertStep`** | USB güç ayarları (cihaz başına) | USB ile ilgili optimizasyonlar (elle `ExecutionScope.RecordStep()` ile) |

<h3 id="revert-command">Kabuk Çağrılarına Geri Alma Komutu Ekleme</h3>

`CMDAsync` veya `PowerShellAsync` çağırırken, geri alma için saklanan `revertCommand` parametresini isteğe bağlı geçebilirsiniz:

```csharp
// "powercfg /h on" bu değişikliği tersine çevirmek için saklanır
await ShellService.CMDAsync("powercfg /h off", "powercfg /h on");
```

<h3 id="revert-data-format-tr">Geri Alma Veri Formatı</h3>

```json
{
  "SchemaVersion": 1,
  "OptimizationId": "guid",
  "OptimizationName": "DisableTelemetry",
  "AppliedAt": "2026-06-02T12:00:00Z",
  "Steps": [
    { "Index": 0, "Type": "Registry", "Data": { "..." } },
    null,                    // null boşluğu = bu dizindeki başarısız adım
    { "Index": 2, "Type": "Service", "Data": { "..." } }
  ]
}
```

<h3 id="key-details-tr">Temel Detaylar</h3>

- **Uygulanma durumu**, diskte dosya varlığından çıkarılır (`RevertManager.IsAppliedAsync(id)`).
- **Atomik yazma**: `.tmp` dosyasına yazar, sonra `File.Replace()` — çökme güvenli.
- **Eşzamanlı erişim**: dosya başına `SemaphoreSlim` kilitleri yarış koşullarını önler; 30 saniye zaman aşımı.
- **`ExecutionScope`** çevresel adım takibi için `AsyncLocal<ExecutionScope?>` kullanır. Parametrelerle bağlam geçirmeye gerek yok.
- **Geri alma adımları ters sırada çalışır** (en son uygulanan = ilk geri alınan).
- **Kısmi başarı**: bazı adımlar başarısız olsa bile geri alma devam eder. Başarısız adımlara yeniden deneme eylemleri kaydedilir.
- **Yeniden deneme**: `OptimizationService.RetryFailedStepsAsync()` tek tek başarısız adımları yeniden deneyebilir; `RecordStepAtIndex()` orijinal dizin düzenini korur.
- **Upsert**: `RevertManager.UpsertRevertStepAtIndexAsync()` belirli dizinlerdeki geri alma adımlarını ekleyebilir/değiştirebilir (yeniden deneme sırasında kullanılır).
- **Adım kaydı**: Geri alma adımı serisizleştirme yansıma tabanlı `_stepRegistry` kullanır — yeni adım türleri, `IRevertStep` uygulayıp statik bir `FromData(JObject)` metoduyla otomatik kaydolur.

> **Önemli**: Sağlayıcı hizmetleri (`RegistryService.Write`, `ShellService.CMDAsync` vb.) çağırdığınızda geri alma adımları otomatik kaydedilir. Özel bir sağlayıcı uygulamadığınız sürece (`UsbPowerRevertStep` gibi) geri alma adımlarını elle oluşturMAYIN.

---

<h1 id="testing">Test Etme</h1>

Testler **xUnit v3** kullanır ve gerçek I/O ile entegrasyon tarzı bir yaklaşım izler.

<h3 id="test-patterns-tr">Test Desenleri</h3>

| Desen | Detay |
|---|---|
| **Mock kütüphanesi yok** | Tüm test ikizleri arayüzleri uygulayan elle yazılmış sınıflardır |
| **Gerçek I/O** | Gerçek dosya sistemi (geri alma JSON dosyaları), gerçek kayıt defteri (`HKCU\Software\TestOptimizerDuck*`), gerçek süreç yürütme (CMD, PowerShell) |
| **Temizlik** | Test artefakt temizliği için `try/finally` veya `IDisposable` kullanın |
| **Adlandırma** | `{Method}_{Scenario}_{ExpectedResult}` — örn. `ApplyAsync_Success_PersistsRevertDataFile` |
| **Günlük** | DI günlük parametreleri için `NullLogger<T>.Instance` / `NullLoggerFactory.Instance` kullanın |
| **STA iş parçacığı** | WPF bileşenleri içeren testler `RunInStaThreadAsync` yardımcısını (STA iş parçacığı + `TaskCompletionSource`) kullanmalıdır |

<h3 id="test-structure-tr">Test Yapısı</h3>

```
optimizerDuck.Test/
├── Common/Helpers/
├── Domain/
│   ├── Customize/
│   ├── Exceptions/
│   ├── Optimizations/
│   └── Revert/Steps/
└── Services/
    ├── Managers/
    ├── OptimizationServices/
    └── ApplyRevertComprehensiveTests.cs
```

Uygulamanın yapısını yansıtın: `Services/OptimizationServices/` testleri `Services/OptimizationServices/` içine, etki alanı modeli testleri eşleşen `Domain/` alt dizinine gider.

<h3 id="running-tests-tr">Testleri Çalıştırma</h3>

```bash
# Derlemeden sonra
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

# Derleme + testi tek adımda
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release

# Ada göre tek bir test çalıştır
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build --filter "FullyQualifiedName~TestName"
```

<h3 id="ci-integration-tr">CI Entegrasyonu</h3>

CI hattı (`.github/workflows/ci.yml`) şunları çalıştırır:

```bash
dotnet restore optimizerDuck.slnx
dotnet build optimizerDuck.slnx --configuration Release --no-restore
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build --blame-hang --blame-hang-timeout 30s
```

`--blame-hang --blame-hang-timeout 30s` bayrakları, testlerin 30 saniyeden uzun süre asılı kalmamasını sağlar; bu, gerçek Windows hizmetleriyle etkileşime giren entegrasyon tarzı testler için kritiktir.

<h3 id="writing-tests-tr">Sağlayıcı Hizmetler için Test Yazma</h3>

```csharp
public class MyOptimizationTests
{
    [Fact]
    public async Task ApplyAsync_Success_PersistsRevertDataFile()
    {
        var optimization = new TestOptimization
        {
            ApplyImpl = _ =>
            {
                ExecutionScope.RecordStep("Test", "Step 1", true);
                return Task.FromResult(ApplyResult.True());
            },
        };

        var service = CreateService();
        var result = await service.ApplyAsync(optimization, new Progress<ProcessingProgress>());

        Assert.Equal(OptimizationSuccessResult.Success, result.Status);
    }

    private static OptimizationService CreateService()
    {
        return new OptimizationService(
            new RevertManager(NullLogger<RevertManager>.Instance, NullLoggerFactory.Instance),
            NullLoggerFactory.Instance,
            new SystemInfoService(NullLogger<SystemInfoService>.Instance),
            new StreamService(NullLogger<StreamService>.Instance),
            null!,
            NullLogger<OptimizationService>.Instance
        );
    }
}
```

---

<h1 id="coding-standards">Kodlama Standartları</h1>

<h3 id="language-features-tr">Dil Özellikleri</h3>

| Özellik | Kullanım | Notlar |
|---|---|---|
| Dosya kapsamlı ad alanları | Evet | `namespace X.Y;` |
| Koleksiyon ifadeleri | Evet | Boş için `[]`, listeler için `[item1, item2]` |
| Birincil yapıcılar | Evet | Hizmetlerde ve basit tiplerde kullanılır |
| Örtük using'ler | Evet | `.csproj` içinde etkin |
| Null atanabilir referans tipleri | Evet | `<Nullable>enable</Nullable>` — null'ları düzgün işleyin |
| Uzantı metotları (`extension(T type)`) | Evet | C# 13 özelliği, `OptimizationTagsToDisplay` içinde kullanılır |

<h3 id="naming-conventions-tr">Adlandırma Kuralları</h3>

| Öğe | Kural | Örnek |
|---|---|---|
| Sınıflar, enum'lar, arayüzler, metotlar, özellikler | `PascalCase` | `RegistryService`, `ApplyAsync` |
| Özel alanlar | `_camelCase` | `_lastError` |
| Yerel değişkenler, parametreler | `camelCase` | `progress`, `serviceName` |
| Asenkron metotlar | `*Async` eki | `ChangeServiceStartupTypeAsync`, `CMDAsync` |
| Sabitler | `PascalCase` / `_PascalCase` | `MaxRetries` / `_defaultTimeout` |

<h3 id="formatting-tr">Biçimlendirme</h3>

| Ayar | Değer |
|---|---|
| Girinti | 4 boşluk (sekme yok) |
| Satır sonu | LF |
| Kodlama | UTF-8 |
| Maksimum satır uzunluğu | 100 karakter |
| Sondaki boşluk | Kırpılır |
| Sondaki yeni satır | Zorunlu |
| Biçimlendirici | **CSharpier** — commit öncesi `dotnet csharpier .` çalıştırın |
| CA1416 | `.editorconfig` ile susturulur — tüm kod Windows'a özeldir |

<h3 id="code-style-tr">Kod Stili</h3>

- **Sabit kodlanmış dize yok** — her zaman `Translations.KeyName` veya `Loc.Instance["Key"]`
- **Yorumları seyrek tutun** — mevcut kodda neredeyse hiç yok. Gereksiz yorum eklemeyin.
- **Yeni bağımlılıklar yerine mevcut kütüphaneleri tercih edin**.
- **Büyük yeniden düzenlemeler yerine küçük, odaklı değişiklikleri tercih edin**.
- **Makineye özgü yolları veya sırları asla commit etmeyin**.

<h3 id="dependency-injection-tr">Bağımlılık Enjeksiyonu</h3>

- Hizmetler, ViewModel'ler ve Sayfalar `App.xaml.cs` içinde singleton olarak kaydedilir.
- Yapıcı enjeksiyonu kullanın: `public class Foo(Bar bar, Baz baz)` veya `public class Foo(ILogger<Foo> logger)`.
- Statik sağlayıcı hizmetler (`RegistryService`, `ShellService`, `ScheduledTaskService`, `ServiceProcessService`) enjekte EDİLMEZ — doğrudan erişin.
- Test ikizleri elle yazılır (mock kütüphanesi yok).

<h3 id="error-handling-tr">Hata İşleme</h3>

| Katman | Uygulama |
|---|---|
| **Optimizasyonlar** | Fırlatmak yerine `ApplyResult.False("reason")` döndürün. Adım düzeyi başarısızlık takibini `ExecutionScope`'a bırakın. |
| **Sağlayıcı hizmetler** | Sistem çağrılarını try/catch ile sarın, hataları günlükleyin. Başarısız adımları yeniden deneme eylemleriyle kaydedin. |
| **ViewModel'ler** | Komut işleyicilerinde istisnaları yakalayın, kullanıcı dostu snackbar'lar gösterin. |
| **Koşullar** | `ConditionResult.Error()` döndürün veya fırlatın — `ConditionEvaluator` yakalar ve `Error`'a (asla engellemez) eşler. |
| **Yapmayın** | İşleyemeyeceğiniz istisnaları yakalamayın. Tüm istisnaları sessizce yutmayın. |

<h3 id="global-error-handling-tr">Genel Hata İşleme</h3>

`App.xaml.cs` üç genel istisna işleyicisi kaydeder:

- `AppDomain.CurrentDomain.UnhandledException` — ölümcül istisnaları yakalar
- `TaskScheduler.UnobservedTaskException` — gözlemlenmemiş görev istisnalarını yakalar
- `DispatcherUnhandledException` — işlenmemiş UI iş parçacığı istisnalarını yakalar

Tüm çökme ayrıntıları `%localappdata%\optimizerDuck\Crashes\crash_*.log` konumuna kaydedilir.

---

<h1 id="localization">Yerelleştirme</h1>

<h3 id="resx-files-tr">RESX Dosyaları</h3>

Kullanıcıya dönük tüm dizeler `Resources/Languages/Translations.resx` (İngilizce varsayılan) içinde bulunur. C# içinde güçlü tipli `Translations` sınıfını, dinamik arama için `Loc.Instance["Key"]` kullanın.

- `Translations.Designer.cs`'i **doğrudan düzenlemeyin** — otomatik oluşturulur.
- [ResXManager](https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager) (VS) veya Rider'ın yerleşik kaynak düzenleyicisini kullanın.
- `{0}`, `{1}` gibi biçim parametrelerini tam olarak koruyun.
- Dizeleri kısa tutun — bazı UI kartlarının genişlik sınırları vardır.

<h3 id="available-locales-tr">Mevcut Yerel Ayar</h3>

Uygulama **15'ten fazla dil** ile gelir ve liste büyümeye devam eder. Burada listelemek (bayatlayacağı için) yerine şunlara bakın:

- **Yerel ayar dosyalarının kendisi**: `optimizerDuck/Resources/Languages/` — her dil için bir `Translations.{locale}.resx`, İngilizce varsayılan olarak da `Translations.resx`.
- **Kayıt listesi**: `UI/ViewModels/Pages/SettingsViewModel.cs` içindeki `Languages` — UI'da gösterilen dillerin yetkili listesidir.

<h3 id="adding-a-new-language-tr">Yeni Dil Ekleme</h3>

1. `Translations.{locale}.resx` (örn. `Translations.de-DE.resx`) dosyasını, `Translations.resx` ile aynı tüm anahtarlarla oluşturun.
2. Dili `UI/ViewModels/Pages/SettingsViewModel.cs` içine kaydedin:

```csharp
new() { DisplayName = "Deutsch", Culture = new CultureInfo("de-DE") },
```

<h3 id="hardcoded-string-rule-tr">Sabit Dize Kuralı</h3>

**Asla dize sabit kodlamayın**. Her zaman şunları kullanın:

```csharp
// Güçlü tipli (önerilir)
string title = Translations.Features_Desktop_Name;

// Biçim argümanlarıyla
string msg = string.Format(Translations.Dashboard_SystemInfo_Storage_DiskInfo, used, total, percent);

// Dinamik anahtar arama (kural tabanlı anahtarlar için)
string title = Loc.Instance[$"Optimizer.{category}.{key}.Name"];
```

XAML içinde:

```xml
<!-- Argümansız -->
<ui:TextBlock Text="{ext:Loc Dashboard.Header.Title}" />

<!-- Bağlı argümanlarla -->
<ui:TextBlock Text="{ext:Loc Dashboard.UpdateInfoBar.Message, {Binding ViewModel.LatestVersion}}" />
```

---

<h1 id="pull-request-process">Çekme İsteği Süreci</h1>

1. **`master`'dan dallanın** — asla doğrudan master üzerinde çalışmayın:

   ```bash
   git checkout -b feature/sizin-ozellik-adiniz
   # veya
   git checkout -b fix/sorun-numarasi
   ```

2. **Conventional Commits ile commit edin**:

   | Önek | Ne Zaman Kullanılır |
   |---|---|
   | `feat:` | Yeni optimizasyonlar veya özellikler |
   | `fix:` | Hata düzeltmeleri |
   | `refactor:` | Davranış değişikliği olmadan kod yeniden yapılandırma |
   | `docs:` | Dokümantasyon güncellemeleri |
   | `test:` | Test ekleme veya düzeltme |
   | `i18n:` | Çeviri güncellemeleri |
   | `chore:` | Bakım, derleme yapılandırması, bağımlılıklar |

3. **İtmeden önce doğrulayın**:

   ```bash
   # 1. Derle
   dotnet build optimizerDuck.slnx --configuration Release

   # 2. Test
   dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build

   # 3. Biçimlendir
   dotnet csharpier .

   # 4. git status'u kontrol edin — yalnızca amaçlanan dosyaların aşamalandığından emin olun
   git status
   git diff --cached
   ```

4. **PR'ı açın**:
   - **Neyin** değiştiğini ve **neden** değiştiğini açıklayın.
   - UI değişikliği içeren PR'lar **bir ekran görüntüsü içermelidir**.
   - İlgili sorunları bağlayın: `Closes #42`.
   - Hâlâ devam eden bir çalışmaysa taslak olarak işaretleyin.

5. **İnceleme**: Bir bakımcı inceleyecektir. Geri bildirime açık olun ve hızlı yanıt verin.

<h3 id="pr-checklist-tr">PR Kontrol Listesi</h3>

- [ ] Kod mevcut desenleri izliyor (keşif, öznitelikler, asenkron adlandırma)
- [ ] Yerelleştirme anahtarları en azından `Translations.resx` içine eklendi
- [ ] İlgiliyse koşullar bildirildi ([Koşul Sistemi](#the-condition-system) bölümüne bakın)
- [ ] `dotnet build` başarılı (0 hata)
- [ ] `dotnet test` geçiyor (tüm testler yeşil)
- [ ] `dotnet csharpier .` çalıştırıldı
- [ ] Sabit kodlanmış dize yok
- [ ] Geri alma adımları düzgün kaydedildi (geçerliyse)
- [ ] UI değişiklikleri bir ekran görüntüsü içeriyor

---

<h1 id="issue-guidelines">Sorun (Issue) Yönergeleri</h1>

- **Hata raporları**: Hata Raporu şablonunu kullanın. Yeniden üretme adımlarını, beklenen ve gerçekleşen davranışı, `%localappdata%\optimizerDuck\optimizerDuck.log` günlüklerini + sistem özelliklerini ekleyin.
- **Özellik istekleri**: Kullanım durumunu, çözdüğü sorunu ve nasıl çalışması gerektiğini açıklayın.
- **Optimizasyon önerileri**: Kayıt defteri yollarını, hizmet adlarını veya CLI komutlarını ekleyin. Dokümantasyona veya güvenilir kaynaklara bağlantı verin.
- **Sorular**: GitHub Discussions'ı kullanın veya [Discord](https://discord.gg/tDUBDCYw9Q) sunucusuna katılın.

---

<h1 id="faq--troubleshooting">SSS ve Sorun Giderme</h1>

<h3>Derleme "CA1416" hatalarıyla başarısız oluyor</h3>

`.editorconfig` CA1416'yı susturur. Hâlâ görüyorsanız, master'dan en son `.editorconfig`'e sahip olduğunuzdan emin olun. Bu proje Windows'a özeldir — `SupportedOSPlatform` korumaları eklemeyin.

<h3>Optimizasyonum UI'da görünmüyor</h3>

Kontrol listesi:

- Kategori sınıfı içinde **iç içe public bir sınıf** mı?
- Kategori sınıfı `IOptimizationCategory` uyguluyor mu?
- Optimizasyon sınıfı `BaseOptimization`'dan mı türüyor?
- Bir `[Optimization(Id = "...", ...)]` özniteliği var mı?
- Yerelleştirme anahtarları `Translations.resx` içine eklendi mi?
- Optimizasyon kategorisi önceden yüklendi mi? (`OptimizationRegistry.IsPreloaded`'ı kontrol edin)
- Bir `Condition` onu engelliyor mu? (Bir sonraki soruya bakın)

<h3>Optimizasyonum/ayarım "desteklenmiyor" (engelli) olarak görünüyor</h3>

- Öznitelikte bildirilen `Condition = typeof(...)` değerini kontrol edin.
- Koşul türünün `ICondition` uyguladığını, somut olduğunu ve public parametresiz yapıcıya sahip olduğunu doğrulayın (`ConditionValidation` bunu başlangıçta zorlar).
- Öğenin yalnızca **uygulanmadığında** engellendiğini unutmayın; uygulanan öğeler her zaman normal kartlarını gösterir.

<h3>Özelleştirme ayarım görünmüyor</h3>

- `[CustomizeSetting(Section = ..., Icon = ...)]` var mı? (`Icon` zorunludur.)
- `Section` değeri doğru yazıldı mı?
- Kategori sınıfı doğru `[CustomizeCategory(PageType = ...)]` özniteliğini mi kullanıyor?
- Bir `Condition` onu engelliyor mu?

<h3>Testten sonra geri alma veri dosyası yok</h3>

Geri alma verisini kontrol eden testler `%localappdata%\optimizerDuck\Revert\` içindeki dosyaları bekler. Test temizliği `finally` bloklarında çalışır — önermelerin (assertions) temizlikten önce çalıştığından emin olun.

<h3>Optimizasyon uygularken UI donuyor</h3>

`ApplyAsync`'in, asenkron olan tüm sağlayıcı çağrıları (`ChangeServiceStartupTypeAsync`, `CMDAsync`, `PowerShellAsync`) için `async`/`await` kullandığından emin olun. `Task.FromResult` kullanıyor veya `.Result` / `.Wait()` ile blokluyorsanız UI iş parçacığı donar.

<h3>GUID nasıl üretirim?</h3>

```powershell
# PowerShell
[guid]::NewGuid()
```

```bash
# Komut satırı (uuidgen mevcutsa)
uuidgen
```

<h3>Çeviriler UI'da anahtar adları olarak görünüyor</h3>

`Translations.resx` içine yerelleştirme anahtarlarını eklemeyi kaçırdınız. Beklenen anahtar desenleri için [Yerelleştirme](#localization) bölümüne bakın.

<h3>Geri alırken "No revert data" hatası</h3>

Optimizasyonun `Id` GUID'inin değişmediğini kontrol edin. Geri alma dosyaları `Id` ile anahtarlanır. GUID'i yeniden üretirseniz, daha önce uygulanan optimizasyonların eşleşen geri alma dosyaları kalmaz.

<h3>Yeni bir geri alma adımı türü nasıl eklerim?</h3>

1. `Domain/Revert/Steps/` içinde `IRevertStep` uygulayan yeni bir sınıf oluşturun.
2. Serisizleştirme için statik bir `FromData(JObject data)` metodu ekleyin.
3. `RevertManager`'ın yansıma tabanlı `_stepRegistry`'si onu otomatik keşfeder.
4. `ExecutionScope.RecordStep()` ile `revertStep` parametresi olarak kaydedin.

<h3>Uygulama çökme güvenliğini nasıl ele alır?</h3>

- Geri alma dosyaları atomik yazma kullanır (`.tmp` + `File.Replace`).
- Çökme günlükleri `%localappdata%\optimizerDuck\Crashes\crash_*.log` konumuna yazılır.
- `WmiHelper.Initialize()` başlangıçta anormal sonlanma için WMI temizliği kaydeder.
- `App.xaml.cs` 3 genel istisna işleyicisi kaydeder.

---

<div align="center">

<h2 id="credits">Krediler</h2>

Birleştirilmiş PR'ları olan katkıda bulunanlar sürüm notlarında listelenir. Bir modüle önemli ölçüde katkıda bulunursanız, dosya başlığının üstüne bir yazar etiketi ekleyebilirsiniz.

---

<h2 id="license">Lisans</h2>

optimizerDuck'a katkıda bulunarak, katkılarınızın projenin [GPL v3 Lisansı](../../LICENSE) altında lisanslanacağını kabul etmiş olursunuz.

---

<p><i>optimizerDuck'ı daha iyi hale getirdiğiniz için teşekkürler.</i></p>

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
