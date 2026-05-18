# LinguaLens — Техническое задание

## 1. Обзор продукта

**LinguaLens** — десктопное Windows-приложение, работающее в системном трее. При наведении курсора на английское или испанское слово в любом приложении (включая PDF-ридеры) показывает всплывающую карточку с переводом на русский, транскрипцией, комментарием и примерами использования. Поддерживает режим перевода выделенного предложения и ведёт личный словарь просмотренных слов с экспортом в Anki/CSV.

---

## 2. Функциональные требования

### 2.1 Режим слова (Word Mode)
- Пользователь наводит курсор на слово — после 400 мс debounce появляется карточка
- Карточка содержит: слово, часть речи, транскрипция (EN), перевод, краткий комментарий (контекстные нюансы), 2–3 примера предложений с переводом
- Определяется язык слова: EN или ES; слова на других языках — игнорируются
- Карточка исчезает при уходе курсора с попапа или по нажатию Escape
- Если слово/контекст уже есть в кэше — ответ мгновенный (без LLM запроса)

### 2.2 Режим предложения (Sentence Mode)
- Пользователь выделяет текст (любым способом) → в правом нижнем углу экрана появляется кнопка-подсказка "Перевести"
- По нажатию открывается карточка с переводом всего выделенного текста и кратким комментарием
- Примеры в этом режиме не генерируются

### 2.3 Vocabulary Store
- Каждый успешно переведённый запрос (word mode) автоматически сохраняется в локальную SQLite БД
- В трее: пункт "Словарь" → открывает окно со списком слов, фильтрами (язык, дата, источник), поиском
- Из словаря можно удалять записи и помечать как "изучено"
- Экспорт: CSV (универсальный) и Anki `.apkg` (Basic card: Front=слово, Back=перевод+пример)

### 2.4 Настройки
- API Key для LLM провайдера
- Выбор провайдера: Groq / Google Gemini (с возможностью добавить OpenRouter)
- Debounce задержка (по умолчанию 400 мс)
- Горячая клавиша для включения/выключения (по умолчанию Alt+Shift+L)
- Языки для обнаружения (EN, ES — по умолчанию оба включены)
- Автосохранение в словарь (вкл/выкл)
- Тема попапа (светлая/тёмная)

### 2.5 Системный трей
- Приложение запускается в трей, без окна
- Пункты меню: Включить/Выключить, Словарь, Настройки, Выход
- Иконка трея меняется при включении/выключении

---

## 3. Нефункциональные требования

| Параметр | Требование |
|---|---|
| ОС | Windows 10 / 11 x64 |
| Latency (cache hit) | < 50 мс |
| Latency (LLM, Groq) | < 1500 мс |
| Размер дистрибутива | < 100 МБ |
| RAM footprint | < 150 МБ |
| Запуск с Windows | Опционально, через реестр |
| Оффлайн | Кэш работает, LLM недоступен — показывается сообщение |

---

## 4. Извлечение текста

### Приоритет стратегий (ITextExtractor)

1. **UIAutomation** (`System.Windows.Automation`)
   - `AutomationElement.FromPoint(point)` → `TextPattern` → `RangeFromPoint`
   - Expand to `TextUnit.Word` для слова, `TextUnit.Sentence` для контекста
   - Работает: браузеры, Foxit Reader, Word, Notepad++, большинство UI-приложений

2. **Clipboard fallback**
   - Используется только если UIAutomation вернул пустую строку
   - Только в sentence mode: пользователь уже выделил текст → читаем буфер
   - В word mode clipboard fallback не применяется (слишком инвазивно)

### Что извлекаем
- `word` — слово под курсором
- `sentence` — окружающее предложение (контекст для LLM)
- `sourceName` — имя процесса/заголовок окна (для vocab store)

---

## 5. Определение языка

Простая эвристика без внешних библиотек:
- Алфавит: только латиница → кандидат EN/ES
- Наличие символов `ñ, á, é, í, ó, ú, ü, ¿, ¡` → испанский
- Иначе → английский
- Не латиница → игнорировать

