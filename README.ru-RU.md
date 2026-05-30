<div align="center">

<a href="https://optimizerduck.vercel.app/"><img src="./.github/assets/optimizerDuck.png" alt="optimizerDuck Banner" title="optimizerDuck"/></a>

# [optimizerDuck](https://optimizerduck.vercel.app/)

**optimizerDuck — это бесплатный инструмент с открытым исходным кодом для оптимизации Windows, созданный для производительности, конфиденциальности и простоты.**

[![Release](https://img.shields.io/github/release/itsfatduck/optimizerDuck?color=fed114&label=%D0%A0%D0%B5%D0%BB%D0%B8%D0%B7&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/itsfatduck/optimizerDuck/total?label=%D0%A1%D0%BA%D0%B0%D1%87%D0%B8%D0%B2%D0%B0%D0%BD%D0%B8%D1%8F&style=flat-square&color=lightgreen)](https://github.com/itsfatduck/optimizerDuck/releases)
[![Stars](https://img.shields.io/endpoint?url=https://api.pinstudios.net/api/badges/stars/itsfatduck/optimizerDuck&style=flat-square)](https://github.com/itsfatduck/optimizerDuck/stargazers)
[![License](https://img.shields.io/badge/%D0%9B%D0%B8%D1%86%D0%B5%D0%BD%D0%B7%D0%B8%D1%8F-GPL_v3-red?style=flat-square)](./LICENSE)
[![Discord](https://img.shields.io/discord/1091675679994675240?color=5865f2&label=Discord&style=flat-square)](https://discord.gg/tDUBDCYw9Q)
<br>
[![CI](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml/badge.svg)](https://github.com/itsfatduck/optimizerDuck/actions/workflows/ci.yml)
[![.NET Latest](https://img.shields.io/badge/.NET_Runtime-%D0%9F%D0%BE%D1%81%D0%BB%D0%B5%D0%B4%D0%BD%D1%8F%D1%8F-ef99dd?style=flat-square)](https://dotnet.microsoft.com/en-us/download)
[![Supported OS](https://img.shields.io/badge/%D0%9F%D0%BE%D0%B4%D0%B4%D0%B5%D1%80%D0%B6%D0%BA%D0%B0-Windows_10%2B_x64-0078d4?style=flat-square)](https://www.microsoft.com/en-us/software-download/)

**[Начало работы](https://optimizerduck.vercel.app/docs/guides/getting-started) | [Как это работает](https://optimizerduck.vercel.app/docs/guides/how-it-works) | [FAQ](https://optimizerduck.vercel.app/docs/faq/general)**

[English](README.md) | [Tiếng Việt](README.vi.md) | [繁體中文](README.zh-TW.md) | [简体中文](README.zh-CN.md) | **Русский** | [Français](README.fr-FR.md) | [한국어](README.ko-KR.md)

<details>
<summary>⭐ История звёзд</summary>

Если optimizerDuck помог улучшить ваш ПК, поставьте репозиторию ⭐ и поделитесь им с другими.
Каждая звезда мотивирует на дальнейшие улучшения.

<a href="https://www.star-history.com/#itsfatduck/optimizerDuck&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=itsfatduck/optimizerDuck&legend=top-left" />
 </picture>
</a>

</details>

<img src="./.github/assets/app.png" alt="optimizerDuck Тёмная тема" title="optimizerDuck Тёмная тема" width="800"/>

</div>

---

## Быстрый старт

1. Скачайте из **[GitHub Releases](https://github.com/itsfatduck/optimizerDuck/releases/latest)**
2. Запустите `.exe` напрямую, установка не требуется
3. Выберите нужные оптимизации, примените их и перезагрузите ПК, когда будете готовы

> [!TIP]
> Всегда создавайте **точку восстановления системы** перед внесением изменений.

> [!NOTE]
> Доступно на английском, вьетнамском, традиционном китайском (спасибо [@abc0922001](https://github.com/abc0922001)), упрощённом китайском (спасибо [@wcxu21](https://github.com/wcxu21)), русском (спасибо [@Foodhead](https://github.com/Foodhead)), французском (спасибо [@Robocnop](https://github.com/Robocnop)) и корейском (спасибо [@klfnn](https://github.com/klfnn)).
> Хотите добавить свой язык? См. [CONTRIBUTING.md](./CONTRIBUTING.md).

---

## Что делает optimizerDuck

Windows содержит много вещей, которые могут быть вам не нужны: фоновые службы, телеметрия, предустановленные приложения, программы автозагрузки и запланированные задачи, потребляющие ресурсы. optimizerDuck предоставляет единый интерфейс для очистки всего этого.

Он применяет целевые системные настройки для снижения нагрузки и блокировки нежелательного поведения, а также включает несколько инструментов управления, чтобы вы могли видеть, что запущено, удалять ненужное и отменять любые изменения в случае проблем.

> [!NOTE]
> Каждую оптимизацию можно применить вручную. optimizerDuck просто упрощает их применение.

### Системные оптимизации

Более 30 настроек в 6 категориях, каждая с чётким описанием и уровнем риска, чтобы вы точно знали, что делает изменение до его применения.

| Категория                 | Что включает                                                                                                                                                        |
| :------------------------ | :------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Производительность**    | Настройка Service Host на основе вашей RAM, регулировка приоритетов процессов, снижение задержки клавиатуры, настройки Multimedia Scheduler для более плавной игры  |
| **Конфиденциальность**    | Отключение телеметрии Windows, отчётов об ошибках, рекламного ID, отслеживания местоположения, Cortana, Copilot и предложений контента                              |
| **GPU**                   | Специфические для производителя настройки реестра для GPU AMD, NVIDIA и Intel, включая состояния питания, clock gating и задержку отображения                       |
| **Питание**               | Отключение гибернации и быстрого запуска, отключение USB selective suspend, установка кастомного высокопроизводительного плана питания, отключение power throttling |
| **Bloatware и службы**    | Блокировка повторной установки OEM-приложений и тонкая настройка типов запуска для 200+ служб Windows                                                               |
| **Пользовательский опыт** | Удаление задержек меню, отключение визуальных эффектов, таких как анимация панели задач и прозрачность, для более быстрого отклика                                  |

> [!IMPORTANT]
> Если вам кажется, что оптимизаций не так много, не думайте, что они неэффективны или устарели. optimizerDuck фокусируется только на тех оптимизациях, которые были протестированы, проверены или широко признаны сообществом. Некоторые изменения могут не давать немедленно заметных результатов, но они могут помочь вашей системе работать более плавно и стабильно со временем.

### Переключатели функций

Включайте и выключайте настройки Windows без копания в реестре или поиска инструкций в интернете. Организованы в четыре раздела:

- **Рабочий стол**: Показывать или скрывать значки (Этот компьютер, Корзина, Сеть, Файлы пользователя, Панель управления), удалять стрелки ярлыков
- **Панель задач**: Выравнивание по центру или слева, переключение виджетов, кнопки Task View и End Task, секунды на часах, поиск Bing в меню Пуск
- **Проводник**: Расширения файлов, скрытые файлы, история буфера обмена, компактный вид, snap assist, флажки элементов, классическое контекстное меню и другое
- **Игры**: Game Mode, Game Bar, фоновая запись, ускорение мыши, оптимизации полноэкранного режима, аппаратное ускорение GPU
- **Система**: Включение Num Lock при загрузке

### Встроенные инструменты

| Инструмент            | Что делает                                                                                                                                       |
| :-------------------- | :----------------------------------------------------------------------------------------------------------------------------------------------- |
| **System Dashboard**  | Просмотр информации о CPU, RAM, GPU, дисках и ОС в одной панели                                                                                  |
| **Startup Manager**   | Просмотр всех приложений и задач, запускаемых при загрузке, их включение/отключение и открытие расположения файлов                               |
| **Scheduled Tasks**   | Просмотр, запуск, остановка, включение, отключение или удаление запланированных задач Windows                                                    |
| **Disk Cleanup**      | Сканирование и очистка временных файлов, системного кэша, остатков Windows Update, prefetch, эскизов, корзины, дампов и старых установок Windows |
| **Bloatware Remover** | Список всех удаляемых пакетов AppX с метками риска (Безопасно, Осторожно, Неизвестно), чтобы вы выбрали, что удалять                             |

---

## Безопасность

Мы знаем, что изменение системных настроек сопряжено с риском, поэтому инструмент построен на принципах отката и контроля пользователя.

См. [Политику конфиденциальности](./PRIVACY.md) для получения подробной информации о нашей работе с данными.

- **Автоматическое резервирование**: Каждое изменение записывает файл отката в локальную папку. Вы можете восстановить отдельные настройки или откатить всё
- **Откат в один клик**: Отмените любую применённую оптимизацию из интерфейса одним нажатием
- **Уровни риска**: Каждая настройка помечена как Безопасная, Умеренная или Рискованная в зависимости от потенциального воздействия
- **Нет автоматического применения**: Ничего не запускается, пока вы не выберете. Инструмент ничего не включает сам
- **Предложение точки восстановления**: Перед первой оптимизацией приложение предлагает создать точку восстановления Windows

---

## Технические детали

- **Фреймворк**: WPF на .NET 10, с использованием библиотеки WPF UI для дизайна Fluent
- **Система отката**: Четыре типа шагов отката (Registry, Service, Scheduled Task, Shell) с JSON-состоянием и потокобезопасным файловым вводом-выводом
- **Темы оформления**: Тёмная (по умолчанию), Светлая и Высокая контрастность с поддержкой Mica
- **Без установки**: Запускается как один .exe, установка не требуется
- **Система резервирования**: Локальная папка для резервных копий каждого изменения с восстановлением в один клик
- **Автообнаружение**: Категории оптимизаций и функций обнаруживаются автоматически через reflection + кастомные атрибуты, ручная регистрация не требуется
- **Нет телеметрии**: Приложение не собирает никаких данных пользователя

---

## Документация

### [Официальная документация](https://optimizerduck.vercel.app/docs/guides/getting-started)

Пошаговые руководства, подробное описание каждой оптимизации и лучшие практики использования optimizerDuck.

---

## Вклад в проект

Мы приветствуем вклад сообщества! Будь то исправление ошибок, добавление новых оптимизаций или функций, улучшение документации или помощь в переводе приложения на другие языки — ваша поддержка очень ценится.

Для получения дополнительной информации см. [CONTRIBUTING.md](./CONTRIBUTING.md).

---

## Сообщество

> [!TIP]
> Присоединяйтесь к нашему серверу Discord для получения поддержки, советов и обсуждений с другими пользователями и контрибьюторами.
>
> <a href="https://discord.gg/tDUBDCYw9Q"><img src="https://discord.com/api/guilds/1091675679994675240/widget.png?style=banner2" alt="Discord Banner 2"/></a>

Если optimizerDuck помог вашему ПК:

- ⭐ Поставьте звезду репозиторию
- 💬 Присоединяйтесь к Discord для общения
- 🐞 Сообщайте об ошибках на GitHub
- 🎁 Поддержите проект [здесь](https://optimizerduck.vercel.app/docs/contribute/support-me)

### Ссылки

- 🌐 [Веб-сайт](https://optimizerduck.vercel.app/)
- 📖 [Документация](https://optimizerduck.vercel.app/docs/guides/getting-started)
- 💬 [Discord](https://discord.gg/tDUBDCYw9Q)
- 🐞 [Issues](https://github.com/itsfatduck/optimizerDuck/issues)

Важен любой вклад. Отчёты об ошибках, предложения функций, переводы и просто ваш опыт — всё имеет значение.

---

## Отказ от ответственности

optimizerDuck предоставляется **«как есть»**, без каких-либо гарантий.

Используя этот инструмент, вы соглашаетесь, что авторы не несут ответственности за нестабильность системы, потерю данных или проблемы, вызванные сторонним ПО или изменениями пользователя.

Всегда создавайте **точку восстановления** перед применением изменений.

> [!NOTE]
> optimizerDuck изменяет системные настройки и реестр Windows. Используйте на свой страх и риск. Мы рекомендуем создавать резервную копию важных данных и точку восстановления перед внесением изменений.
>
> См. [Условия использования](./TERMS.md), [Политику конфиденциальности](./PRIVACY.md) и [Отказ от ответственности](./DISCLAIMER.md) для получения дополнительной информации.

---

## Лицензия

<div align="center">

<a href="./LICENSE">
<img src=".github/assets/gplv3.png" alt="GPL v3 License" title="GPL v3 License"/>
</a>

**[Лицензия GPL v3](https://www.gnu.org/licenses/gpl-3.0.en.html)**<br>См. [LICENSE](./LICENSE).

</div>

<div align="center">

## Спасибо всем контрибьюторам

[![Contributors](https://contrib.rocks/image?repo=itsfatduck/optimizerDuck)](https://github.com/itsfatduck/optimizerDuck/graphs/contributors)

</div>
