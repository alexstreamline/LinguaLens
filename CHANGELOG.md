# Changelog

## [Unreleased]

### Sentence-card: пропал оригинал, окно уплывало, OCR-courrupted слова

Три проблемы одной серии после введения `ContextSentence` для оригинала.

**Баг 1 — пропадал оригинал в sentence-карточке.** В предыдущем коммите оригинал привязан к `HasContextSentence`, но `_overlay.ShowSentenceResult(sentenceResult)` вызывался без 2-го аргумента → ContextSentence пустой → блок скрыт.
- `LinguaLens.App/Overlay/DebounceController.cs`:
  - `TriggerManualAsync` (selection-flow): `_overlay.ShowSentenceResult(sentenceResult, selectedText)` — передаём оригинальный selected text
  - `OnTranslateSentenceRequested` (word→sentence escalation): `_overlay.ShowSentenceResult(result, contextSentence ?? "")` — передаём контекст-параграф из word-card

**Баг 2 — окно уплывало вниз-вправо при первом показе.** Раньше я убрал `RepositionWindow` из `Show*Result` чтобы окно не «прыгало» при переключении содержимого. Но при росте Width (loading=320 → word=420 → sentence=580) окно оставалось в той же left-позиции и вылезало за правый край экрана.
- `LinguaLens.App/Overlay/OverlayWindow.xaml.cs`:
  - Новый приватный метод `RepositionIfOffscreen()` — вызывает `RepositionWindow()` только если `Left + ActualWidth > workingArea.Right` или `Top + ActualHeight > workingArea.Bottom`
  - Добавлен вызов `RepositionIfOffscreen()` после `UpdateLayout()` в `ShowResult`, `ShowSentenceResult`, `ShowError`
  - Trade-off: окно стоит на месте если новый размер помещается; flip к курсору только при выходе за экран

**Баг 3 — OCR ловит caret-курсор как букву посреди слова** («StriYing» вместо «Striving»). Caret в Foxit/IDE моргает поверх текста, попадает в скриншот.
- `LinguaLens.Infrastructure/Llm/LlmPrompts.cs WordTranslation` — добавлен явный блок IMPORTANT — OCR correction: если слово выглядит битым (wrong case в середине, лишние/пропавшие символы), LLM ВЫВОДИТ настоящее слово из контекста и переводит его, а `word` в JSON — corrected form. Если слово выглядит нормально — оставляет как есть.

**Прочее:**
- `GroqLlmClient.cs` `max_tokens` `900 → 1500` — большой промпт (definition + synonyms + pairs + comment + examples) превышал 900 у llama-3.1-8b, ответ обрезался → пустой Translation в карточке.

**Проверка:** build 0/0.

**Эффект:**
- Оригинал в sentence-карточке снова виден (точный extractor-output)
- Окно не уплывает за пределы экрана даже при первом показе word/sentence — repositioning срабатывает по необходимости
- LLM сама исправляет OCR-битые слова по контексту (например `nappy patn cowmn` → `more nested levels` если контекст подсказывает)

---

### Sentence-card: оригинал = исходный контекст (не pairs от LLM)

На скрине 6.png пользователь показал: в тексте «the more nested levels a function requires», в карточке-оригинале «**nappy patn cowmn** a function requires» — LLM придумала бред в pairs.original. Pairs от LLM небезопасны — она может терять/искажать/придумывать слова, особенно на шумном OCR-входе.

**Решение** — отображать оригинал в карточке как `contextSentence` (исходный текст, который extractor реально прочитал), не как concat(pairs.Original).

**`LinguaLens.App/ViewModels/SentenceCardViewModel.cs`:**
- Добавлены `public string ContextSentence` (источник: параметр конструктора), `public bool HasContextSentence`
- Сохранён `_contextSentence` для legacy-логики picker'a и сохранения в vocab

**`LinguaLens.App/Overlay/SentenceCardView.xaml`:**
- ОРИГИНАЛ-блок теперь `<TextBlock Text="{Binding ContextSentence}" />` (вместо TextBlock + Inlines из Pairs)
- Visibility биндится на `HasContextSentence` (вместо `HasPairs`)

**`LinguaLens.App/Overlay/SentenceCardView.xaml.cs`** — упрощён до пустого partial-class (`InitializeComponent` only). Удалены build Inlines, обработчики MouseEnter/Leave/Click, словарь Run'ов, подписки PropertyChanged на pairs.

**Известное ограничение (deferred):** picker-режим (кнопка «+ Слова в словарь») сейчас не показывает интерактивный выбор слов — оригинал не разбит на Run'ы. State машина SentenceMode остаётся, но UI выбора пока не отображается. Будет восстановлено отдельной задачей: в picker mode заменять статичный оригинал на ItemsControl с chips для выбора.

**Проверка:** build 0/0.

**Эффект:** оригинал в карточке теперь точно соответствует тому, что extractor прочитал из приложения — без «творчества» LLM. Если оригинал кривой (плохой OCR) — это видно сразу, и пользователь понимает откуда искажения в переводе.

---

### Sentence-card: пунктуация в переводе + опрятный вид