Если нужна точность в edge cases — опционально подключить `LanguageDetection` NuGet (порт Nakatani Shuyo).

---

## 6. LLM интеграция

### Промпт (Word Mode)
```
You are a language learning assistant. The user is reading text in {lang} and needs help understanding a word.

Word: "{word}"
Context sentence: "{sentence}"
Target language for translation: Russian

Respond with ONLY valid JSON, no markdown, no explanation:
{
  "word": "original word",
  "detected_lang": "en|es",
  "pos": "noun|verb|adjective|adverb|other",
  "transcription": "IPA or standard transcription, EN only, empty string for ES",
  "translation": "перевод на русский (с учётом контекста)",
  "comment": "краткий комментарий: контекстный нюанс, если слово многозначное или есть ловушка",
  "examples": [
    {"original": "example sentence", "translation": "перевод примера"},
    {"original": "example sentence 2", "translation": "перевод примера 2"}
  ]
}
```

### Промпт (Sentence Mode)
```
Translate the following {lang} text to Russian. Provide a natural translation.
Respond with ONLY valid JSON:
{
  "translation": "перевод на русский",
  "comment": "краткий комментарий если есть важный нюанс, иначе пустая строка"
}

Text: "{text}"
```

### Провайдеры

**Groq (primary)**
- Endpoint: `https://api.groq.com/openai/v1/chat/completions`
- Model: `llama-3.1-8b-instant` (слово) / `llama-3.3-70b-versatile` (предложение)
- Free tier: 14 400 req/day, 6000 RPM
- OpenAI-compatible API

**Google Gemini Flash (alternative)**
- Endpoint: `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent`
- Free tier: 1500 req/day

---

## 7. Схема БД (SQLite)

```sql
-- Кэш переводов
CREATE TABLE translation_cache (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    cache_key TEXT NOT NULL UNIQUE,  -- "{lang}:{word}:{context_hash}"
    word TEXT NOT NULL,
    response_json TEXT NOT NULL,
    created_at TEXT NOT NULL,
    hit_count INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX idx_cache_key ON translation_cache(cache_key);

-- Словарь пользователя
CREATE TABLE vocab_entries (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    word TEXT NOT NULL,
    detected_lang TEXT NOT NULL,
    translation TEXT NOT NULL,
    pos TEXT,
    context_sentence TEXT,
    source_app TEXT,
    response_json TEXT NOT NULL,  -- полный ответ LLM для экспорта
    created_at TEXT NOT NULL,
    is_learned INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX idx_vocab_word ON vocab_entries(word);
CREATE INDEX idx_vocab_created ON vocab_entries(created_at);
```

---

## 8. Структура WPF Overlay

```
OverlayWindow (Topmost, AllowsTransparency, WindowStyle=None)
├── Позиционирование: рядом с курсором, но с учётом краёв экрана
├── Анимация: FadeIn 150ms при появлении
├── Закрытие: MouseLeave с задержкой 300ms (чтобы можно было навести на попап)
│
└── WordCard
    ├── Header: [word]  [pos badge]  [transcription]  [flag emoji]
    ├── Translation: крупный текст
    ├── Comment: серый курсив (если не пустой)
    ├── Examples: список с оригиналом и переводом
    ├── Footer: [💾 Сохранить] [📋 Копировать] [Перевести предложение →]
    └── LoadingState: shimmer/spinner пока идёт запрос
```

---

## 9. Экспорт в Anki

Формат `.apkg` — это ZIP-архив с SQLite внутри.

```
deck.apkg
├── collection.anki2  (SQLite: notes, cards, col)
└── media             (пустой файл)
```

Схема Anki Basic note:
- `flds`: `{word}\x1f{translation}\n{example_original}\n{example_translation}`
- `sflds`: то же, разделённое `\x1f`

Использовать библиотеку `AnkiSharp` или реализовать напрямую (схема фиксированная и хорошо задокументирована).

---

## 10. Out of scope (v1)

- macOS / Linux
- Встроенный spaced repetition (только экспорт в Anki)
- Произношение (TTS)
- Расширение языков помимо EN/ES
- Синхронизация словаря между устройствами
- История сессий
