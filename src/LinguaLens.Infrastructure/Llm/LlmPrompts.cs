namespace LinguaLens.Infrastructure.Llm;

public static class LlmPrompts
{
    public static string WordTranslation(string word, string sentence, string lang) => $$"""
        You are a language learning assistant helping a Russian speaker understand {{(lang == "es" ? "Spanish" : "English")}} text.

        Word (may be OCR-corrupted): "{{word}}"
        Context: "{{sentence}}"

        IMPORTANT — OCR correction:
        The word may come from imperfect OCR — it can have wrong case in the middle (e.g. "StriYing"
        for "Striving"), missing/added characters, or look like a non-word. If the word looks corrupted,
        INFER the most likely real word from the context and translate THAT word. Set "word" in the
        output to the corrected form. If the word looks correct, use it as-is.

        IMPORTANT — context-aware translation protocol:
        1. First, identify the DOMAIN of the context (one of: programming, finance, medicine, law,
           biology, physics, chemistry, sports, cooking, military, gaming, music, general, etc.).
           Look for telltale signs: code syntax, library/class names, financial terms, anatomical
           terms, legal phrasing, etc.
        2. Choose the meaning of "{{word}}" that fits this domain. For polysemous words
           (e.g. "bank", "capacity", "class", "argument", "cell", "spring"), the domain is decisive.
        3. Translate accordingly. If the word has a domain-specific term in Russian
           (e.g. "capacity" in programming → "ёмкость/вместимость" for collections, not "потенциал"),
           use that term.
        4. In "comment" mention the detected domain ONLY if it materially affects the translation
           — e.g. "В контексте программирования — поле структуры данных". For obvious/general
           contexts, leave comment empty.
        5. Examples must reflect the SAME domain as the context. Don't give generic examples
           if the word has a clear specialized meaning here.

        Respond with ONLY valid JSON, no markdown fences, no explanation:
        {
          "word": "the word as it appears in text",
          "detected_lang": "{{lang}}",
          "pos": "noun|verb|adjective|adverb|other",
          "transcription": "IPA transcription (for English only, empty string for Spanish)",
          "definition": "полное толкование на русском в 1-2 предложениях, как в толковом словаре, с учётом выявленного домена. Раскрой смысл, не просто переведи. Пример: для слова 'capacity' в коде — 'Максимальное количество элементов, которое может вместить структура данных (массив, коллекция).'",
          "translation": "краткий перевод одним-двумя словами с учётом домена",
          "synonyms": ["3-5 синонимов", "на исходном языке", "из того же домена"],
          "comment": "если домен меняет смысл — упомяни его кратко, иначе пустая строка",
          "examples": [
            {"original": "example sentence in {{(lang == "es" ? "Spanish" : "English")}} matching the domain", "translation": "перевод на русский"},
            {"original": "another domain-matching example", "translation": "перевод"}
          ]
        }
        """;

    public static string SentenceTranslation(string text, string lang) => $$"""
        Translate the following {{(lang == "es" ? "Spanish" : "English")}} text to Russian.
        Provide a natural, fluent translation.

        IMPORTANT: first identify the DOMAIN of the text (programming, finance, medicine, law,
        biology, physics, sports, gaming, general, etc.) and translate domain-specific terms
        with the correct specialized Russian equivalents. For example, "class" in programming
        is "класс", in biology — "класс (таксон)", in sociology — "класс (общественный)".
        Don't fall back to a generic dictionary translation when a domain term exists.

        CRITICAL — punctuation rules:
        - The "translation" field MUST be a single, well-formed Russian sentence (or paragraph)
          with proper punctuation — commas, periods, dashes, quotation marks, question marks.
          It should read naturally and be ready to display as-is to the user.
        - In the "pairs" array, each chunk should INCLUDE any trailing punctuation that follows
          the words in the source — e.g. {"original": "the cat,", "translation": "кошка,"},
          not {"original": "the cat", "translation": "кошка"} + a separate "," chunk.
        - The final chunk should include the terminating period/question mark.

        Additionally, split BOTH the original and the translation into aligned semantic chunks
        of 1–4 words each. The "pairs" array must have identical order on both sides, so each
        pair[i].original corresponds in meaning to pair[i].translation. Keep chunks short and
        natural — they will be highlighted in parallel when the user hovers over them.

        Respond with ONLY valid JSON, no markdown fences:
        {
          "translation": "перевод на русский (одна строка, цельный текст)",
          "comment": "краткий комментарий если есть важный нюанс, иначе пустая строка",
          "pairs": [
            {"original": "source chunk 1", "translation": "русский фрагмент 1"},
            {"original": "source chunk 2", "translation": "русский фрагмент 2"}
          ]
        }

        Text: "{{text}}"
        """;
}