Сравнение с Lookupper показало: наша карточка выглядела «колонкой» (узкая + жирный шрифт + большой line-height + перевод без знаков препинания). Корневая причина потери пунктуации — рендер перевода через Pairs (Run'ы через пробел), а LLM делит pairs без знаков препинания.

**Решение** — перевод показывать как сплошной текст из поля `Translation` (LLM возвращает его с правильной пунктуацией), а pairs использовать только для оригинала (hover + picker).

**Изменения:**
- `LinguaLens.App/Overlay/OverlayWindow.xaml.cs ShowSentenceResult` — `Width = 580` (было 500)
- `LinguaLens.App/Overlay/SentenceCardView.xaml`:
  - **TranslationTextBlock** теперь обычный TextBlock с `Text="{Binding Translation}"` (без Inlines). FontSize 17→15, FontWeight Medium→Normal, LineHeight 24→20. Не жирный, плотнее.
  - **OriginalTextBlock** компактнее: FontSize Body(14)→13, LineHeight 22→19
- `LinguaLens.App/Overlay/SentenceCardView.xaml.cs`:
  - `BuildInlines()` теперь строит Inlines только для `OriginalTextBlock`
  - Удалён `_transRuns` словарь и связанные операции в `OnPair_PropertyChanged`
  - Hover/picker по-прежнему работает: hover на слове оригинала подсвечивает Run в OriginalTextBlock. Подсветка parallel на стороне перевода временно отключена (перевод не разбит на Run'ы)
- `LinguaLens.Infrastructure/Llm/LlmPrompts.cs SentenceTranslation` — добавлен блок CRITICAL: `translation` MUST содержать «well-formed Russian sentence with proper punctuation», pairs MUST включать trailing punctuation (`"the cat,"` не `"the cat" + ","`), последний chunk должен включать терминальную точку/вопрос

**Проверка:** build 0/0.

**Эффект:** перевод теперь выглядит как нормальный связный текст с запятыми/точками. Карточка визуально просторнее за счёт большей ширины и плотного шрифта без излишнего веса.

**Известное ограничение:** подсветка пары на стороне перевода при hover на оригинале не работает (перевод теперь не разбит на интерактивные Run'ы). Это сознательный trade-off — связный текст с пунктуацией важнее визуальной парности.

---

### Fix v4: ширина окна per-mode (root cause обрезки)

Корневая причина обрезки sentence-card — `SizeToContent="WidthAndHeight"` на `OverlayWindow` не пересчитывал ширину окна при смене Visibility панелей внутри Grid. Окно оставалось узким (~280 px от прошлого word-card) и обрезало sentence-content слева/справа.

**Решение** — управлять шириной окна явно из code-behind для каждого режима, оставить `SizeToContent="Height"` только для авто-подгонки по высоте.

**`LinguaLens.App/Overlay/OverlayWindow.xaml`:**
- `SizeToContent="WidthAndHeight"` → `SizeToContent="Height" Width="320"` (320 — default для loading)

**`LinguaLens.App/Overlay/OverlayWindow.xaml.cs`** — в начало каждого `Show*` метода добавлено явное `Width = ...`:
- `ShowLoading` → `Width = 320`
- `ShowResult` (word) → `Width = 420`
- `ShowSentenceResult` → `Width = 500`
- `ShowError` → `Width = 320`

**Уборка:**
- `SentenceCardView.xaml` — убран `Width="460" MaxWidth="460"` на root StackPanel (теперь ширину диктует окно)
- `WordCardView.xaml` — убран `MinWidth="300" MaxWidth="420"` (то же)

**Проверка:** build 0/0.

**Эффект:** sentence-card теперь нормальной ширины 500 px, текст оборачивается корректно. Word-card 420 px, loading/error 320 px. Размер окна меняется при переключении режимов — но окно стоит на месте (RepositionWindow всё ещё не вызывается из Show*).

---

### Fix v3: sentence-pairs через TextBlock + Inlines (нативный word-wrap)

`WrapPanel` в `ItemsPanelTemplate` (даже с `RelativeSource` ActualWidth) надёжно не получает constrained width в WPF — пары всё равно вылазили за правый край карточки.

**Решение** — отказаться от `ItemsControl + WrapPanel + Border` и использовать `TextBlock + Inline Run`. TextBlock с `TextWrapping="Wrap"` — это нативный word-wrap WPF, корректно переносит слова в потоке.

**`LinguaLens.App/Overlay/SentenceCardView.xaml`:**
- Оба `ItemsControl` (Original / Translation) заменены на простые `TextBlock` с `x:Name="OriginalTextBlock"` / `"TranslationTextBlock"`, `TextWrapping="Wrap"`, `LineHeight=22/24`, типографика из Tokens.
- Inlines заполняются программно в code-behind при `DataContextChanged`.

**`LinguaLens.App/Overlay/SentenceCardView.xaml.cs`** переписан:
- `DataContextChanged` → `BuildInlines()` строит `Run`'ы из `Pairs`:
  - На каждую пару — 2 `Run` (Original в OriginalTextBlock, Translation в TranslationTextBlock), `Tag = pair`
  - Между парами вставляется `new Run(" ")` как разделитель
  - Подписываются `Run.MouseEnter/MouseLeave/MouseLeftButtonDown`
- Хранятся словари `_origRuns[index] → Run` и `_transRuns[index] → Run` для быстрого обновления стилей
- На каждой паре подписан `PropertyChanged`; при изменении `IsHovered`/`IsSelected` вызывается `ApplyPairStyle()`:
  - `IsSelected` → `Background=PickHighlightBrush`, `Foreground=White`, `FontWeight=SemiBold`
  - `IsHovered` (но не Selected) → `Background=HoverHighlightBrush`, остальные `ClearValue`
  - normal → все `ClearValue` (возврат к default'у TextBlock'а), `FontWeight=Medium/Normal` в зависимости от типа Run
- Aliases `WpfBrush`/`WpfBrushes` для разрешения конфликта с `System.Drawing` (в App включён WinForms из-за NotifyIcon)

**Проверка:** build 0/0.

**Эффект:** длинные предложения теперь рендерятся как нормальный текст с word-wrap по словам, hover/click по любому слову подсвечивает парный фрагмент на другой стороне. Карточка больше не обрезается.

---

### Fix v2: WrapPanel в ItemsControl не переносил длинные пары

Предыдущая попытка фикса обрезки sentence-card не помогла. Корневая причина — известный WPF bug: `WrapPanel` внутри `ItemsPanelTemplate` получает `Width=infinity` и не переносит элементы, даже когда parent имеет фиксированную ширину.

**`LinguaLens.App/Overlay/SentenceCardView.xaml`** — оба `ItemsControl` (Original и Translation):
- `<WrapPanel Width="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=ItemsControl}}"/>` — явно привязываем ширину WrapPanel к актуальной ширине ItemsControl. Это известный workaround.
- На Border'ах добавлен `MaxWidth="400"` — если LLM вернёт ОЧЕНЬ длинную пару (целое подпредложение как один chunk), Border не вылезет за пределы карточки
- На TextBlock внутри Border'ов добавлен `TextWrapping="Wrap"` — длинная пара переносится внутри себя
- ItemsControl'ам даны `x:Name="OriginalItems"` / `"TranslationItems"` для возможной отладки в дальнейшем

**Проверка:** build 0/0.

**Эффект:** длинные пары теперь:
1. Переносятся целиком на новую строку (через WrapPanel с constrained width)
2. Если одна пара слишком длинная — оборачивается внутри себя через TextWrapping

---

### Fix: обрезка sentence-card + понятный переход word → sentence

Два UX-бага после rich-translation шага.

**Fix 1 — обрезка sentence-card по ширине:**
- В шаге 3 был добавлен внешний `WrapPanel` вокруг кавычек «…» + `ItemsControl` с парами. Это ломало constrained-width: ItemsControl внутри горизонтального WrapPanel получает infinite-width и его внутренний WrapPanel не переносит длинные пары → пары вылазят за правый край карточки.
- `LinguaLens.App/Overlay/SentenceCardView.xaml`:
  - Убрана обёртка `<WrapPanel><TextBlock>«</TextBlock><ItemsControl/>...» </WrapPanel>` — теперь просто `<ItemsControl>` напрямую, как было до шага 3
  - Кавычки опущены (визуально section-label «ОРИГИНАЛ» достаточен)
  - Добавлен `MaxWidth="460"` на root StackPanel'е дополнительно к `Width="460"` для надёжного constrained-width

**Fix 2 — резкий переход word → sentence:**
Раньше при клике «Перевести предложение» окно скрывалось и появлялось в другом месте.
- `LinguaLens.App/Overlay/OverlayWindow.xaml.cs`:
  - `ShowLoading()` теперь принимает опциональный `string? statusOverride` — можно подменить «Перевожу… (Groq · llama-3.1-8b)» на любой текст
  - Убран вызов `RepositionWindow()` из `ShowResult`, `ShowSentenceResult`, `ShowError`. Позиционирование происходит ТОЛЬКО при `ShowAtPoint()` (initial show). При смене состояний loading → word → sentence окно стоит на месте, а не прыгает к курсору с учётом нового размера
- `LinguaLens.App/Overlay/DebounceController.cs OnTranslateSentenceRequested`:
  - Не вызывает больше `_overlay.ShowAtPoint(_lastPoint)` — окно уже показано (мы внутри word-карточки)
  - Вместо стандартного loading вызывает `_overlay.ShowLoading("Перевожу всё предложение…")` — пользователь видит явный статус что происходит, а не «карточка пропала, потом что-то появилось»

**Проверка:** build 0/0, тесты 93/93.

**Эффект:**
- Длинные пары sentence-card теперь нормально оборачиваются на новую строку, ничего не вылазит за границу карточки
- При клике «Перевести предложение» карточка остаётся на месте, контент плавно меняется на loading-state с явным «Перевожу всё предложение…», затем sentence-результат — пользователь видит непрерывный процесс

---

### Rich translation: Definition + Synonyms + правильно работающая кнопка «Перевести предложение»

Перевод стал не односложным (как у Lookupper Pro): большой блок-определение на русском, краткий перевод со стрелкой и акцентом, строка синонимов из исходного языка. Плюс починена кнопка «Перевести предложение» — она использует контекст-параграф, в котором было найдено слово.

**Часть 1 — расширенный перевод:**

- `LinguaLens.Core/Models/Models.cs` — `TranslationResult` расширен опциональными полями `Definition = ""` (полное толкование на русском) и `Synonyms = null` (синонимы на исходном языке)
- `LinguaLens.Infrastructure/Llm/LlmPrompts.cs WordTranslation` — добавлены поля `definition` и `synonyms` в JSON-схему. В описании `definition` явно сказано «полное толкование на русском в 1-2 предложениях, как в толковом словаре, с учётом выявленного домена». Translation теперь явно "краткий перевод одним-двумя словами".
- `LinguaLens.Infrastructure/Llm/GroqLlmClient.cs` + `GeminiLlmClient.cs` — парсят `definition` и `synonyms[]` через `TryGetProperty` (опциональные — старые ответы без этих полей не ломают парсер)
- `GroqLlmClient.cs` — `max_tokens` поднят `600 → 900` (определение + синонимы добавляют ~200 токенов к ответу)
- `LinguaLens.App/ViewModels/WordCardViewModel.cs` — добавлены `Definition`, `HasDefinition`, `SynonymsLine` (готовая строка "size, volume, limit"), `HasSynonyms`
- `LinguaLens.App/Overlay/WordCardView.xaml` — новая структура секции перевода:
  - **Definition** — крупный блок (FontSize 15, LineHeight 22) перед translation, как primary-контент
  - **Translation** — теперь в строке с оранжевой стрелкой `→` и FontSize.Translation (20) SemiBold
  - **Synonyms** — горизонтальная строка `Синонимы: size, volume, limit` (italic, secondary text)
  - **Comment** — остался как опциональный нюанс под всем

**Часть 2 — починка «Перевести предложение»:**

Кнопка раньше вызывала `ProcessSelectionAsync()` без аргументов, который пытался прочитать **выделение** через UIA. В word-mode у пользователя нет выделения → метод возвращал null → кнопка ничего не делала.

Решение: передавать через всю цепочку контекст-параграф, который extractor уже извлёк при показе word-card.

- `LinguaLens.App/ViewModels/WordCardViewModel.cs` — конструктор получил опциональный параметр `string contextSentence = ""`, хранится в `_contextSentence`. Событие типизировано: `event EventHandler<string>? TranslateSentenceRequested`. Команда `TranslateSentenceCommand` поднимает событие с `_contextSentence`.
- `LinguaLens.App/Overlay/OverlayWindow.xaml.cs`:
  - Событие `TranslateSentenceRequested` тоже типизировано `EventHandler<string>`
  - `ShowResult(TranslationResult result, string contextSentence = "")` — новый параметр, передаётся в VM
  - `OnTranslateSentenceRequested(object?, string)` — пробрасывает sentence наружу
- `LinguaLens.App/Overlay/DebounceController.cs`:
  - `ProcessWordAtPointAsync` — после успешного перевода передаёт `extracted.Sentence` в `_overlay.ShowResult(result, sentence)`
  - `OnTranslateSentenceRequested(object?, string contextSentence)` — если sentence непустой, вызывает `_orchestrator.ProcessSelectionAsync(token, preExtracted: sentence)`; иначе fallback на чтение выделения через UIA

**Проверка:** build 0/0, тесты 93/93.

**Эффект:**
- Карточка слова теперь показывает полное определение (1-2 предложения толкования) + краткий перевод + синонимы — намного информативнее «вместимость» в одну строку.
- Кнопка «Перевести предложение» переводит весь параграф контекста, без необходимости выделять текст руками.

---

### Domain-aware prompt — LLM явно определяет домен текста

В промпте word- и sentence-перевода добавлена инструкция: сначала определить домен (programming/finance/medicine/law/biology/...), затем выбирать перевод с учётом домена. Для полисемичных слов (`bank`, `capacity`, `class`, `argument`, `cell`, `spring`) домен — решающий фактор.

**Изменения в `LinguaLens.Infrastructure/Llm/LlmPrompts.cs`:**

- `WordTranslation` — добавлен 5-шаговый context-aware protocol:
  1. Определить домен из контекста по telltale signs (синтаксис кода, имена классов, мед-терминология, юр-фразы, ...)
  2. Выбрать значение слова по домену
  3. Использовать domain-specific Russian term (`capacity` в коде → «ёмкость/вместимость», не «потенциал»)
  4. В `comment` упомянуть домен **только если он меняет смысл** — иначе пустая строка
  5. Examples должны быть из того же домена, что и контекст
- `SentenceTranslation` — добавлен короткий блок: «сначала определи домен, переводи domain-specific термины правильным специализированным русским эквивалентом» (`class` → «класс» в IT vs «класс (таксон)» в биологии vs «класс (общественный)» в социологии)

**Структура JSON-ответа не менялась** — это чисто prompt engineering, парсеры/модели/UI не затрагиваются.

**Проверка:** build 0/0, тесты 93/93.

**Эффект:** для технических текстов перевод должен стать заметно точнее. Например, `capacity` в коде C# теперь должен переводиться как «ёмкость» (контейнера/коллекции), а не как «способность» или «потенциал».

---

### Context expansion — больше окружения для LLM

Для разрешения омонимии (`bank`: финансовый / речной / memory bank / database bank) LLM нужен полный абзац, а не одна строка. Расширен контекст в обоих extractor'ах.

**Изменения:**
- `LinguaLens.Infrastructure/TextExtraction/UiaTextExtractor.cs` — лимит на параграф `GetText(600)` → `GetText(1500)`. Теперь UIA отдаёт LLM до 1500 символов окружающего текста (целый абзац или несколько коротких).
- `LinguaLens.Infrastructure/TextExtraction/WindowsOcrService.cs` — `ContextSentence` теперь собирается из **всех** распознанных в регионе строк (`string.Join(" ", lines.Select(l => l.Text))`), а не из одной строки под курсором. Регион 800×200 px → весь текст внутри идёт в LLM как контекст.

**Зачем:** старый OCR-flow давал LLM ровно одну OCR-строку (часто 5-8 слов из-за PDF-переносов) — этого слишком мало для определения смысла слова в специализированном тексте (программирование, медицина, финансы). Теперь контекст ≈ абзац.

**Проверка:** build 0/0, тесты 93/93. Эффект увидим при следующем запросе перевода.

---

### OCR Fallback — поддержка PDF-читалок и приложений без UIA TextPattern

Foxit Reader, Adobe Reader и другие PDF-читалки не реализуют `TextPattern` в UI Automation — `UiaTextExtractor.ExtractWordAtPointAsync` возвращает `null`. Добавлен OCR-fallback через `Windows.Media.Ocr` (нативный Windows API), который захватывает регион 800×200 px вокруг курсора и распознаёт текст.

**Новые файлы:**
- `LinguaLens.Core/Interfaces/IOcrService.cs` — `Task<OcrResult?> ExtractTextNearAsync(Point screenPoint, CancellationToken ct)`
- `LinguaLens.Core/Models/Models.cs` — добавлены `record OcrLine(string Text, Rect Bounds)` и `record OcrResult(string? WordAtPoint, string ContextSentence, IReadOnlyList<OcrLine> Lines)`
- `LinguaLens.Infrastructure/TextExtraction/WindowsOcrService.cs` — реализация:
  - `Graphics.CopyFromScreen` захватывает регион 800×200 px вокруг курсора, обрезанный по `GetSystemMetrics(SM_*VIRTUALSCREEN)` (мульти-монитор)
  - `Bitmap` → PNG в `MemoryStream` → `InMemoryRandomAccessStream` через `DataWriter.WriteBytes/StoreAsync/DetachStream` → `BitmapDecoder.GetSoftwareBitmapAsync`
  - `OcrEngine.TryCreateFromUserProfileLanguages()` → fallback `en-US` → `es-ES`
  - Перебор `result.Lines.Words[].BoundingRect`, находит слово, в bbox которого попала точка курсора (в локальных координатах региона); возвращает слово (без хвостовой пунктуации через `StripPunctuation`) + всю строку как контекст

**Изменения:**
- `LinguaLens.Infrastructure/LinguaLens.Infrastructure.csproj`:
  - `TargetFramework`: `net8.0-windows` → `net8.0-windows10.0.19041.0` (нужно для доступа к WinRT `Windows.Media.Ocr`)
  - `SupportedOSPlatformVersion="10.0.17763.0"`
  - Добавлен `<PackageReference Include="System.Drawing.Common" Version="8.*" />`
- `LinguaLens.App/LinguaLens.App.csproj` — TFM поднят до `net8.0-windows10.0.19041.0` (требование совместимости с Infrastructure)
- `LinguaLens.Tests/LinguaLens.Tests.csproj` — TFM поднят (требование совместимости)
- `LinguaLens.Core/Services/TranslationOrchestrator.cs`:
  - Конструктор принимает опциональный `IOcrService? ocr = null`
  - Новый приватный `ExtractWithFallbackAsync(Point)`: UIA первым, fallback на OCR (3-секундный timeout), возвращает `WordExtractionResult(Word, Sentence, SourceApp="OCR", ScreenPoint)`. Логирует hit через `_logger.LogInformation`
  - `ExtractAsync` и `ProcessHoverAsync` теперь используют `ExtractWithFallbackAsync` вместо прямого `_extractor.ExtractWordAtPointAsync`
- `LinguaLens.App/App.xaml.cs` — DI: `services.AddSingleton<IOcrService, WindowsOcrService>()`. DI автоматически прокинет `IOcrService` в Orchestrator (заменив default `null` параметр)

**Параметры (по согласованию):**
- Регион OCR: **800×200 px** вокруг курсора (компромисс качества/скорости)
- Триггер: **только когда UIA вернул null** (никакой постоянной фоновой нагрузки)
- Выбор слова: **bbox содержит точку курсора** (если курсор между слов — не переводится)

**Требования среды:**
- Windows 10 1809+ (build 17763)
- Установленный OCR pack для языка: Settings → Time &amp; Language → Languages → Add language → Optional features → "Optical character recognition". По умолчанию для русской локали EN/ES OCR pack может быть не установлен — если ничего не распознаётся, добавь EN/ES вручную.

**Проверка:** `dotnet build LinguaLens.sln` → 0/0; `dotnet test` → 93/93.

**Эффект:**
- В Foxit Reader / Adobe Reader / любом PDF-вьювере: наведи курсор на английское слово → нажми Ctrl+Shift+Space → если UIA не сработал, происходит захват региона + OCR + перевод.
- Источник в карточке будет `OCR` (см. `SourceApp` в `VocabEntry`) — отличается от `manual`/обычных приложений.

---

### Design Sync — Шаг 5: Состояния Loading / Error / Saved

Завершающий шаг по дизайну. Saved-фидбек для WordCard, переписаны Loading и Error панели в `OverlayWindow` под спеку `misc-screens.jsx StateLoading/StateError`, рамка карточки становится `BadBrush` при ошибке.

**`ViewModels/WordCardViewModel.cs`:**
- Добавлен `bool IsSaved` с `INotifyPropertyChanged` и `SaveButtonLabel` (`"＋ Сохранить"` / `"✓ Сохранено"`)
- `SaveToVocabCommand` теперь:
  - Запускает `_vocab.SaveAsync(...)` fire-and-forget (UI не ждёт БД)
  - Переключает `IsSaved=true`
  - Через `DispatcherTimer` 1500 мс возвращает в `IsSaved=false`
  - `CanExecute = !IsSaved` (повторный клик во время feedback заблокирован)
- Сохранён исходный `TranslationResult` (`_result`) для повторного save при перерисовке

**`Overlay/WordCardView.xaml`** — кнопка Save:
- `Content` биндится на `SaveButtonLabel`
- `Style.Triggers` по `IsSaved=True` → `Foreground` и `BorderBrush` меняются на `OkBrush` (зелёный фидбек)

**`Overlay/OverlayWindow.xaml`** — `LoadingPanel` переписан:
- Ширина `280` (раньше `260`)
- Header-row: 3 placeholder (word `80×22`, POS-pill capsule `44×16 / radius 999`, transcription `50×14`) — повторяет header реальной карточки
- Dashed-divider вместо solid `Separator`
- Translation-bar `180×22` + 2 comment-bars (`240×12`, `180×12`)
- **Spinner-row снизу**: вращающийся `Ellipse` (Stroke=`AccentBrush`, StrokeDashArray="3 2", `RotateTransform` через `EventTrigger Loaded → Storyboard RepeatBehavior=Forever`, 0.9 с период) + динамический `LoadingStatusText` (например, `"Перевожу… (Groq · llama-3.1-8b)"`)
- Footer-bars удалены (избыточно — spinner-row их заменяет)

**`Overlay/OverlayWindow.xaml`** — `ErrorPanel` переписан:
- Ширина `280` (раньше `240`)
- Иконка `⚠` цвет → `BadBrush` (раньше хардкод `#E24B4A`)
- Заголовок цвет → `PrimaryTextBrush` SemiBold (раньше хардкод `#A32D2D` Medium) — красная теперь только рамка карточки и иконка
- Body-text: более развёрнутый, мягкий межстрочный (`LineHeight=16`)
- Кнопки `↻ Повторить` / `⚙ Настройки` — `SmallOutlineButton`, Settings с приглушённым `TertiaryTextBrush` foreground

**`Overlay/OverlayWindow.xaml`** — добавлен `x:Name="RootBorder"` внешнему Border'у, чтобы можно было перекрашивать рамку.

**`Overlay/OverlayWindow.xaml.cs`:**
- `ShowLoading()` — собирает `modelLabel` из `_settings.LlmProvider` (`"Groq · llama-3.1-8b"` или `"Gemini · gemini-2.0-flash"`), пишет в `LoadingStatusText`; вызывает `RestoreNormalBorder()`
- `ShowResult(...)` / `ShowSentenceResult(...)` — `RestoreNormalBorder()` перед отображением (на случай возврата из error-state)
- `ShowError()` — `RootBorder.SetResourceReference(BorderBrushProperty, "BadBrush")` (вся рамка карточки красная)
- Новый приватный `RestoreNormalBorder()` — возвращает `BorderBrush` к `CardBorderBrush` через `SetResourceReference` (важно: не через прямой `Brush` — иначе DynamicResource потеряется при смене темы)

**Что НЕ сделано в этом шаге (по плану):**
- Настоящая shimmer-анимация на placeholder bars (анимированный `LinearGradientBrush`) — пока статичный `DividerBrush` (`#592A2826` light), визуально это «серые палочки», не движущийся блик. Реализация — отдельный мини-шаг (требует Storyboard на `LinearGradientBrush.GradientStop.Offset` для каждого Rectangle).
- Динамический `ErrorBodyText` — сейчас статический. Чтобы показывать реальное сообщение исключения, нужно передавать его в `ShowError(string message)` — это уже выходит за рамки шага.
- Saved-state для SentenceCard pick-режима — уже сделан в Шаге 2 (`SentenceMode.Saved` + 1.6 с DispatcherTimer + зелёная подсказка).

**Проверка:** `dotnet build LinguaLens.sln` → 0/0; `dotnet test` → 93/93.

**Эффект сейчас:**
- Клик `＋ Сохранить` на карточке слова → кнопка становится `✓ Сохранено` с зелёной рамкой и текстом, через 1.5 с возвращается в исходное состояние. Повторный клик во время feedback заблокирован.
- Во время LLM-запроса показывается компактный набор placeholder-палочек (повторяющий layout итоговой карточки) + вращающийся spinner + строка с провайдером и моделью.
- При ошибке рамка всей карточки становится `BadBrush` (красно-оранжевая), `⚠` иконка тоже красная, заголовок и описание обычного цвета, две кнопки повторить/настройки.

---

### Design Sync — Шаг 4: SettingsWindow sidebar + segmented controls

Полная перекомпоновка окна настроек по дизайну `misc-screens.jsx SettingsWindow`. Custom chrome, sidebar с переключаемыми вкладками, pill-toggles вместо CheckBox, segmented pills для провайдера и темы, удалены эмодзи 👁.

**`Windows/SettingsWindow.xaml`** — полная перепись:
- `WindowStyle="None" AllowsTransparency="True"` — custom chrome (раньше стандартный Windows-titlebar).
- Внешний `Border` с paper-фоном, контрастной рамкой 1.5 px, `CardCornerRadius=10` и `CardShadowEffect` (Margin=8 чтобы тень не обрезалась).
- **Header bar** (Row 0): `PaperAltBrush` фон, нижний border 1.5 px, заголовок "Настройки" + кнопка `✕` справа. `MouseLeftButtonDown` → `DragMove()` для перемещения окна.
- **Sidebar** (Column 0, 140 px): 5 `RadioButton` (`SidebarItemStyle`), активный получает `PaperAltBrush` фон + `CardBorderBrush` рамку + SemiBold. Hover — `ButtonHoverBrush`. Вертикальный `LineSoftBrush` border между sidebar и контентом.
- **Content** (Column 1): 5 `StackPanel` (`ApiPanel/BehaviorPanel/AppearancePanel/SystemPanel/UsagePanel`) накладываются друг на друга в одном Grid-cell, видна только одна (Visibility управляется `ShowPanel(target)` из code-behind по `Checked` sidebar-кнопок).
- **SettingRow** реализован inline через `Grid` с двумя колонками `160`/`*` (без отдельного UserControl — проще читать).

**Контролы вкладок:**
- **API**:
  - Провайдер: 2 `RadioButton` `SegmentedPillStyle` (Groq / Gemini) — capsule pills, активный получает `PrimaryTextBrush` фон + `CardBackgroundBrush` текст
  - API ключ: `PasswordBox` (mono шрифт) + опциональный plain `TextBox` (toggle через кнопку "показать"/"скрыть"); эмодзи 👁 удалён.
- **Поведение**:
  - Debounce: `Slider 100..1000` с границами `100` / `1000` слева/справа, mono-метка `400ms` справа (вместо просто числа)
  - 3 `CheckBox` `PillToggleStyle` (Английский / Испанский / Авто-сохранение) — кастомный шаблон: capsule 32×18, ползунок 11px из `CardBackgroundBrush`, on-state — заполнение `AccentBrush` + ползунок справа
- **Внешний вид**: тема через 2 `RadioButton` `SegmentedPillStyle` (Светлая / Тёмная) — раньше обычные RadioButton без визуала
- **Система**: hotkey (`TextBox` mono) + StartWithWindows pill-toggle
- **Использование**: компактный блок (`PaperAltBrush` фон + dashed `LineSoftBrush` border, `ChipCornerRadius=6`) с заголовком, `ProgressBar` (Foreground меняется по уровню: `OkBrush` < 80%, `AccentBrush` 80-90%, `BadBrush` >= 90%) и строкой `42 100 / 100 000 токенов  $0.00 · Groq free`. Раньше было 2 отдельные UniformGrid-карточки.

**Стили (локально в `Window.Resources`):**
- `SidebarItemStyle` (RadioButton как ghost-кнопка с active-состоянием)
- `SegmentedPillStyle` (RadioButton-pill: capsule, тонкая рамка, dark-active)
- `PillToggleStyle` (CheckBox как iOS-toggle: track + thumb)
- `SettingLabelStyle` / `SettingHintStyle` / `DashedDividerStyle`

**`Windows/SettingsWindow.xaml.cs`** — полная перепись:
- `LoadValues()` / `SaveValues()` — отдельные методы, читают/пишут в `IAppSettings`
- `WireEvents()` — события sidebar.Checked, Slider.ValueChanged (sync mono-метки), ShowKeyBtn (toggle PasswordBox/TextBox), ResetUsageBtn, CloseBtn (save + close)
- `ShowPanel(WpfPanel target)` — переключение видимости. Alias `WpfPanel = System.Windows.Controls.Panel` нужен из-за конфликта `System.Windows.Forms.Panel`.
- **Удалена отдельная кнопка "Сохранить"** — настройки сохраняются при закрытии (✕). Это соответствует дизайну.
- `OnHeader_MouseLeftButtonDown` → `DragMove()` для перемещения окна за header bar
- `RefreshUsageAsync()` — обновляет `UsageBar.Value` и Foreground по уровню (`FindResource(brushKey)`)

**Что НЕ сделано в этом шаге (по плану):**
- Кастомный стиль `Slider` thumb (сейчас стандартный WPF, выглядит немного плоско в стилизованной палитре)
- Hotkey-recorder (TextBox принимает строку как есть; запись через нажатие клавиш — отдельная фича)
- Анимация перехода между вкладками (статичное переключение Visibility)

**Проверка:** `dotnet build LinguaLens.sln` → 0/0; `dotnet test` → 93/93.

**Эффект сейчас:**
- Окно настроек выглядит как карточка LinguaLens (paper-фон, контрастная рамка, тень), а не как стандартный Windows-диалог.
- Sidebar даёт быструю навигацию между 5 разделами без скролла.
- Все on/off настройки — capsule-toggles (как iOS); провайдер и тема — segmented pills.
- Эмодзи 👁 удалена; API key переключается текстовой кнопкой "показать"/"скрыть".
- Использование показано одним блоком с зелёным/янтарным/красным прогрессом и компактной строкой.

---

### Design Sync — Шаг 3: Border-обёртки карточек + типографика

Визуальное "карточное" оформление: paper-фон, контрастная рамка, скругление 10 px, тень (light), capsule POS-pill, абстрактный SVG-флаг, dashed-разделители, Expander для примеров, кавычки «…» в оригинале, letter-spacing на section-labels. Все размеры/шрифты теперь идут через `Tokens.xaml`.

**Новые файлы:**
- `LinguaLens.App/Converters/LetterSpacedConverter.cs` — value-конвертер, эмулирующий CSS `letter-spacing` для коротких UPPERCASE-лейблов (вставляет U+2009 thin space между символами). Используется как `Text="{Binding Source=ОРИГИНАЛ, Converter={StaticResource LetterSpacedConverter}}"`. WPF не поддерживает разрядку нативно, это самый компактный fallback.

**Изменения:**
- `App.xaml` — добавлен ресурс `LetterSpacedConverter`.
- `OverlayWindow.xaml` — внешний `Border` теперь использует токены: `CornerRadius={StaticResource CardCornerRadius}` (10), `BorderThickness={StaticResource CardBorderThickness}` (1.5), `Effect={DynamicResource CardShadowEffect}` (раньше был inline `DropShadowEffect` хардкодом). Добавлен `Margin="8"` чтобы тень не обрезалась границей окна.
- `ViewModels/WordCardViewModel.cs` — переписан:
  - `PosDisplay` `"[noun]"` → `Pos` + `PosUpper` (`"NOUN"`) + `HasPos`
  - `FlagEmoji` (🇬🇧/🇪🇸) → `LangCode` (`"EN"`/`"ES"`); сам флаг рисуется в XAML
  - Добавлены `HintLabel` (`"WORD MODE · EN"`) и `ExamplesHeader` (`"Примеры (N)"`)
  - **Удалён** `CopyTranslationCommand` (по спеке)
- `Overlay/WordCardView.xaml` — полная перерисовка под дизайн WordCardA:
  - Hint-row "WORD MODE · EN" сверху (с letter-spacing)
  - Header: word `FontSize.Word=22 / SemiBold`, POS-pill capsule (`PillCornerRadius=999`, бежевый фон, тёмный текст, тонкая рамка `PillBorderThickness=1.2`, UPPERCASE + letter-spacing), транскрипция моноширинным `MonoFontFamily`, справа SVG-флаг (Border + 2 Line — generic 3-stripe) + код `EN`/`ES`
  - `Separator` → dashed `<Line StrokeDashArray="2 3"/>`
  - Translation `FontSize.Translation=20 / SemiBold` (раньше Medium), `LineHeight=24`
  - Comment `FontSize.Comment=13` (раньше 12), italic, `LineHeight=18`
  - Примеры теперь в свёрнутом `Expander` с заголовком `"Примеры (N)"`
  - Второй dashed-divider перед footer
  - Footer: 2 кнопки `[＋ Сохранить]` + `[Перевести предложение]` (растягивается через `Grid.ColumnDefinition Width="*"`). Кнопка "Копировать" удалена.
  - Все размеры/шрифты через `{StaticResource ...}` из `Tokens.xaml`
- `Overlay/SentenceCardView.xaml`:
  - `ModeLabel`, "ОРИГИНАЛ" и "ПЕРЕВОД" теперь рендерятся через `LetterSpacedConverter`
  - Оригинал обёрнут в `WrapPanel` с типографскими кавычками «…» по краям

**Что НЕ сделано в этом шаге (по плану):**
- Стилизация кнопок Save/PrimaryButton/GhostButton — пока всё через тот же `SmallOutlineButton`. Полноценный набор стилей кнопок (с правильным `CardCornerRadius=8` и `ButtonBorderThickness=1.5`) можно вынести в шаг 5 или отдельно.
- Подключение настоящего шрифта Inter (.ttf) — fallback на Segoe UI Variable/Segoe UI продолжает работать.
- Реальный флаг с национальными цветами (сейчас generic 3-stripe монохром по дизайну).

**Проверка:** `dotnet build LinguaLens.sln` → 0/0; `dotnet test` → 93/93.

**Эффект сейчас:**
- Карточка получила визуальную "плоть": тёплый paper-фон `#FBF9F4`, контрастная рамка `#2A2826 / 1.5 px`, скругление 10 px, мягкая тень. В Dark — тёплый `#242220`, белая полупрозрачная рамка, тень отключена.
- POS-бейдж стал capsule UPPERCASE (например `N O U N`) на бежевом фоне с тонкой рамкой — раньше был синий box со скобками.
- Транскрипция — настоящий моноширинный шрифт (Cascadia Mono / Consolas вместо Consolas-hardcoded).
- Примеры свёрнуты по умолчанию в Expander — карточка стала компактнее в норм-состоянии.
- Section-labels (`ОРИГИНАЛ`, `ПЕРЕВОД`, `WORD MODE · EN`) — с разрядкой между символами, выглядит как дизайнерские labels.

---

### Design Sync — Шаг 2: SentenceCard с alignment-парами + picker

Самый крупный продуктовый шаг — затрагивает Core / Infrastructure / App. LLM теперь возвращает выровненные пары `original ↔ translation`, UI рендерит их через `WrapPanel`, hover подсвечивает парный фрагмент на обеих сторонах, picker-режим позволяет выбрать фрагменты и сохранить их в словарь.

**Core / Models:**
- `LinguaLens.Core/Models/Models.cs` — добавлен `record AlignedPair(string Original, string Translation)`; `SentenceTranslationResult` расширен полями `IReadOnlyList<AlignedPair>? Pairs = null` и `string DetectedLang = "en"`. Опциональные параметры → старые места создания (тесты, моки) остаются совместимыми.

**Infrastructure:**
- `LinguaLens.Infrastructure/Llm/LlmPrompts.cs` — `SentenceTranslation(...)` обновлён: LLM просим вернуть массив `pairs[{original, translation}]` с одинаковым порядком на обеих сторонах, фрагменты по 1–4 слова.
- `LinguaLens.Infrastructure/Llm/GroqLlmClient.cs` — `ParseSentenceResult` теперь принимает `lang` и читает `pairs[]` через `TryGetProperty` (если поле отсутствует → `Pairs = []`, fallback на цельный перевод); кладёт `DetectedLang = lang` в результат. `TranslateSentenceAsync` передаёт `lang` в парсер.
- `LinguaLens.Infrastructure/Llm/GeminiLlmClient.cs` — идентичные изменения.

**App / ViewModels:**
- `RelayCommand.cs` — расширен: добавлен опциональный `Func<bool>? canExecute`, `CanExecuteChanged` теперь подключён к `CommandManager.RequerySuggested` (раньше был пустой обработчик с `#pragma 67`). Добавлен generic `RelayCommand<T>` для команд с параметром (нужен для `TogglePairCommand(AlignedPairViewModel)`).
- `ViewModels/AlignedPairViewModel.cs` (новый) — обёртка над `AlignedPair`. Хранит `Index`, `Original`, `Translation` + три булевых `IsHovered` / `IsSelected` / `IsInPickingMode` с `INotifyPropertyChanged`. XAML биндится на эти свойства для смены фона/жирности через `Style.Triggers`.
- `ViewModels/SentenceCardViewModel.cs` — полная перепись:
  - `enum SentenceMode { Normal, Picking, Saved }` + свойства `Mode`, `IsNormal`, `IsPicking`, `IsSaved`
  - `Pairs : IReadOnlyList<AlignedPairViewModel>`, `HasPairs`, `ModeLabel` ("SENTENCE MODE · EN → RU"), `HintText` (динамическая подсказка под mode), `HintIsSaved`
  - `SelectedCount`, `SaveButtonText` ("Сохранить N")
  - Команды `StartPickingCommand`, `CancelPickingCommand`, `SaveSelectedCommand` (CanExecute = `SelectedCount > 0`), `TogglePairCommand`
  - `SetHovered(int?)` — синхронная подсветка пары на обеих сторонах (вызывается из code-behind при `MouseEnter/Leave`)
  - `SaveSelected()` — синтезирует `TranslationResult` на каждую выбранную пару (`Word=Original`, `Translation=Translation`, остальное пустое) и пишет в `IVocabRepository`, переключает Mode → `Saved`, через 1.6 c `DispatcherTimer` возвращает `Normal`
  - Старый `CopyCommand` удалён по спеке

**App / View:**
- `Overlay/SentenceCardView.xaml` — полная перепись:
  - Hint-row сверху (`ModeLabel` слева, `HintText` справа, цвет меняется на `OkBrush` при `Saved`)
  - Section-label "ОРИГИНАЛ" (uppercase, `FontSize.Label`, tertiary)
  - `ItemsControl` + `WrapPanel` для оригинала: `Border` со `Style.Triggers` по `IsHovered` (`HoverHighlightBrush`) и `IsSelected` (`PickHighlightBrush`); `TextBlock` italic, `FontSize.Body=14`, на `IsSelected` foreground → белый и SemiBold
  - Dashed divider (`Line` + `StrokeDashArray="2 3"`, `LineSoftBrush`)
  - Section-label "ПЕРЕВОД" + ещё один `ItemsControl` с `WrapPanel` для пары перевода (Medium, `FontSize.SentenceBody=17`)
  - Fallback: если `HasPairs=false`, под секцией показывается цельный `Translation` (триггер по `HasPairs`)
  - Comment-quote (`BorderThickness="2,0,0,0"`, `LineSoftBrush`, italic)
  - Footer: в Normal — кнопка "＋ Слова в словарь" (`StartPickingCommand`); в Picking — "выбрано N" + [Отмена] + [Сохранить N] (Save с `SmallOutlineButton` пока, primary-стиль будет в шаге 3)
  - Ширина: `Width="460"` (по спеке default), без `MinWidth/MaxWidth`
- `Overlay/SentenceCardView.xaml.cs` — code-behind с обработчиками `OnPair_MouseEnter/MouseLeave/MouseLeftButtonDown`, читает `DataContext` Border'а как `AlignedPairViewModel` и зовёт `SetHovered` / `TogglePairCommand`. Aliases `WpfUserControl` / `WpfMouseEventArgs` нужны для разрешения конфликта `System.Windows.Forms.*` (в App включены и WPF и WinForms из-за NotifyIcon).
- `Overlay/OverlayWindow.xaml.cs` `ShowSentenceResult` — добавлены опциональные параметры `contextSentence`, `sourceApp`, и при создании `SentenceCardViewModel` теперь прокидывается `_vocab` (для `SaveSelected`).

**Что НЕ сделано в этом шаге (по плану):**
- Стилизация кнопок footer (PrimaryButton для Save, GhostButton для Отмена) — будет в шаге 3
- Кавычки «...» вокруг оригинала и `letter-spacing` для section-label — в шаге 3
- Передача `contextSentence` / `sourceApp` из `DebounceController` в `ShowSentenceResult` — пока используются дефолты ("" и "Sentence picker"); реальный sentence-extractor пока не пробрасывает эти данные сюда
- Border-обёртка карточки с paper-фоном и тенью — шаг 3

**Проверка:** `dotnet build LinguaLens.sln` → 0/0; `dotnet test` → 93/93 проходят.

**Эффект сейчас:**
- LLM при следующем запросе sentence-перевода вернёт расширенный JSON; парсер прочитает `pairs[]` если оно есть, иначе list останется пустым (UI покажет fallback с цельным переводом).
- В нормальном режиме под предложением видна кнопка "＋ Слова в словарь"; при клике карточка переходит в picker — клик по фрагменту подсвечивает пару оранжевым и считает её; кнопка "Сохранить N" пишет выбранные пары в vocab и через 1.6 с возвращает карточку в normal с зелёной подписью "✓ сохранено".

---

### Design Sync — Шаг 1: Tokens.xaml + темы

Фундамент под перенос дизайна из `design_handoff_lingualens_ui/` в WPF. Без визуальной обёртки карточек — она будет в шаге 3.

**Новые файлы:**
- `LinguaLens.App/Themes/Tokens.xaml` — невизуальные токены: `UiFontFamily` / `MonoFontFamily` (с family-fallback `Inter → Segoe UI Variable → Segoe UI` и `JetBrains Mono → Cascadia Mono → Consolas`), размеры шрифтов (`FontSize.Word=22`, `FontSize.Translation=20`, `FontSize.SentenceBody=17`, `FontSize.Body=14`, `FontSize.Comment=13`, `FontSize.Caption=12`, `FontSize.Mono=12`, `FontSize.Label=10`), веса (`FontWeight.Display=SemiBold`, `FontWeight.Body=Normal`, `FontWeight.Label=SemiBold`), радиусы (`CardCornerRadius=10`, `ButtonCornerRadius=8`, `PillCornerRadius=999`, `ChipCornerRadius=6`), толщины (`CardBorderThickness=1.5`, `ButtonBorderThickness=1.5`, `PillBorderThickness=1.2`), padding (`CardPadding=16,14`, `ButtonPadding=12,7`, `PillPadding=8,2`)

**Изменения:**
- `LightTheme.xaml` — палитра приведена к спеке `styles.css`:
  - `CardBackgroundBrush` `#FFFFFF` → `#FBF9F4` (тёплый paper)
  - `CardBorderBrush` `#E5E5E5` → `#2A2826` (жирная контрастная рамка)
  - `PrimaryTextBrush` `#1A1A1A` → `#1F1D1B`, `SecondaryTextBrush` `#6B6B6B` → `#4A4742`, `TertiaryTextBrush` `#9B9B9B` → `#8A857F`
  - `AccentBrush` `#185FA5` (синий) → `#C9712A` (оранжевый)
  - `PosBadgeBackgroundBrush` `#E6F1FB` → `#F2E4D2` (бежевый), `PosBadgeForegroundBrush` `#0C447C` → `#1F1D1B`
  - `DividerBrush` `#EBEBEB` → `#592A2826` ARGB (rgba(40,38,36,.35))
  - `ButtonBorderBrush` `#D0D0D0` → `#2A2826`, `ButtonHoverBrush` `#F5F5F5` → `#F4F1EA`
  - **Добавлены** `PaperAltBrush #F4F1EA`, `AccentSoftBrush #F2E4D2`, `HoverHighlightBrush #F0DAB5`, `PickHighlightBrush #D88550`, `OkBrush #5FA678`, `BadBrush #C45838`, `LineSoftBrush #592A2826`, `CardShadowEffect` (DropShadowEffect, Opacity 0.12, BlurRadius 12, ShadowDepth 4, Direction 270)
- `DarkTheme.xaml` — палитра приведена к спеке:
  - `CardBackgroundBrush` `#1E1E1E` → `#242220`, `PaperAltBrush` `#2E2C2A`
  - `PrimaryTextBrush` `#F0F0F0` → `#F0ECE4`, `SecondaryTextBrush` `#A0A0A0` → `#B8B0A4`, `TertiaryTextBrush` `#6B6B6B` → `#756E65`
  - `CardBorderBrush` `#3A3A3A` → `#2EFFFFFF` (rgba(255,255,255,.18)), `DividerBrush` → `#24FFFFFF`
  - `AccentBrush` `#378ADD` → `#D88550`, `AccentSoftBrush #3A2D22`
  - `PosBadgeBackgroundBrush` `#0C447C` → `#14FFFFFF`, `PosBadgeForegroundBrush` `#B5D4F4` → `#F0ECE4`
  - **Добавлены** те же ключи что и в light: `HoverHighlightBrush #7A4E3A`, `PickHighlightBrush #A85528`, `OkBrush #7BC18F`, `BadBrush #E07355`, `LineSoftBrush`
  - `CardShadowEffect` с `Opacity=0` — тень отключена в dark, но ключ присутствует чтобы `DynamicResource` не падал
- `App.xaml` — `MergedDictionaries` теперь два словаря: `[0]` Tokens.xaml (статика), `[1]` LightTheme.xaml (тема, подменяется)
- `App.xaml.cs` `ApplyTheme()` — подменяет `merged[1]` вместо `merged[0]`, чтобы не затирать Tokens.xaml при смене темы

**Не сделано в этом шаге (по плану):**
- Реальный шрифт Inter через .ttf и pack URI — пока работает только системный fallback (Segoe UI Variable / Segoe UI). Подключение настоящего Inter — отдельный шаг при необходимости.
- `letter-spacing` для section-label / POS-pill — WPF не поддерживает CSS-style letter-spacing нативно; будет решаться при отрисовке через расстановку пробелов в Run или AttachedProperty.
- Стили `PrimaryButton` / `GhostButton` / `SectionLabel` / `WordHeadline` — будут добавлены в шаге 3 при перерисовке карточек.

**Проверка:** `dotnet build src/LinguaLens.App` → 0 ошибок / 0 предупреждений.

**Эффект сейчас:** в существующих местах, использующих `DynamicResource` (POS-бейдж и кнопки в WordCardView, разделитель в SentenceCardView, фон оверлея), произойдёт смена цветов — синий accent уйдёт на оранжевый, POS станет бежевым, рамки потенциально станут жирнее. Карточка пока без Border-обёртки — paper-фон и тень станут видны только после шага 3.

---

### Phase 5 — API Infrastructure

**Новые файлы:**
- `LinguaLens.Infrastructure/Llm/LlmPrompts.cs` — промпты для слова и предложения (`$"""..."""` double-dollar raw strings для совместимости JSON + интерполяции)
- `LinguaLens.Infrastructure/Llm/ApiKeyHandler.cs` — `DelegatingHandler`, добавляет `Authorization: Bearer` per-request из `IAppSettings.ApiKey`
- `LinguaLens.Infrastructure/Llm/LlmClientFactory.cs` — фабрика, переключается между Groq/Gemini по `settings.LlmProvider`; метод `Create()` виртуальный для Moq
- `LinguaLens.Core/Interfaces/ILlmClientFactory.cs` — интерфейс в Core (избегает циклической зависимости Core→Infrastructure)

**Изменения:**
- `GroqLlmClient` — `SendAsync` возвращает `(Content, PromptTokens, CompletionTokens)`; `ITokenUsageRepository` опциональный (null-safe для тестов); fire-and-forget `RecordAsync`
- `GeminiLlmClient` — то же; парсит `usageMetadata.promptTokenCount/candidatesTokenCount`
- `TranslationOrchestrator` — принимает `ILlmClientFactory` вместо `ILlmClient`; optional `ILogger`; `cache.SetAsync` и `vocab.SaveAsync` fire-and-forget
- `TranslationOrchestratorTests` — добавлен `Mock<ILlmClientFactory>`

---

### Блок 1 — Token Usage Tracking

**Новые файлы:**
- `LinguaLens.Core/Models/Models.cs` — добавлены `TokenUsageEntry`, `DailyUsageSummary`
- `LinguaLens.Core/Interfaces/ITokenUsageRepository.cs` — `RecordAsync`, `GetTodaySummaryAsync`, `GetMonthSummaryAsync`, `ResetAsync`
- `LinguaLens.Infrastructure/Data/SqliteTokenUsageRepository.cs` — реализация; стоимость Groq=$0, Gemini=$0.1/1M prompt + $0.4/1M completion
- `LinguaLens.Infrastructure/Migrations/AddTokenUsage` — создаёт таблицу `token_usage`

**Изменения:**
- `LinguaLensDbContext` — добавлен `DbSet<TokenUsageEntity>`, таблица `token_usage`
- `IAppSettings` / `AppSettings` — добавлены `DailyTokenLimit` (100 000) и `WarnAtEightyPercent` (true)
- `TrayIconManager` — `ShowWarning(title, message)` через `ShowBalloonTip`; info-item с токенами в меню
- `SettingsWindow` — новая секция "Использование API": карточки Today/Cost, прогресс-бар (зелёный/янтарный/красный), лимит, чекбокс 80%, кнопка сброса

---

### Блок 2 — UI Themes & Overlay

**Новые файлы:**
- `LinguaLens.App/Themes/LightTheme.xaml` — 11 кистей (Card, Text×3, PosBadge×2, Divider, Button×2, Accent)
- `LinguaLens.App/Themes/DarkTheme.xaml` — те же ключи, тёмные значения
- `LinguaLens.App/Overlay/SentenceCardView.xaml/.cs` — карточка перевода предложения
- `LinguaLens.App/ViewModels/SentenceCardViewModel.cs` — Translation, Comment, CopyCommand
- `LinguaLens.App/ViewModels/RelayCommand.cs` — стандартный `ICommand` wrapper
- `LinguaLens.App/Converters/StringToVisibilityConverter.cs` — `Collapsed` если null/empty

**Изменения:**
- `App.xaml` — `MergedDictionaries[0]` = LightTheme; `BoolToVisibilityConverter`, `StringToVisibilityConverter`, стиль `SmallOutlineButton` с hover через `DynamicResource`
- `App.xaml.cs` — `ApplyTheme()` заменяет `[0]` без очистки статичных стилей; подписка на `PropertyChanged` для live-смены темы; DI: `ITokenUsageRepository`, `OverlayWindow` фабрика, `TrayIconManager` с usageRepo
- `OverlayWindow.xaml` — `ShowActivated="False" Focusable="False"` (устранение мерцания и кражи фокуса); 4 панели: Loading shimmer, WordCard, SentenceCard, ErrorPanel
- `OverlayWindow.xaml.cs` — `ShowAtPoint` с `Screen.FromPoint()` для мультимонитора; `ShowResult/ShowSentenceResult/ShowLoading/ShowError`; FadeIn 150ms / FadeOut 100ms; события `TranslateSentenceRequested`, `RetryRequested`, `OpenSettingsRequested`
- `WordCardView.xaml` — полный MVVM-переписан: POS-бейдж, транскрипция, флаг, примеры, все `DynamicResource`
- `WordCardViewModel.cs` — `CopyTranslationCommand`, `SaveToVocabCommand`, `TranslateSentenceCommand`
- `DebounceController` — восстановлен `ShowLoading()` (безопасен с `ShowActivated="False"`); `OnTranslateSentenceRequested`, `OnRetryRequested`, `CheckUsageWarningAsync` (80% порог), midnight timer

---

### Исправленные конфликты

| Проблема | Решение |
|---|---|
| Мерцание + кража фокуса | `ShowActivated="False"` + `Focusable="False"` на OverlayWindow |
| Циклическая зависимость Core→Infrastructure | `ILlmClientFactory` интерфейс вынесен в Core |
| CS9006 в LlmPrompts.cs | `$"""..."""` double-dollar raw strings |
| `ILogger` не найден в Core | `Microsoft.Extensions.Logging.Abstractions` добавлен в Core.csproj |
| `AddHttpClient` не найден | `Microsoft.Extensions.Http` добавлен в App.csproj |
| Тесты сломались после нового ctor GroqLlmClient | `ITokenUsageRepository` сделан nullable с default null |
| Мультимонитор | `Screen.FromPoint()` вместо `SystemParameters.PrimaryScreenWidth` |

---

**Итог:** сборка 0 ошибок / 0 предупреждений, 93/93 тестов проходят.
