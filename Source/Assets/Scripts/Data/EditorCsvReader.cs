#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SeaVillage.Data
{
    internal readonly struct CsvRecord
    {
        public CsvRecord(int lineNumber, string[] values)
        {
            LineNumber = lineNumber;
            Values = values;
        }

        public int LineNumber { get; }
        public string[] Values { get; }
    }

    internal static class EditorCsvReader
    {
        public static List<CsvRecord> ReadAll(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("CSV 경로가 비어 있습니다", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("CSV 파일을 찾을 수 없습니다", path);

            return Parse(File.ReadAllText(path));
        }

        public static List<CsvRecord> Parse(string text)
        {
            var records = new List<CsvRecord>();
            if (string.IsNullOrEmpty(text))
                return records;

            var fields = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            int lineNumber = 1;
            int recordStartLine = 1;

            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];

                if (current == '"')
                {
                    if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (current == ',' && !inQuotes)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    continue;
                }

                if (current == '\r' || current == '\n')
                {
                    bool isCrLf = current == '\r' && i + 1 < text.Length && text[i + 1] == '\n';
                    if (inQuotes)
                    {
                        field.Append('\n');
                    }
                    else
                    {
                        fields.Add(field.ToString());
                        field.Clear();
                        AddRecordIfNotEmpty(records, recordStartLine, fields);
                        fields.Clear();
                        recordStartLine = lineNumber + 1;
                    }

                    if (isCrLf)
                        i++;
                    lineNumber++;
                    continue;
                }

                field.Append(current);
            }

            if (inQuotes)
                throw new FormatException($"행 {recordStartLine}: 닫히지 않은 큰따옴표가 있습니다");

            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                AddRecordIfNotEmpty(records, recordStartLine, fields);
            }

            if (records.Count > 0 && records[0].Values.Length > 0)
                records[0].Values[0] = records[0].Values[0].TrimStart('\uFEFF');

            return records;
        }

        private static void AddRecordIfNotEmpty(List<CsvRecord> records, int lineNumber, List<string> fields)
        {
            bool hasValue = false;
            for (int i = 0; i < fields.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(fields[i]))
                {
                    hasValue = true;
                    break;
                }
            }

            if (hasValue)
                records.Add(new CsvRecord(lineNumber, fields.ToArray()));
        }
    }
}
#endif
