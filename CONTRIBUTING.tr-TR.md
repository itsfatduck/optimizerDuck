<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Afişi" title="optimizerDuck"/></a>

[English](CONTRIBUTING.md) | [日本語](CONTRIBUTING.ja-JP.md) | **Türkçe**

[Giriş](#giriş) • [Başlangıç](#başlangıç) • [Mimariye Genel Bakış](#mimariye-genel-bakış) • [Katkıda Bulunma Yolları](#katkıda-bulunma-yolları) • [Optimizasyon Oluşturma](#optimizasyon-oluşturma) • [Özelleştirme Ayarı Oluşturma](#özelleştirme-ayarı-oluşturma) • [Yenileme Kapsamı Sistemi](#yenileme-kapsamı-sistemi) • [Yeni Özellikler Geliştirme](#yeni-özellikler-geliştirme) • [Geri Alma Sistemi](#geri-alma-sistemi) • [Test Etme](#test-etme) • [Kodlama Standartları](#kodlama-standartları) • [Yerelleştirme](#yerelleştirme) • [Çekme İsteği Süreci](#çekme-isteği-süreci) • [Sorun (Issue) Yönergeleri](#sorun-issue-yönergeleri) • [SSS ve Sorun Giderme](#sss-ve-sorun-giderme) • [Lisans](#lisans)

</div>

---

# Giriş

.NET 10 üzerinde WPF ile oluşturulmuş ücretsiz, açık kaynaklı bir Windows optimizasyon aracı olan **optimizerDuck**'a katkıda bulunduğunuz için teşekkür ederiz.

Birçok şekilde yardımcı olabilirsiniz:
- Açık yeniden üretme adımlarıyla hataları bildirmek
- Yeni optimizasyonlar veya özellikler önermek (önce bir sorun açın)
- Dokümantasyonu ve kılavuzları geliştirmek
- Çeviriler eklemek veya düzeltmek
- Kod katkısında bulunmak: optimizasyonlar, özelleştirme ayarları, hizmetler, kullanıcı arayüzü iyileştirmeleri
- Test eklemek veya mevcut testleri gözden geçirmek

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

Çözüm, `.slnx` formatını kullanır (XML tabanlı çözüm dosyası, `.sln` değil).

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

### 5. Hızlı Başlangıç Kontrol Listesi

İlk katkınızdan önce:

- [ ] Depoyu forkladınız ve klonladınız
- [ ] `dotnet build` başarılı oldu (0 hata)
- [ ] `dotnet test` geçer (tüm testler yeşil)
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
│   ├── app.manifest                        # requireAdministrator UAC seviyesi
│   │
│   ├── Domain/                             # Saf modeller, arayüzler, özellikler (WPF bağımlılığı yok)
│   │   ├── Abstractions/                   # IOptimization, ICustomizeSetting, IRevertStep, IWindow, ICustomizeCategory, IOptimizationCategory
│   │   ├── Attributes/                     # [Optimization], [CustomizeSetting], [OptimizationCategory], [CustomizeCategory]
│   │   ├── Configuration/                  # AppSettings modeli
│   │   ├── Execution/                      # ExecutionScope — AsyncLocal ile çevresel adım takibi
│   │   ├── Customize/                      # Özelleştirme ayarları (Desktop, Gaming, Preferences, System)
│   │   │   ├── Categories/                 # İçi içe ayar sınıflarına sahip kategori sınıfları
│   │   │   └── Models/                     # BaseCustomizeSetting, RegistryToggle, RefreshScope, SettingOption, RecommendationState
│   │   ├── Optimizations/                  # Optimizasyonlar (Performance, Privacy, GPU, vb.)
│   │   │   ├── Categories/                 # İçi içe optimizasyon sınıflarına sahip kategori sınıfları
│   │   │   └── Models/                     # BaseOptimization, ApplyResult, OptimizationContext, ServiceItem, RegistryItem
│   │   ├── Revert/                         # RevertData, RevertResult, geri alma adımı türleri
│   │   │   └── Steps/                      # RegistryRevertStep, ServiceRevertStep, ScheduledTaskRevertStep, ShellRevertStep, UsbPowerRevertStep
│   │   └── UI/                             # Enumlar: OptimizationRisk, OptimizationTags, OptimizationCategoryOrder, CustomizeOrder, LanguageOption, OptimizationState
│   │
│   ├── Common/                             # Ortak yardımcılar, eklentiler, dönüştürücüler
│   │   ├── Converters/                     # 20+ WPF değer dönüştürücüsü
│   │   ├── Extensions/                     # StringExtensions, CustomizePageRegistryExtensions, OptimizationPageRegistryExtensions
│   │   ├── Helpers/                        # Shared.cs, ReflectionHelper.cs, SystemRefreshService.cs, EmbeddedResourceHelper.cs, WmiHelper.cs
│   │   └── Native/                         # Yerel işletim sistemi yardımcıları
│   │
│   ├── Services/                           # İş mantığı katmanı
│   │   ├── Configuration/                  # ConfigManager, LanguageManager
│   │   ├── Customize/                      # CustomizeRegistry (yansıma tabanlı keşif)
│   │   ├── Optimization/                   # OptimizationRegistry, OptimizationService
│   │   │   └── Providers/                  # Statik: RegistryService, ShellService, ScheduledTaskService, ServiceProcessService
│   │   ├── Revert/                         # RevertManager (atomik JSON okuma/yazma)
│   │   ├── System/                         # RegistryWatcher, StreamService, SystemInfoService, UpdaterService
│   │   └── UI/                             # BloatwareService, DiskCleanupService, StartupManagerService
│   │
│   ├── UI/                                 # WPF sayfaları, ViewModel'ler, kontroller, stiller
│   │   ├── Behaviors/                      # SmoothScrollBehavior
│   │   ├── Controls/                       # FilledNavigationViewItem
│   │   ├── Dialogs/                        # ProcessingDialog, OptimizationDetailsDialog, RestorePointDialog, LegalDialog, BloatwareConfirmationDialog, ScheduledTaskCreateDialog
│   │   ├── Pages/                          # Dashboard, Optimize, Customize, Settings, Bloatware, DiskCleanup, StartupManager, ScheduledTasks
│   │   ├── Styles/                         # FluentDesign.xaml, NavigationViewOverride.xaml
│   │   ├── ViewModels/                     # Sayfa ve diyalog ViewModel'leri
│   │   └── Windows/                        # MainWindow
│   │
│   └── Resources/                          # Resimler, gömülü dosyalar, yerelleştirme
│       ├── Embedded/                       # Güç planları, simgeler
│       ├── Images/                         # Duck.png, logolar
│       └── Languages/                      # Translations.resx + 11 dil varyantı
│
└── optimizerDuck.Test/                     # xUnit v3 test projesi
```

### Önemli Tasarım Kararları

| Karar | Gerekçesi |
|---|---|
| **Yansıma tabanlı keşif** | Güncellenecek DI kayıt dizileri yok. `ReflectionHelper.FindImplementationsInLoadedAssemblies<T>()` başlangıçta `optimizerDuck.*` derlemelerini tarar. Yeni optimizasyonlar/ayarlar otomatik keşfedilir. |
| **Statik sağlayıcı hizmetler** | `RegistryService`, `ShellService`, `ScheduledTaskService`, `ServiceProcessService` statik sınıflardır. Geri alma adımlarını `ExecutionScope`'a otomatik kaydederler. |
| **Dosya tabanlı geri alma takibi** | Uygulanan durum = diskte dosyanın var olması (`%localappdata%\optimizerDuck\Revert\{id}.json`). Veritabanı yok. Atomik yazmalar `File.Replace()` ile yapılır. |
| **Entegrasyon tarzı testler** | Gerçek dosya sistemi, gerçek kayıt defteri, gerçek işlem yürütme. Mock kütüphanesi yok — elle yazılmış test double'ları. |
| **Zaman uyumsuz servis metotları** | Harici işlemleri yürüten yöntemler asenkrondur (`*Async` son eki). |
| **Statik WMI yardımcısı** | `WmiHelper.Initialize()` başlangıçta çalışır, anormal sonlanma için WMI temizleme işleyicilerini kaydeder. |

---

# Katkıda Bulunma Yolları

| Katkı Türü | Açıklama | Nereden Başlamalı |
|---|---|---|
| **Yeni Optimizasyonlar** | Kayıt defteri ayarları, servis değişiklikleri, sistem ayarları | `Domain/Optimizations/Categories/*.cs` |
| **Yeni Özelleştirme Ayarları** | Windows ayarları için kullanıcı arayüzü anahtarları | `Domain/Customize/Categories/*.cs` |
| **Yeni Uygulama Özellikleri** | Yeni sayfalar, araçlar veya işlevsellikler | Önce bir sorun (Issue) açın |
| **Hata Düzeltmeleri** | Çökme düzeltmeleri, mantık hataları, UI sorunları | Herhangi bir yer |
| **Çeviriler** | Yeni diller veya mevcut çevirileri düzeltme | `Resources/Languages/Translations.*.resx` |
| **Dokümantasyon** | README, CONTRIBUTING vb. | `*.md` dosyaları |
| **Test** | Yeni/mevcut testleri ekleme veya gözden geçirme | `optimizerDuck.Test/` |

---

# Optimizasyon Oluşturma

### Keşif Nasıl Çalışır

Başlangıçta:

1. `ReflectionHelper.FindImplementationsInLoadedAssemblies<IOptimizationCategory>()` tüm `optimizerDuck.*` derlemelerini tarar.
2. `IOptimizationCategory` uygulayan her sınıfı bulur.
3. Her kategori için, `IOptimization` uygulayan **iç içe geçmiş public sınıfları** tarar.
4. Keşfedilen tüm optimizasyonlar örneklenir ve `OwnerType` otomatik olarak atanır.
5. `OptimizationService.UpdateOptimizationStateAsync` diskteki geri alma dosyalarını tarar.
6. `OptimizationRegistry.StartPreload()` bunu başlangıçta arka planda çalıştırır.

**Sizin göreviniz**: Bir kategorinin içine yerleştirilmiş bir sınıf oluşturun, `BaseOptimization`'dan türetin ve `[Optimization]` ile işaretleyin. Hepsi bu kadar.

### Optimizasyon Kategorileri

Mevcut kategoriler (`Domain/Optimizations/Categories/` içinde):

| Dosya | Öznitelik | Odak Noktası |
|---|---|---|
| `Performance.cs` | `[OptimizationCategory(typeof(PerformanceOptimizerPage))]` | RAM ayarı, süreç önceliği, klavye gecikmesi, multimedya zamanlayıcısı, erişilebilirlik kısayol tuşları |
| `SecurityAndPrivacy.cs` | `[OptimizationCategory(typeof(SecurityAndPrivacyOptimizerPage))]` | Telemetri, hata bildirimleri, reklam kimliği, konum, Cortana, Copilot, içerik dağıtım yöneticisi, etkinlik geçmişi |
| `Gpu.cs` | `[OptimizationCategory(typeof(GpuOptimizerPage))]` | AMD/NVIDIA/Intel kayıt defteri ayarları, güç durumları, clock gating, ASPM, async flips |
| `PowerManagement.cs` | `[OptimizationCategory(typeof(PowerManagementOptimizerPage))]` | Hazırda bekletme, hızlı başlatma, USB seçmeli askıya alma, özel güç planı kurulumu |
| `BloatwareAndServices.cs` | `[OptimizationCategory(typeof(BloatwareAndServicesOptimizerPage))]` | OEM uygulama engellemesi, 170+ Windows servis başlatma türü optimizasyonu |
| `UserExperience.cs` | `[OptimizationCategory(typeof(UserExperienceOptimizerPage))]` | Menü gecikmeleri, görsel efektler, görev çubuğu animasyonları, şeffaflık, Başlat Menüsü web araması |

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

            // 2. Async işlemleri await ile bekleyin
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
| **`Id` yeni bir GUID olmalıdır** | Geri alma dosyası adlandırmasında kullanılır. PowerShell'de `[guid]::NewGuid()` ile oluşturun. |
| **`BaseOptimization`'ı genişletin** | `Name`, `ShortDescription`, `RiskVisual`, `TagDisplays`'i otomatik sağlar |
| **`async Task<ApplyResult>` kullanın** | Servis sağlayıcıları asenkrondur — `await` kullanın |
| **`CompleteFromScope()` döndürün** | `ApplyResult`'u kaydedilen adımlardan türetir |
| **İlerlemeyi raporlayın** | `progress.Report(new ProcessingProgress { ... })` |
| **Tüm istisnaları yakalamayın** | `ExecutionScope` başarı/başarısızlığı izler |
| **Manuel geri alma adımı oluşturmayın** | Statik sağlayıcılar bunu `ExecutionScope.RecordStep()` ile otomatik yapar |
| **`context.Logger` kullanın** | Tanılama bilgilerini kaydetmek için |
| **`context.Snapshot`'ı kontrol edin** | `SystemSnapshot` RAM, GPU, CPU bilgisi sağlar |

### Mevcut Servis Sağlayıcıları

| Servis | Önemli Metotlar | Açıklama |
|---|---|---|
| **`RegistryService`** | `Write()`, `Read<T>()`, `DeleteValue()`, `CreateSubKey()`, `DeleteSubKeyTree()`, `KeyExists()` | Kayıt defteri okuma/yazma/silme. Orijinal değerleri yedekler. |
| **`ShellService`** | `CMDAsync()`, `PowerShellAsync()`, `CMD()`, `PowerShell()` | CMD/PowerShell komutları. `revertCommand` parametresi alabilir. |
| **`ScheduledTaskService`** | `DisableTask()`, `EnableTask()`, `IsTaskEnabled()`, `DeleteTask()` | Windows Zamanlanmış Görevlerini yönetir. |
| **`ServiceProcessService`** | `ChangeServiceStartupTypeAsync()`, `GetStartupTypeAsync()` | Windows Hizmetlerini yönetir. |

Kullanım örnekleri:

```csharp
// Çoklu kayıt defteri yazma
RegistryService.Write(
    new RegistryItem(@"HKLM\...", "Value1", 1),
    new RegistryItem(@"HKLM\...", "Value2", 0)
);

// Çoklu asenkron servis değişikliği
await ServiceProcessService.ChangeServiceStartupTypeAsync(
    new ServiceItem("DiagTrack", ServiceStartupType.Disabled),
    new ServiceItem("dmwappushservice", ServiceStartupType.Disabled)
);

// Geri alma komutu ile shell komutu
var result = await ShellService.CMDAsync(
    "powercfg /h off",
    "powercfg /h on"     // geri alma için saklanır
);
```

### Yeni Kategori Oluşturma ve Yardımcı Sınıflar

Yalnızca optimizasyonlarınız mevcut kategorilere uymuyorsa. Aşırı spesifik kategorilerden kaçının.

1. `Domain/Optimizations/Categories/YourCategory.cs` oluşturun
2. `IOptimizationCategory` uygulayın
3. `[OptimizationCategory(PageType = typeof(YourPage))]` ekleyin
4. `Domain/UI/OptimizationCategoryOrder.cs`'e yeni üye ekleyin
5. XAML sayfası `App.xaml.cs`'deki `services.AddAllOptimizationPages()` ile otomatik kaydedilir

Birden fazla optimizasyon aynı yapıyı paylaşıyorsa soyut bir ara sınıf oluşturun (örn. `Gpu.cs`'deki `GpuRegistryOptimization`).

### Yerelleştirme Anahtarları

Her optimizasyon `Translations.resx` içinde girdilere ihtiyaç duyar:

```
Optimizer.{KategoriAdı}.{OptimizasyonAnahtarı}.Name
Optimizer.{KategoriAdı}.{OptimizasyonAnahtarı}.ShortDescription
Optimizer.{KategoriAdı}.{OptimizasyonAnahtarı}.Progress.{ÖzelAnahtar}
Optimizer.{KategoriAdı}.{OptimizasyonAnahtarı}.Error.{ÖzelAnahtar}
```

> [!IMPORTANT]
> **Çeviriler zorunludur**. En azından `Translations.resx`'e (İngilizce) giriş ekleyin.

---

# Özelleştirme Ayarı Oluşturma

Özelleştirme ayarları, Windows ayarlarını AÇIK/KAPALI yapan UI kontrolleridir. `Domain/Customize/Categories/` dizininde bulunurlar.

### Özelleştirme Kategorileri

| Dosya | Öznitelik | Odak Noktası |
|---|---|---|
| `Desktop.cs` | `[CustomizeCategory(PageType = typeof(DesktopFeatureCategory))]` | Masaüstü simgeleri, kısayol okları |
| `Preferences.cs` | `[CustomizeCategory(PageType = typeof(PreferencesFeatureCategory))]` | Görev çubuğu, widget'lar, karanlık mod, dosya uzantıları, gizli dosyalar, pano geçmişi |
| `Gaming.cs` | `[CustomizeCategory(PageType = typeof(GamingFeatureCategory))]` | Oyun Modu, Game Bar, fare ivmesi, GPU zamanlaması |
| `SystemFeatures.cs` | `[CustomizeCategory(PageType = typeof(SystemFeatureCategory))]` | Num Lock, Geliştirici Modu, Uzun Yollar, pil yüzdesi |

### Basit Kayıt Defteri Anahtarı

```csharp
[CustomizeSetting(
    Section = nameof(Sections.Taskbar),
    Icon = SymbolRegular.AlignCenter24,
    Recommendation = RecommendationState.On
)]
public class TaskbarAlignment : BaseCustomizeSetting
{
    protected override IEnumerable<RegistryToggle> RegistryToggles =>
        [new()
        {
            Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
            Name = "TaskbarAl",
            OnValue = 0,
            OffValue = 1,
            DefaultValue = 1,
        }];

    protected override CustomizeRefreshScope RefreshScope =>
        CustomizeRefreshScope.TaskbarSettings;
}
```

### RegistryToggle Özellikleri

| Özellik | Tür | Varsayılan | Açıklama |
|---|---|---|---|
| `Path` | `string` | zorunlu | Tam kayıt defteri yolu |
| `Name` | `string` | zorunlu | Kayıt defteri değer adı |
| `OnValue` | `object?` | `1` | "Açık" durumu değeri |
| `OffValue` | `object?` | `0` | "Kapalı" durumu değeri |
| `DefaultValue` | `object?` | `0` | Anahtar yoksa kullanılacak değer |
| `IsOptional` | `bool` | `false` | Durum tespiti için gerekli değilse `true` |
| `ValueKind` | `RegistryValueKind` | `DWord` | Kayıt defteri değer türü |

### Kontrol Türleri

| Tür | Görüntü | Kullanım |
|---|---|---|
| `Toggle` | Aç/Kapat anahtarı | Çoğu ayar (varsayılan) |
| `Dropdown` | Açılır menü | Çoklu seçim |
| `Option` | Radyo düğmesi | Birbirini dışlayan seçenekler |
| `NumberInt` | Sayı girişi | Tamsayı değerleri |
| `NumberFloat` | Ondalık giriş | Hassas değerler |
| `String` | Metin girişi | Serbest metin |

### Özel Mantık (GetStateAsync / ApplyAsync)

```csharp
[CustomizeSetting(
    Section = nameof(Sections.Input),
    Icon = SymbolRegular.Cursor24,
    Recommendation = RecommendationState.Off
)]
public class MouseAcceleration : BaseCustomizeSetting
{
    private const string Path = @"HKCU\Control Panel\Mouse";

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
        await ExecutePostActionAsync();
    }

    protected override CustomizeRefreshScope RefreshScope => CustomizeRefreshScope.Default;
}
```

### Öneri Sistemi

Her özelleştirme ayarı bir öneri bildirebilir:

```csharp
[CustomizeSetting(
    ...,
    Recommendation = RecommendationState.On   // On / Off / Depends / Experimental / None
)]
```

| Durum | Anlamı |
|---|---|
| `On` | AÇILMASI önerilir — sistemi iyileştirir |
| `Off` | KAPATILMASI önerilir — sistemi iyileştirir |
| `Depends` | Kullanıcının ihtiyaçlarına bağlıdır |
| `Experimental` | Kararsız olabilir, dikkatli kullanın |
| `None` (varsayılan) | Öneri gösterilmez |

### Ne Zaman Ne Geçersiz Kılınır

| Senaryo | Geçersiz Kıl |
|---|---|
| Basit kayıt defteri anahtarı | `RegistryToggles` + `RefreshScope` |
| Çoklu kayıt defteri | `RegistryToggles` (hepsini listele) |
| Açılır menü/Seçenekler | `ControlType`, `Options`, `ApplyAsync`, `CurrentValue` |
| Çoklu değer mantığı | `GetStateAsync()` + `ApplyAsync()` + `GetWatchedRegistryPaths()` |
| Kayıt defteri olmayan ayar | `GetStateAsync()` + `ApplyAsync()` (tamamen özel) |
| Dinamik seçenekler | `Options` getter'ını koşullu yap |
| Yakınsama ile durum tespiti | `GetStateWithRetryAsync()` (yerleşik — geçersiz kılmayın) |
| Gömülü kaynak çıkarma | `EmbeddedResourceHelper.TryExtract()` içinde `ApplyAsync` |

### Yerelleştirme Anahtarları (Özelleştirme)

```
Customize.{KategoriAdı}.{AyarAnahtarı}.Name
Customize.{KategoriAdı}.{AyarAnahtarı}.Description
Customize.{KategoriAdı}.{AyarAnahtarı}.Options.{SeçenekAnahtarı}
Customize.{KategoriAdı}.{AyarAnahtarı}.Recommendation.Reason
Customize.{KategoriAdı}.Section.{BölümAdı}
```

---

# Yenileme Kapsamı Sistemi

`CustomizeRefreshScope` [Flags] enum'u Windows yüzeylerinin nasıl yenileneceğini kontrol eder.

| Üye | Değer | Etki |
|---|---|---|
| `None` | `0` | Yenileme yok |
| `Settings` | `1 << 0` | `WM_SETTINGCHANGE` yayınla |
| `Associations` | `1 << 1` | Dosya ilişkilendirme değişikliklerini bildir |
| `Desktop` | `1 << 2` | Masaüstü simge listesini yenile |
| `Taskbar` | `1 << 3` | Görev çubuğu `WM_SETTINGCHANGE` yayınla |
| `PolicyUpdate` | `1 << 4` | `SystemParametersInfo` gönder |
| `Theme` | `1 << 5` | `WM_THEMECHANGED` yayınla |
| `DesktopIconCache` | `1 << 6` | Masaüstü simge önbelleğini yenile |

**Bileşikler**: `Default` (`Settings | Associations`), `DesktopIcons` (`Settings | Desktop`), `TaskbarSettings` (`Settings | Taskbar`), `ExplorerView` (`Settings | Associations | PolicyUpdate`).

`ApplyAsync`'i geçersiz kılarsanız, `await ExecutePostActionAsync()` çağırmayı unutmayın.

---

# Yeni Özellikler Geliştirme

1. **Önce GitHub Sorunu (Issue) açın**
2. **Uygulama sırası**: Service → ViewModel → XAML Page → `App.xaml.cs`'e kaydet

```csharp
// DI Kayıt Deseni
services.AddSingleton<YourViewModel>();
services.AddSingleton<YourPage>();
services.AddSingleton<ConfigManager>();
services.AddSingleton<RevertManager>();
services.AddSingleton<OptimizationRegistry>();
services.AddSingleton<OptimizationService>();
services.AddSingleton<SystemInfoService>();
services.AddSingleton<StreamService>();
services.AddSingleton<UpdaterService>();
```

### Sistem Servisleri

| Servis | Amaç |
|---|---|
| `SystemInfoService` | `SystemSnapshot` ile CPU, RAM, GPU bilgisi |
| `StreamService` | Uzak kaynakları indirme |
| `UpdaterService` | GitHub sürümlerini kontrol etme |
| `RegistryWatcher` | Kayıt defteri değişikliklerini izleme |
| `BloatwareService` | Ön yüklemeli AppX paketlerini listeleme |
| `DiskCleanupService` | Disk temizlik taraması |
| `StartupManagerService` | Başlangıç uygulamalarını yönetme |

---

# Geri Alma Sistemi

Her uygulanan optimizasyon `%localappdata%\optimizerDuck\Revert\{optimizationId}.json` yolunda bir JSON dosyası oluşturur.

### Nasıl Çalışır

```
ApplyAsync() → ExecutionScope.Begin() → Provider çağrıları otomatik adım kaydeder → CompleteFromScope() → RevertManager.SaveRevertDataAsync()
```

### Adım Türleri

| Adım Türü | Kaydedilen | Otomatik Oluşturan |
|---|---|---|
| **`RegistryRevertStep`** | Orijinal kayıt defteri değeri | `RegistryService.Write()`, `DeleteValue()`, vb. |
| **`ServiceRevertStep`** | Orijinal servis başlatma türü | `ServiceProcessService.ChangeServiceStartupTypeAsync()` |
| **`ScheduledTaskRevertStep`** | Orijinal görev durumu | `ScheduledTaskService.DisableTask()`, `EnableTask()` |
| **`ShellRevertStep`** | Geri alma komutu | `ShellService.CMDAsync()` — `revertCommand` parametresi |
| **`UsbPowerRevertStep`** | USB güç ayarları | USB optimizasyonları (manuel `ExecutionScope.RecordStep()`) |

Geri alma komutu ekleme:

```csharp
await ShellService.CMDAsync("powercfg /h off", "powercfg /h on");  // revertCommand
```

### Önemli Detaylar

- **Uygulanma durumu**: Dosyanın diskteki varlığından çıkarılır
- **Atomik yazma**: `.tmp` + `File.Replace()`
- **Eşzamanlı erişim**: Dosya başına `SemaphoreSlim` kilitleri, 30 saniye zaman aşımı
- **Ters sırada geri alma**: Son uygulanan = ilk geri alınan
- **Kısmi başarı**: Bazı adımlar başarısız olsa bile devam eder
- **Yeniden deneme**: `OptimizationService.RetryFailedStepsAsync()`
- **Yeni adım türü ekleme**: `Domain/Revert/Steps/`'e `IRevertStep` uygulayan sınıf + `FromData(JObject)` metodu

---

# Test Etme

Testler **xUnit v3** kullanır ve gerçek G/Ç ile entegrasyon tarzı bir yaklaşım benimser.

### Test Kalıpları

| Kalıp | Detay |
|---|---|
| **Mock kütüphanesi yok** | Tüm test double'ları elle yazılmıştır |
| **Gerçek G/Ç** | Gerçek dosya sistemi, kayıt defteri, süreç yürütme |
| **Temizlik** | `try/finally` veya `IDisposable` |
| **İsimlendirme** | `{Method}_{Scenario}_{ExpectedResult}` |
| **Loglama** | `NullLogger<T>.Instance` |

```bash
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release
```

### Test Yapısı

```
optimizerDuck.Test/
├── Common/Helpers/
│   └── SystemRefreshServiceTests.cs
├── Domain/
│   ├── Customize/BaseCustomizeSettingTests.cs
│   ├── Exceptions/StepExecutionExceptionTests.cs
│   └── Revert/Steps/RevertStepSerializationTests.cs, ScheduledTaskRevertStepTests.cs
└── Services/
    ├── ApplyRevertComprehensiveTests.cs
    ├── OptimizationServiceTests.cs, OptimizationServiceIntegrationTests.cs
    ├── RevertManagerTests.cs
    ├── RegistryServiceTests.cs, ServiceProcessServiceTests.cs, ShellServiceTests.cs
    └── RegistryWatcherTests.cs, SystemInfoServiceTests.cs
```

### CI Entegrasyonu

```bash
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build --blame-hang --blame-hang-timeout 30s
```

---

# Kodlama Standartları

| Kural | Değer |
|---|---|
| Sınıflar, metotlar, özellikler | `PascalCase` |
| Private alanlar | `_camelCase` |
| Yerel değişkenler | `camelCase` |
| Async metotlar | `*Async` soneki |
| Girinti | 4 boşluk |
| Maksimum satır uzunluğu | 100 karakter |
| Biçimlendirici | **CSharpier** — `dotnet csharpier .` |
| **String'leri hardcode etmeyin** | `Translations.KeyName` veya `Loc.Instance["Key"]` kullanın |
| **Yorumları minimumda tutun** | |
| **Null yapılabilir referans türleri** | Etkin (`<Nullable>enable</Nullable>`) |

### Hata Yönetimi

| Katman | Uygulama |
|---|---|
| **Optimizasyonlar** | `ApplyResult.False("sebep")` döndürün, fırlatmayın |
| **Sağlayıcı servisler** | try/catch + `ExecutionScope.LogError` |
| **ViewModel'ler** | Komut işleyicilerinde yakalayın, snackbar gösterin |
| **Genel** | App.xaml.cs'de 3 genel hata işleyici. Çökmeler `%localappdata%\optimizerDuck\Crashes\crash_*.log`'a kaydedilir |

---

# Yerelleştirme

Tüm kullanıcı metinleri `Resources/Languages/Translations.resx` içindedir.

### Mevcut Diller (11 dil + İngilizce)

| Dil | Dosya |
|---|---|
| English | `Translations.resx` (varsayılan) |
| Vietnamese | `Translations.vi-VN.resx` |
| Spanish | `Translations.es-ES.resx` |
| French | `Translations.fr-FR.resx` |
| Geleneksel Çince | `Translations.zh-TW.resx` |
| Basitleştirilmiş Çince | `Translations.zh-CN.resx` |
| Russian | `Translations.ru-RU.resx` |
| Korean | `Translations.ko-KR.resx` |
| Japanese | `Translations.ja-JP.resx` |
| Polish | `Translations.pl-PL.resx` |
| Turkish | `Translations.tr-TR.resx` |
| Portuguese (Brazil) | `Translations.pt-BR.resx` |

### Yeni Dil Ekleme

1. `Translations.{locale}.resx` dosyasını oluşturun (tüm anahtarlarla)
2. `SettingsViewModel.cs`'e kaydedin:
```csharp
new() { DisplayName = "Deutsch", Culture = new CultureInfo("de-DE") },
```

XAML'de:
```xml
<ui:TextBlock Text="{ext:Loc Dashboard.Header.Title}" />
```

---

# Çekme İsteği Süreci (Pull Request Process)

1. **`master`'dan dal açın**: `feature/adiniz` veya `fix/issue-id`
2. **Conventional Commits**: `feat:` (yeni özellik), `fix:` (hata düzeltme), `refactor:` (yeniden düzenleme), `docs:` (dokümantasyon), `test:` (test), `i18n:` (çeviri), `chore:` (bakım)
3. **Göndermeden önce doğrulayın**:

```bash
dotnet build optimizerDuck.slnx --configuration Release
dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build
dotnet csharpier .
```

4. **PR açın**: Ne ve neden değişti? UI değişiklikleri varsa ekran görüntüsü ekleyin. İlgili Issue'ları `Closes #42` ile bağlayın.

### PR Kontrol Listesi

- [ ] Kod mevcut kalıpları takip ediyor
- [ ] Yerelleştirme anahtarları `Translations.resx`'e eklendi
- [ ] `dotnet build` başarılı (0 hata)
- [ ] `dotnet test` başarılı
- [ ] `dotnet csharpier .` çalıştırıldı
- [ ] Hardcode string yok
- [ ] Geri alma adımları kaydedildi (varsa)
- [ ] UI değişiklikleri için ekran görüntüsü eklendi

---

# Sorun (Issue) Yönergeleri

- **Hata raporları**: Yeniden üretme adımları, beklenen/gerçek davranış, `%localappdata%\optimizerDuck\optimizerDuck.log` + sistem özellikleri
- **Özellik istekleri**: Kullanım durumu, çözdüğü sorun, nasıl çalışması gerektiği
- **Optimizasyon önerileri**: Kayıt defteri yolları, servis adları, CLI komutları, güvenilir kaynaklar
- **Sorular**: GitHub Discussions veya [Discord](https://discord.gg/tDUBDCYw9Q)

---

# SSS ve Sorun Giderme

### Derleme "CA1416" hatasıyla başarısız oluyor
`.editorconfig` CA1416'yı susturur. Proje Windows'a özeldir.

### Optimizasyon UI'da görünmüyor
- Kategori içinde **iç içe public sınıf** mı?
- Kategori `IOptimizationCategory` uyguluyor mu?
- `BaseOptimization`'dan türetilmiş mi?
- `[Optimization(Id = "...")]` özniteliği var mı?
- Yerelleştirme anahtarları `Translations.resx`'e eklendi mi?

### Özelleştirme ayarı görünmüyor
- `[CustomizeSetting(Section = ..., Icon = ...)]` var mı?
- `Section` yazımı doğru mu?
- `[CustomizeCategory(PageType = ...)]` var mı?

### UI donuyor
`ApplyAsync`'te `async`/`await` kullandığınızdan emin olun. `.Result`/`.Wait()` UI'ı dondurur.

### GUID nasıl oluşturulur?
```powershell
[guid]::NewGuid()
```

### Çeviriler anahtar adı olarak görünüyor
`Translations.resx`'e yerelleştirme anahtarları eklemeyi unuttunuz.

### "Geri alma verisi yok" hatası
Optimizasyonun `Id` GUID'inin değişmediğinden emin olun.

### Yeni geri alma adım türü ekleme
1. `Domain/Revert/Steps/`'e `IRevertStep` uygulayan sınıf oluşturun
2. Statik `FromData(JObject data)` metodu ekleyin
3. `ExecutionScope.RecordStep()` ile kaydedin

### Çökme güvenliği
- Geri alma dosyaları atomik yazma kullanır (`.tmp` + `File.Replace`)
- Çökme günlükleri: `%localappdata%\optimizerDuck\Crashes\crash_*.log`
- App.xaml.cs'de 3 genel hata işleyici

---

<div align="center">

## Teşekkürler

Katkıda bulunanlar sürüm notlarında listelenir.

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
