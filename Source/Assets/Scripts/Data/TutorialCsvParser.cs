#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SeaVillage.Core;

namespace SeaVillage.Data
{
    internal static class TutorialCsvParser
    {
        private const int FirstTutorialNumber = 1;
        private const int LastTutorialNumber = 30;

        private static readonly Regex TutorialIdPattern = new Regex(
            "^Tutorial_[0-9]{3}$",
            RegexOptions.CultureInvariant);

        public static bool TryParse(
            string path,
            out List<TutorialData> tutorials,
            out List<string> errors)
        {
            tutorials = new List<TutorialData>();
            errors = new List<string>();

            List<CsvRecord> records;
            try
            {
                records = EditorCsvReader.ReadAll(path);
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
                return false;
            }

            if (records.Count == 0)
            {
                errors.Add("CSV 데이터가 비어 있습니다");
                return false;
            }

            if (!TryResolveHeaders(records[0], out int idIndex, out int typeIndex, out int scriptIndex, errors))
                return false;

            var sequenceById = new Dictionary<string, int>(StringComparer.Ordinal);
            var completedIds = new HashSet<string>(StringComparer.Ordinal);
            string previousId = string.Empty;

            for (int i = 1; i < records.Count; i++)
            {
                CsvRecord record = records[i];
                string[] values = record.Values;
                int requiredIndex = Math.Max(idIndex, Math.Max(typeIndex, scriptIndex));
                if (values.Length <= requiredIndex)
                {
                    errors.Add($"행 {record.LineNumber}: 필수 열이 누락되었습니다");
                    continue;
                }

                string id = values[idIndex].Trim();
                string typeText = values[typeIndex].Trim();
                string script = NormalizeScript(values[scriptIndex]);

                bool rowValid = true;
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add($"행 {record.LineNumber}: ID가 비어 있습니다");
                    rowValid = false;
                }
                else if (!IsSupportedDialogueId(id))
                {
                    errors.Add(
                        $"행 {record.LineNumber}: ID '{id}'는 Tutorial_000 형식 또는 등록된 수동 튜토리얼 ID여야 합니다");
                    rowValid = false;
                }

                if (!TryParseDialogueType(typeText, out TutorialDialogueType dialogueType))
                {
                    errors.Add($"행 {record.LineNumber}: Type '{typeText}'는 Stop, Auto, Box 중 하나여야 합니다");
                    rowValid = false;
                }

                if (string.IsNullOrWhiteSpace(script))
                {
                    errors.Add($"행 {record.LineNumber}: Script가 비어 있습니다");
                    rowValid = false;
                }

                if (!string.IsNullOrEmpty(id) && !string.Equals(previousId, id, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(previousId))
                        completedIds.Add(previousId);

                    if (completedIds.Contains(id))
                    {
                        errors.Add($"행 {record.LineNumber}: ID '{id}'가 비연속 구간에서 다시 등장합니다");
                        rowValid = false;
                    }

                    previousId = id;
                }

                if (!rowValid)
                    continue;

                sequenceById.TryGetValue(id, out int sequence);
                tutorials.Add(new TutorialData
                {
                    ID = id,
                    Type = dialogueType,
                    Script = script,
                    Sequence = sequence
                });
                sequenceById[id] = sequence + 1;
            }

            if (tutorials.Count == 0 && errors.Count == 0)
                errors.Add("변환 가능한 튜토리얼 데이터가 없습니다");

            if (errors.Count == 0)
                ValidateRuntimeContracts(tutorials, errors);

            return errors.Count == 0;
        }

        private static bool TryResolveHeaders(
            CsvRecord headerRecord,
            out int idIndex,
            out int typeIndex,
            out int scriptIndex,
            List<string> errors)
        {
            idIndex = FindHeader(headerRecord.Values, "ID");
            typeIndex = FindHeader(headerRecord.Values, "Type");
            scriptIndex = FindHeader(headerRecord.Values, "Script");

            if (idIndex < 0)
                errors.Add($"행 {headerRecord.LineNumber}: ID 헤더가 없습니다");
            if (typeIndex < 0)
                errors.Add($"행 {headerRecord.LineNumber}: Type 헤더가 없습니다");
            if (scriptIndex < 0)
                errors.Add($"행 {headerRecord.LineNumber}: Script 헤더가 없습니다");

            return errors.Count == 0;
        }

        private static int FindHeader(string[] headers, string expected)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (string.Equals(headers[i].Trim(), expected, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static bool TryParseDialogueType(string value, out TutorialDialogueType dialogueType)
        {
            dialogueType = default;
            string[] names = Enum.GetNames(typeof(TutorialDialogueType));
            for (int i = 0; i < names.Length; i++)
            {
                if (!string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase))
                    continue;

                return Enum.TryParse(names[i], out dialogueType);
            }

            return false;
        }

        private static bool IsSupportedDialogueId(string id)
        {
            return TutorialIdPattern.IsMatch(id)
                   || string.Equals(
                       id,
                       TutorialDefinitionCatalog.FirstWreckDialogueId,
                       StringComparison.Ordinal);
        }

        private static void ValidateRuntimeContracts(
            IReadOnlyList<TutorialData> tutorials,
            List<string> errors)
        {
            TutorialRepository repository = TutorialDefinitionCatalog.CreateRepository(tutorials);
            if (!repository.TryValidate(out string failReason))
            {
                errors.Add($"통합 튜토리얼 런타임 계약 검증 실패: {failReason}");
            }
        }

        private static string NormalizeScript(string script)
        {
            if (string.IsNullOrEmpty(script))
                return string.Empty;

            return script
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n");
        }
    }
}
#endif
