using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaVillage.Utilities
{
    public sealed class ExpressionEvaluator
    {
        private enum StringTokenType
        {
            Identifier, Number, 
            And, Or, 
            Equal, NEqual, 
            Greater, Less, 
            GEqual, LEqual,
            LParen, RParen
        }

        /// <summary>
        /// 평가를 위한 최소 단위 문자열 토큰
        /// </summary>
        private readonly struct StringToken
        {
            public readonly StringTokenType Type;
            public readonly string Text;
            public StringToken(StringTokenType type, string text)
            {
                Type = type;
                Text = text;
            }
        }

        public bool Evaluate(string expression, Func<string, int> resolver)
        {
            // 공백일 경우 표현식은 참으로 간주
            if (string.IsNullOrWhiteSpace(expression)) return true;

            var tokens = Tokenize(expression);
            int index = 0;
            return ParseOr(tokens, ref index, resolver);
        }
        
        /// <summary>
        /// 여러 줄에 걸친 조건식을 모두 평가한다. 줄바꿈으로 구분하며, 모든 조건 만족 시 true
        /// </summary>
        public bool EvaluateAll(string multilineExpression, Func<string, int> resolver)
        {
            if (string.IsNullOrWhiteSpace(multilineExpression)) return true;

            var lines = multilineExpression.Split(new[] {"\r\n", "\n"}, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!Evaluate(line, resolver)) return false;
            }
            return true;
        }

        /// <summary>
        /// 확률 문자열을 평가한다. 예: "5%" -> 5% 확률로 true 반환
        /// 형식 오류나 파싱 실패 시 false 반환
        /// </summary>
        public bool EvaluateProbability(string probabilityExpression)
        {
            if (string.IsNullOrWhiteSpace(probabilityExpression))
            {
                Debug.LogWarning("[ExpressionEvaluator] 확률 표현식이 비어있습니다.");
                return false;
            }

            string trimmed = probabilityExpression.Trim();
            if (!trimmed.EndsWith("%", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[ExpressionEvaluator] 확률 표현식 형식 오류: '{probabilityExpression}'");
                return false;
            }

            string numStr = trimmed.Substring(0, trimmed.Length - 1).Trim();
            
            if (!float.TryParse(numStr, out float probability))
            {
                Debug.LogWarning($"[ExpressionEvaluator] 확률 파싱 실패: '{numStr}'");
                return false;
            }

            // 범위 검증
            if (probability < 0 || probability > 100)
            {
                Debug.LogWarning($"[ExpressionEvaluator] 확률이 범위를 벗어남: {probability}% (허용 범위: 0-100)");
                return false;
            }

            float roll = UnityEngine.Random.Range(0f, 100f);
            return roll < probability;
        }

        public bool TryEvaluateNumber(string expression, Func<string, float> resolver, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(expression)) return false;

            int index = 0;
            try
            {
                value = ParseNumericExpr(expression, ref index, resolver);
            }
            catch (FormatException)
            {
                return false;
            }

            SkipWhitespace(expression, ref index);
            return index >= expression.Length;
        }

        private static float ParseNumericExpr(string expr, ref int index, Func<string, float> resolver)
        {
            float value = ParseNumericTerm(expr, ref index, resolver);
            while (true)
            {
                SkipWhitespace(expr, ref index);
                if (index < expr.Length && expr[index] == '+')
                {
                    index++;
                    value += ParseNumericTerm(expr, ref index, resolver);
                }
                else if (index < expr.Length && expr[index] == '-')
                {
                    index++;
                    value -= ParseNumericTerm(expr, ref index, resolver);
                }
                else
                {
                    return value;
                }
            }
        }

        private static float ParseNumericTerm(string expr, ref int index, Func<string, float> resolver)
        {
            float value = ParseNumericUnary(expr, ref index, resolver);
            while (true)
            {
                SkipWhitespace(expr, ref index);
                if (index < expr.Length && expr[index] == '*')
                {
                    index++;
                    value *= ParseNumericUnary(expr, ref index, resolver);
                }
                else if (index < expr.Length && expr[index] == '/')
                {
                    index++;
                    value /= ParseNumericUnary(expr, ref index, resolver);
                }
                else
                {
                    return value;
                }
            }
        }

        private static float ParseNumericUnary(string expr, ref int index, Func<string, float> resolver)
        {
            SkipWhitespace(expr, ref index);
            if (index < expr.Length && expr[index] == '-')
            {
                index++;
                return -ParseNumericUnary(expr, ref index, resolver);
            }
            return ParseNumericPrimary(expr, ref index, resolver);
        }

        private static float ParseNumericPrimary(string expr, ref int index, Func<string, float> resolver)
        {
            SkipWhitespace(expr, ref index);
            if (index >= expr.Length) throw new FormatException();

            char c = expr[index];

            if (c == '(')
            {
                index++;
                float inner = ParseNumericExpr(expr, ref index, resolver);
                Expect(expr, ref index, ')');
                return inner;
            }

            if (char.IsDigit(c) || c == '.')
            {
                int start = index;
                while (index < expr.Length && (char.IsDigit(expr[index]) || expr[index] == '.')) index++;
                string num = expr.Substring(start, index - start);
                if (!float.TryParse(num, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                    throw new FormatException();
                return parsed;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = index;
                while (index < expr.Length && (char.IsLetterOrDigit(expr[index]) || expr[index] == '_')) index++;
                string name = expr.Substring(start, index - start);

                SkipWhitespace(expr, ref index);
                if (index < expr.Length && expr[index] == '(')
                {
                    index++;
                    var args = new List<float> { ParseNumericExpr(expr, ref index, resolver) };
                    SkipWhitespace(expr, ref index);
                    while (index < expr.Length && expr[index] == ',')
                    {
                        index++;
                        args.Add(ParseNumericExpr(expr, ref index, resolver));
                        SkipWhitespace(expr, ref index);
                    }
                    Expect(expr, ref index, ')');
                    return EvaluateNumericFunction(name, args);
                }

                return resolver != null ? resolver(name) : 0f;
            }

            throw new FormatException();
        }

        private static float EvaluateNumericFunction(string name, List<float> args)
        {
            if (args.Count == 0) throw new FormatException();

            switch (name)
            {
                case "Min":
                {
                    float min = args[0];
                    for (int i = 1; i < args.Count; i++) min = Mathf.Min(min, args[i]);
                    return min;
                }
                case "Max":
                {
                    float max = args[0];
                    for (int i = 1; i < args.Count; i++) max = Mathf.Max(max, args[i]);
                    return max;
                }
                default:
                    throw new FormatException();
            }
        }

        private static void Expect(string expr, ref int index, char expected)
        {
            SkipWhitespace(expr, ref index);
            if (index >= expr.Length || expr[index] != expected) throw new FormatException();
            index++;
        }

        private static void SkipWhitespace(string expr, ref int index)
        {
            while (index < expr.Length && char.IsWhiteSpace(expr[index])) index++;
        }

        private bool ParseOr(List<StringToken> tokens, ref int index, Func<string, int> resolver)
        {
            bool value = ParseAnd(tokens, ref index, resolver);
            while (Match(tokens, ref index, StringTokenType.Or))
            {
                bool right = ParseAnd(tokens, ref index, resolver);
                value = value || right;
            }
            return value;
        }

        private bool ParseAnd(List<StringToken> tokens, ref int index, Func<string, int> resolver)
        {
            bool value = ParseComparison(tokens, ref index, resolver);
            while (Match(tokens, ref index, StringTokenType.And))
            {
                bool right = ParseComparison(tokens, ref index, resolver);
                value = value && right;
            }
            return value;
        }

        private bool ParseComparison(List<StringToken> tokens, ref int index, Func<string, int> resolver)
        {
            int left = ParsePrimary(tokens, ref index, resolver);

            if (index >= tokens.Count) return left != 0;

            var tok = tokens[index];
            switch (tok.Type)
            {
                case StringTokenType.Equal:
                    index++;
                    return left == ParsePrimary(tokens, ref index, resolver);
                case StringTokenType.NEqual:
                    index++;
                    return left != ParsePrimary(tokens, ref index, resolver);
                case StringTokenType.Greater:
                    index++;
                    return left > ParsePrimary(tokens, ref index, resolver);
                case StringTokenType.GEqual:
                    index++;
                    return left >= ParsePrimary(tokens, ref index, resolver);
                case StringTokenType.Less:
                    index++;
                    return left < ParsePrimary(tokens, ref index, resolver);
                case StringTokenType.LEqual:
                    index++;
                    return left <= ParsePrimary(tokens, ref index, resolver);
                default:
                    return left != 0;
            }
        }

        private int ParsePrimary(List<StringToken> tokens, ref int index, Func<string, int> resolver)
        {
            if (index >= tokens.Count) return 0;
            var tok = tokens[index];
            switch (tok.Type)
            {
                case StringTokenType.Number:
                    index++;
                    return int.TryParse(tok.Text, out var num) ? num : 0;
                case StringTokenType.Identifier:
                    index++;
                    return resolver != null ? resolver(tok.Text) : 0;
                case StringTokenType.LParen:
                    index++;
                    bool value = ParseOr(tokens, ref index, resolver);
                    if (index < tokens.Count && tokens[index].Type == StringTokenType.RParen) index++;
                    return value ? 1 : 0;
                default:
                    index++;
                    return 0;
            }
        }

        private bool Match(List<StringToken> tokens, ref int index, StringTokenType type)
        {
            if (index < tokens.Count && tokens[index].Type == type)
            {
                index++;
                return true;
            }
            return false;
        }

        /// <summary>
        /// string 표현식을 토큰화
        /// </summary>
        private List<StringToken> Tokenize(string expr)
        {
            var tokens = new List<StringToken>();
            int i = 0;
            while (i < expr.Length)
            {
                char c = expr[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                // 문자 토큰 추가, '_' 가능 (e.g. Player_Gold)
                if (char.IsLetter(c) || c == '_')
                {
                    // start부터 문자(숫자를 포함한 문자도 가능)로 식별되는 index까지 하나의 토큰으로 추가
                    int start = i;
                    while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_')) i++;
                    tokens.Add(new StringToken(StringTokenType.Identifier, expr.Substring(start, i - start)));
                    continue;
                }

                // 정수 토큰 추가
                if (char.IsDigit(c))
                {
                    int start = i;
                    while (i < expr.Length && char.IsDigit(expr[i])) i++;
                    tokens.Add(new StringToken(StringTokenType.Number, expr.Substring(start, i - start)));
                    continue;
                }

                // 두 글자 연산자 토큰 추가(먼저 처리)
                if (i + 1 < expr.Length)
                {
                    string two = expr.Substring(i, 2);
                    switch (two)
                    {
                        case "&&": tokens.Add(new StringToken(StringTokenType.And, two)); i += 2; continue;
                        case "||": tokens.Add(new StringToken(StringTokenType.Or, two)); i += 2; continue;
                        case "==": tokens.Add(new StringToken(StringTokenType.Equal, two)); i += 2; continue;
                        case "!=": tokens.Add(new StringToken(StringTokenType.NEqual, two)); i += 2; continue;
                        case ">=": tokens.Add(new StringToken(StringTokenType.GEqual, two)); i += 2; continue;
                        case "<=": tokens.Add(new StringToken(StringTokenType.LEqual, two)); i += 2; continue;
                    }
                }

                // 한 글자 연산자(괄호) 토큰 추가
                switch (c)
                {
                    case '>': tokens.Add(new StringToken(StringTokenType.Greater, ">")); i++; break;
                    case '<': tokens.Add(new StringToken(StringTokenType.Less, "<")); i++; break;
                    case '(': tokens.Add(new StringToken(StringTokenType.LParen, "(")); i++; break;
                    case ')': tokens.Add(new StringToken(StringTokenType.RParen, ")")); i++; break;
                    default:
                        Debug.LogWarning($"Unexpected token in expression: {c}");
                        i++;
                        break;
                }
            }
            return tokens;
        }
    }
}
