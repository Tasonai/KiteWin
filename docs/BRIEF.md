# KiteWin

Автономный Windows-клиент студии архитектуры Antigravity и генерации промптов с бесшовной P2P-синхронизацией с Kite macOS.

## Replaces
Разрозненные текстовые файлы заметок, ручной перенос архитектурных промптов и разрыв контекста между macOS и Windows рабочими станциями.

## Main jobs
1. Управление банком идей и архитектурных концепций (JTBD, стек, платформа, статус).
2. Компиляция высокоточных промптов для Google Stitch (DESIGN.md), Figma Tokens (W3C JSON), FigJam диаграмм и Master Plan без галлюцинаций и без эмодзи.
3. Двусторонняя локальная P2P-синхронизация по REST API между Kite macOS (порт 24242) и KiteWin (порт 24243).
4. Навигация по базе 355 скиллов и 18 плагинов Antigravity с мгновенной фильтрацией.

## Data
- Item: Id (Guid), Title (string), Concept (string), Notes (string), Platform (string: Web/Mobile/Desktop), Style (string: 10 дизайн-систем), Status (string: backlog/in_progress/completed), Tags (string), GeneratedPrompt (string), StitchPrompt (string), TargetedEditPrompt (string), IsStarred (bool), CreatedAt (DateTimeOffset), UpdatedAt (DateTimeOffset).
- SyncEndpoint: MacEndpoint (string, default "http://127.0.0.1:24242"), LocalPort (int: 24243), AutoSync (bool), LastSyncAt (DateTimeOffset?).

## Shape
Нативное WinUI 3 приложение с Mica-бэкдропом, кастомным TitleBar, боковой навигацией NavigationView (Fluent Design) и окном быстрого захвата идей (Quick Capture). Локальная база данных SQLite (WAL mode). Работает полностью офлайн без внешних аккаунтов.

## System hooks
- Глобальный шорткат быстрого захвата Ctrl+Shift+K (Quick Capture).
- Горячие клавиши Ctrl+N (Новая идея), Ctrl+S (Сохранить), Ctrl+R (Синхронизация).
- REST HTTP-сервер (порт 24243) и REST HTTP-клиент для связи с Kite macOS.
- Интеграция с буфером обмена Windows (Clipboard) и запуск из меню Пуск.

## Reference
Kite macOS (Apple HIG / SwiftData / SwiftUI / Network.framework).

## Look
Нативный Fluent Design Windows 11 с фоновым материалом Mica, карточками CardBackgroundFillColorDefaultBrush, строгой 4 epx сеткой (UI/Space.xaml), шрифтом Segoe UI Variable, системными иконками Segoe Fluent Icons и нулевым использованием эмодзи.

## Not in version one
- Облачная авторизация и платные подписки.
- Хранение паролей в открытом виде (используется Windows Credential Locker при подключении к внешним API).
- Тяжелые графические редакторы векторной графики внутри приложения (экспорт выполняется в W3C JSON и Mermaid).
