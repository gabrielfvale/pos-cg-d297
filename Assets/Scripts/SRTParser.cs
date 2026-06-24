using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Parseia um arquivo .srt e retorna os intervalos de fala por locutor.
/// Formato esperado no texto da legenda:
///   Speaker 1: Olá, tudo bem?
///   Speaker 2: Tudo ótimo!
/// </summary>
public class SRTParser
{
    [System.Serializable]
    public class SRTEntry
    {
        public int    index;
        public float  startTime;
        public float  endTime;
        public string speaker;
        public string text;
    }

    // Regex para timestamp: 00:01:23,456
    private readonly Regex _timeRegex =
        new Regex(@"(\d{2}):(\d{2}):(\d{2}),(\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2}),(\d{3})");

    // Regex para detectar locutor no início da linha: "Speaker 1:" ou "LOCUTOR 2:" etc.
    // Ajuste o padrão conforme o formato real do seu .srt
    private readonly Regex _speakerRegex =
        new Regex(@"^([^:]{1,30}):\s*(.*)$", RegexOptions.IgnoreCase);

    public List<SRTEntry> Parse(string srtContent, string defaultSpeaker = "Speaker 1")
    {
        var entries = new List<SRTEntry>();
        // Divide em blocos separados por linha em branco
        var blocks = Regex.Split(srtContent.Trim(), @"\r?\n\s*\r?\n");

        foreach (var block in blocks)
        {
            var lines = block.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 3) continue;

            // Linha 0: índice numérico
            if (!int.TryParse(lines[0].Trim(), out int idx)) continue;

            // Linha 1: timestamps
            var tm = _timeRegex.Match(lines[1]);
            if (!tm.Success) continue;

            float start = ParseTime(tm, 1);
            float end   = ParseTime(tm, 5);

            // Linhas 2+: texto (pode ser multiline)
            string fullText = string.Join(" ", lines, 2, lines.Length - 2).Trim();
            string speaker  = defaultSpeaker;
            string text     = fullText;

            // Tenta extrair locutor do texto
            var sm = _speakerRegex.Match(fullText);
            if (sm.Success)
            {
                speaker = sm.Groups[1].Value.Trim();
                text    = sm.Groups[2].Value.Trim();
            }

            entries.Add(new SRTEntry
            {
                index     = idx,
                startTime = start,
                endTime   = end,
                speaker   = speaker,
                text      = text
            });
        }

        return entries;
    }

    private float ParseTime(Match m, int groupOffset)
    {
        float h   = float.Parse(m.Groups[groupOffset    ].Value);
        float min = float.Parse(m.Groups[groupOffset + 1].Value);
        float sec = float.Parse(m.Groups[groupOffset + 2].Value);
        float ms  = float.Parse(m.Groups[groupOffset + 3].Value);
        return h * 3600f + min * 60f + sec + ms / 1000f;
    }
}