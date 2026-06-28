using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace AzureAISearchTutorialCompanion.Services;

internal class PdfChunkerService
{
    private const int MaxChunkChars = 1000;

    public List<(string Section, string Content)> ChunkPdf(byte[] pdfBytes)
    {
        var rawText  = ExtractText(pdfBytes);
        var sections = SplitIntoSections(rawText);
        var result   = new List<(string, string)>();

        foreach (var (sectionName, sectionText) in sections)
        {
            foreach (var chunk in CreateChunks(sectionText))
            {
                if (!IsTocContent(chunk))
                {
                    var section = sectionName != "Document"
                        ? sectionName
                        : DeriveSection(chunk);
                    result.Add((section, chunk));
                }
            }
        }

        return result;
    }

    // ── Text extraction ───────────────────────────────────────────────────────

    private static string ExtractText(byte[] pdfBytes)
    {
        using var doc = PdfDocument.Open(pdfBytes);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    // ── Section splitting (structural headings + TOC removal) ─────────────────

    private static List<(string Name, string Text)> SplitIntoSections(string fullText)
    {
        var sections       = new List<(string Name, string Text)>();
        var currentSection = "Document";
        var currentText    = new StringBuilder();

        foreach (var rawLine in fullText.Split('\n'))
        {
            var line = rawLine.Trim();

            if (IsHeading(line))
            {
                FlushSection(sections, currentSection, currentText);
                currentSection = line;
                currentText.Clear();
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                currentText.Append(line).Append('\n');
            }
        }

        FlushSection(sections, currentSection, currentText);

        var filtered = sections.Where(s => !IsTocSection(s.Name, s.Text)).ToList();
        return filtered.Count > 0
            ? filtered
            : new List<(string, string)> { ("Document", fullText) };
    }

    private static void FlushSection(List<(string, string)> sections, string name, StringBuilder text)
    {
        var content = text.ToString().Trim();
        if (content.Length > 0)
            sections.Add((name, content));
    }

    private static bool IsHeading(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > 120)
            return false;

        if (Regex.IsMatch(line, @"^\d+(\.\d+)*\.?\s+\w"))
            return true;

        if (line.Length is >= 3 and <= 80 && line == line.ToUpperInvariant() && line.Any(char.IsLetter))
            return true;

        if (Regex.IsMatch(line, @"^(Chapter|Section|Part|Appendix|Introduction|Conclusion|Summary|Overview|Abstract|Background|References)\b",
                RegexOptions.IgnoreCase))
            return true;

        return false;
    }

    private static bool IsTocSection(string name, string text)
    {
        if (Regex.IsMatch(name.Trim(), @"^(table\s+of\s+contents?|contents?|toc)$", RegexOptions.IgnoreCase))
            return true;

        var lines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        if (lines.Length < 2) return false;

        int tocLike = lines.Count(IsTocLine);
        return (double)tocLike / lines.Length > 0.4;
    }

    private static bool IsTocLine(string line)
    {
        if (line.Length > 200) return false;
        return
            Regex.IsMatch(line, @"(\. ){3,}") ||       // ". . . . ."  PdfPig spaced dots
            Regex.IsMatch(line, @"\.{3,}") ||           // "......" consecutive dots
            Regex.IsMatch(line, @"\s{3,}\d{1,4}\s*$"); // large gap + page number
    }

    private static bool IsTocContent(string chunk)
    {
        int dotPairs = Regex.Matches(chunk, @"\. ").Count;
        return dotPairs > 3 && (double)(dotPairs * 2) / chunk.Length > 0.15;
    }

    // ── Paragraph-aware chunking ──────────────────────────────────────────────
    //
    // Strategy:
    //   1. Split section text into paragraphs (lines separated by newlines).
    //   2. Accumulate whole paragraphs until the chunk would exceed MaxChunkChars.
    //   3. If a single paragraph is itself too long, split it at sentence boundaries.
    //
    // Result: every chunk ends at a paragraph or sentence boundary — never mid-word.

    private static IEnumerable<string> CreateChunks(string text)
    {
        var paragraphs = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        var current = new StringBuilder();

        foreach (var para in paragraphs)
        {
            // Paragraph is itself too long — split at sentence boundaries
            if (para.Length > MaxChunkChars)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString().Trim();
                    current.Clear();
                }
                foreach (var chunk in SplitLongParagraph(para))
                    yield return chunk;
                continue;
            }

            // Adding this paragraph would exceed the limit — flush first
            if (current.Length > 0 && current.Length + 1 + para.Length > MaxChunkChars)
            {
                yield return current.ToString().Trim();
                current.Clear();
            }

            if (current.Length > 0) current.Append(' ');
            current.Append(para);
        }

        if (current.Length > 0)
            yield return current.ToString().Trim();
    }

    private static IEnumerable<string> SplitLongParagraph(string para)
    {
        var sentences = SplitIntoSentences(para);
        var current   = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (sentence.Length > MaxChunkChars)
            {
                if (current.Length > 0) { yield return current.ToString().Trim(); current.Clear(); }
                yield return TruncateAtWord(sentence, MaxChunkChars);
                continue;
            }

            if (current.Length > 0 && current.Length + 1 + sentence.Length > MaxChunkChars)
            {
                yield return current.ToString().Trim();
                current.Clear();
            }

            if (current.Length > 0) current.Append(' ');
            current.Append(sentence);
        }

        if (current.Length > 0)
            yield return current.ToString().Trim();
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var normalised = Regex.Replace(text, @"\s{2,}", " ").Trim();
        var parts      = Regex.Split(normalised, @"(?<=[.!?])\s+(?=[A-Z""'\(])");
        return parts.Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }

    private static string TruncateAtWord(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        var cut       = text[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');
        return lastSpace > maxLength / 2 ? cut[..lastSpace] : cut;
    }

    // ── Section title derived from chunk content ──────────────────────────────

    private static string DeriveSection(string chunkText)
    {
        var text = chunkText.Trim();

        var match = Regex.Match(text, @"^(.{10,100}?[.!?])(?:\s|$)");
        if (match.Success)
            return match.Groups[1].Value.Trim();

        if (text.Length <= 100) return text;
        var truncated = text[..100];
        var lastSpace = truncated.LastIndexOf(' ');
        return (lastSpace > 20 ? truncated[..lastSpace] : truncated) + "…";
    }
}
