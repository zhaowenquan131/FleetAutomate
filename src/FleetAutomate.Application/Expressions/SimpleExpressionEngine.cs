using System.Globalization;
using FleetAutomate.Model.Actions.Logic;

namespace FleetAutomate.Expressions;

public sealed class SimpleExpressionEngine : IExpressionEngine
{
    public ExpressionValidationResult Validate(string expressionText, ExpressionContext context)
    {
        try
        {
            var value = Evaluate(expressionText, context);
            return ExpressionValidationResult.Valid(value?.GetType() ?? typeof(object));
        }
        catch (Exception ex)
        {
            return ExpressionValidationResult.Invalid(ex.Message);
        }
    }

    public Task<ExpressionResult> EvaluateAsync(string expressionText, ExpressionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = Evaluate(expressionText, context);
        return Task.FromResult(new ExpressionResult(value, value?.GetType() ?? typeof(object)));
    }

    private static object? Evaluate(string expressionText, ExpressionContext context)
    {
        if (string.IsNullOrWhiteSpace(expressionText))
        {
            throw new InvalidOperationException("Expression cannot be empty.");
        }

        var parser = new Parser(new Lexer(expressionText).Tokenize(), context);
        var value = parser.ParseExpression();
        parser.Expect(TokenKind.End);
        return value;
    }

    private enum TokenKind
    {
        Number,
        String,
        Identifier,
        True,
        False,
        Plus,
        Minus,
        Star,
        Slash,
        Percent,
        EqualEqual,
        BangEqual,
        Greater,
        GreaterEqual,
        Less,
        LessEqual,
        AmpAmp,
        PipePipe,
        Bang,
        LeftParen,
        RightParen,
        Comma,
        Dot,
        End
    }

    private sealed record Token(TokenKind Kind, string Text);

    private sealed class Lexer
    {
        private readonly string _text;
        private int _position;

        public Lexer(string text)
        {
            _text = text;
        }

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            while (_position < _text.Length)
            {
                var ch = _text[_position];
                if (char.IsWhiteSpace(ch))
                {
                    _position++;
                    continue;
                }

                if (char.IsDigit(ch))
                {
                    tokens.Add(ReadNumber());
                    continue;
                }

                if (ch == '"' || ch == '\'')
                {
                    tokens.Add(ReadString(ch));
                    continue;
                }

                if (char.IsLetter(ch) || ch == '_')
                {
                    tokens.Add(ReadIdentifier());
                    continue;
                }

                tokens.Add(ReadOperator());
            }

            tokens.Add(new Token(TokenKind.End, string.Empty));
            return tokens;
        }

        private Token ReadNumber()
        {
            var start = _position;
            while (_position < _text.Length && (char.IsDigit(_text[_position]) || _text[_position] == '.'))
            {
                _position++;
            }

            return new Token(TokenKind.Number, _text[start.._position]);
        }

        private Token ReadString(char quote)
        {
            _position++;
            var start = _position;
            while (_position < _text.Length && _text[_position] != quote)
            {
                _position++;
            }

            if (_position >= _text.Length)
            {
                throw new InvalidOperationException("Unterminated string literal.");
            }

            var value = _text[start.._position];
            _position++;
            return new Token(TokenKind.String, value);
        }

        private Token ReadIdentifier()
        {
            var start = _position;
            while (_position < _text.Length && (char.IsLetterOrDigit(_text[_position]) || _text[_position] == '_'))
            {
                _position++;
            }

            var text = _text[start.._position];
            return text switch
            {
                "true" => new Token(TokenKind.True, text),
                "false" => new Token(TokenKind.False, text),
                _ => new Token(TokenKind.Identifier, text)
            };
        }

        private Token ReadOperator()
        {
            if (TryRead("==", TokenKind.EqualEqual, out var token) ||
                TryRead("!=", TokenKind.BangEqual, out token) ||
                TryRead(">=", TokenKind.GreaterEqual, out token) ||
                TryRead("<=", TokenKind.LessEqual, out token) ||
                TryRead("&&", TokenKind.AmpAmp, out token) ||
                TryRead("||", TokenKind.PipePipe, out token))
            {
                return token;
            }

            var ch = _text[_position++];
            return ch switch
            {
                '+' => new Token(TokenKind.Plus, "+"),
                '-' => new Token(TokenKind.Minus, "-"),
                '*' => new Token(TokenKind.Star, "*"),
                '/' => new Token(TokenKind.Slash, "/"),
                '%' => new Token(TokenKind.Percent, "%"),
                '>' => new Token(TokenKind.Greater, ">"),
                '<' => new Token(TokenKind.Less, "<"),
                '!' => new Token(TokenKind.Bang, "!"),
                '(' => new Token(TokenKind.LeftParen, "("),
                ')' => new Token(TokenKind.RightParen, ")"),
                ',' => new Token(TokenKind.Comma, ","),
                '.' => new Token(TokenKind.Dot, "."),
                _ => throw new InvalidOperationException($"Unexpected character '{ch}'.")
            };
        }

        private bool TryRead(string text, TokenKind kind, out Token token)
        {
            if (_text.AsSpan(_position).StartsWith(text, StringComparison.Ordinal))
            {
                _position += text.Length;
                token = new Token(kind, text);
                return true;
            }

            token = new Token(TokenKind.End, string.Empty);
            return false;
        }
    }

    private sealed class Parser
    {
        private readonly IReadOnlyList<Token> _tokens;
        private readonly ExpressionContext _context;
        private int _position;

        public Parser(IReadOnlyList<Token> tokens, ExpressionContext context)
        {
            _tokens = tokens;
            _context = context;
        }

        public object? ParseExpression() => ParseOr();

        public void Expect(TokenKind kind)
        {
            if (Current.Kind != kind)
            {
                throw new InvalidOperationException($"Expected {kind}, found '{Current.Text}'.");
            }
        }

        private object? ParseOr()
        {
            var left = ParseAnd();
            while (Match(TokenKind.PipePipe))
            {
                left = ToBool(left) || ToBool(ParseAnd());
            }

            return left;
        }

        private object? ParseAnd()
        {
            var left = ParseEquality();
            while (Match(TokenKind.AmpAmp))
            {
                left = ToBool(left) && ToBool(ParseEquality());
            }

            return left;
        }

        private object? ParseEquality()
        {
            var left = ParseComparison();
            while (true)
            {
                if (Match(TokenKind.EqualEqual))
                {
                    left = EqualsNormalized(left, ParseComparison());
                }
                else if (Match(TokenKind.BangEqual))
                {
                    left = !EqualsNormalized(left, ParseComparison());
                }
                else
                {
                    return left;
                }
            }
        }

        private object? ParseComparison()
        {
            var left = ParseTerm();
            while (true)
            {
                if (Match(TokenKind.Greater))
                {
                    left = ToDouble(left) > ToDouble(ParseTerm());
                }
                else if (Match(TokenKind.GreaterEqual))
                {
                    left = ToDouble(left) >= ToDouble(ParseTerm());
                }
                else if (Match(TokenKind.Less))
                {
                    left = ToDouble(left) < ToDouble(ParseTerm());
                }
                else if (Match(TokenKind.LessEqual))
                {
                    left = ToDouble(left) <= ToDouble(ParseTerm());
                }
                else
                {
                    return left;
                }
            }
        }

        private object? ParseTerm()
        {
            var left = ParseFactor();
            while (true)
            {
                if (Match(TokenKind.Plus))
                {
                    var right = ParseFactor();
                    left = left is string || right is string
                        ? string.Concat(left, right)
                        : ToDouble(left) + ToDouble(right);
                }
                else if (Match(TokenKind.Minus))
                {
                    left = ToDouble(left) - ToDouble(ParseFactor());
                }
                else
                {
                    return left;
                }
            }
        }

        private object? ParseFactor()
        {
            var left = ParseUnary();
            while (true)
            {
                if (Match(TokenKind.Star))
                {
                    left = ToDouble(left) * ToDouble(ParseUnary());
                }
                else if (Match(TokenKind.Slash))
                {
                    left = ToDouble(left) / ToDouble(ParseUnary());
                }
                else if (Match(TokenKind.Percent))
                {
                    left = ToDouble(left) % ToDouble(ParseUnary());
                }
                else
                {
                    return left;
                }
            }
        }

        private object? ParseUnary()
        {
            if (Match(TokenKind.Bang))
            {
                return !ToBool(ParseUnary());
            }

            if (Match(TokenKind.Minus))
            {
                return -ToDouble(ParseUnary());
            }

            return ParsePrimary();
        }

        private object? ParsePrimary()
        {
            var value = ParsePrimaryValue();
            while (Match(TokenKind.Dot))
            {
                var method = ConsumeIdentifier();
                Consume(TokenKind.LeftParen);
                var args = new List<object?>();
                if (!Check(TokenKind.RightParen))
                {
                    do
                    {
                        args.Add(ParseExpression());
                    }
                    while (Match(TokenKind.Comma));
                }

                Consume(TokenKind.RightParen);
                value = EvaluateMethod(value, method.Text, args);
            }

            return value;
        }

        private object? ParsePrimaryValue()
        {
            if (Match(TokenKind.Number, out var number))
            {
                return double.Parse(number.Text, CultureInfo.InvariantCulture);
            }

            if (Match(TokenKind.String, out var text))
            {
                return text.Text;
            }

            if (Match(TokenKind.True))
            {
                return true;
            }

            if (Match(TokenKind.False))
            {
                return false;
            }

            if (Match(TokenKind.Identifier, out var identifier))
            {
                if (Match(TokenKind.LeftParen))
                {
                    return EvaluateFunction(identifier.Text);
                }

                var variable = _context.Environment.Variables.FirstOrDefault(v => string.Equals(v.Name, identifier.Text, StringComparison.Ordinal));
                if (variable == null)
                {
                    throw new InvalidOperationException($"Unknown variable '{identifier.Text}'.");
                }

                return variable.Value;
            }

            if (Match(TokenKind.LeftParen))
            {
                var value = ParseExpression();
                Consume(TokenKind.RightParen);
                return value;
            }

            throw new InvalidOperationException($"Unexpected token '{Current.Text}'.");
        }

        private object? EvaluateFunction(string functionName)
        {
            var args = new List<object?>();
            if (!Check(TokenKind.RightParen))
            {
                do
                {
                    args.Add(ParseExpression());
                }
                while (Match(TokenKind.Comma));
            }

            Consume(TokenKind.RightParen);

            return functionName switch
            {
                "now" when args.Count == 0 => DateTimeOffset.Now,
                "today" when args.Count == 0 => DateTimeOffset.Now.Date,
                "uiExists" when args.Count == 1 => !string.IsNullOrWhiteSpace(Convert.ToString(args[0], CultureInfo.InvariantCulture)),
                "uiContainsText" when args.Count == 2 => Convert.ToString(args[0], CultureInfo.InvariantCulture)?.Contains(Convert.ToString(args[1], CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
                "getUiProperty" when args.Count == 2 => $"{args[0]}:{args[1]}",
                "uiCount" when args.Count == 1 => string.IsNullOrWhiteSpace(Convert.ToString(args[0], CultureInfo.InvariantCulture)) ? 0d : 1d,
                "isNowLaterThan" when args.Count == 1 => DateTimeOffset.Now > ParseDateTime(args[0]),
                "isNowEarlierThan" when args.Count == 1 => DateTimeOffset.Now < ParseDateTime(args[0]),
                _ => throw new InvalidOperationException($"Unknown function '{functionName}'.")
            };
        }

        private static object? EvaluateMethod(object? receiver, string methodName, IReadOnlyList<object?> args)
        {
            var text = Convert.ToString(receiver, CultureInfo.InvariantCulture) ?? string.Empty;
            return methodName switch
            {
                "ContainsText" when args.Count == 1 => text.Contains(Convert.ToString(args[0], CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.OrdinalIgnoreCase),
                "StartsWithText" when args.Count == 1 => text.StartsWith(Convert.ToString(args[0], CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.OrdinalIgnoreCase),
                "EndsWithText" when args.Count == 1 => text.EndsWith(Convert.ToString(args[0], CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.OrdinalIgnoreCase),
                _ => throw new InvalidOperationException($"Unknown method '{methodName}'.")
            };
        }

        private Token ConsumeIdentifier()
        {
            if (!Match(TokenKind.Identifier, out var identifier))
            {
                throw new InvalidOperationException($"Expected identifier, found '{Current.Text}'.");
            }

            return identifier;
        }

        private static DateTimeOffset ParseDateTime(object? value)
        {
            return value switch
            {
                DateTimeOffset dto => dto,
                DateTime dt => new DateTimeOffset(dt),
                _ => DateTimeOffset.Parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, CultureInfo.InvariantCulture)
            };
        }

        private Token Current => _tokens[_position];

        private bool Check(TokenKind kind) => Current.Kind == kind;

        private bool Match(TokenKind kind)
        {
            if (!Check(kind))
            {
                return false;
            }

            _position++;
            return true;
        }

        private bool Match(TokenKind kind, out Token token)
        {
            if (Check(kind))
            {
                token = Current;
                _position++;
                return true;
            }

            token = new Token(TokenKind.End, string.Empty);
            return false;
        }

        private void Consume(TokenKind kind)
        {
            if (!Match(kind))
            {
                throw new InvalidOperationException($"Expected {kind}, found '{Current.Text}'.");
            }
        }

        private static double ToDouble(object? value)
        {
            return value switch
            {
                double d => d,
                float f => f,
                decimal m => (double)m,
                int i => i,
                long l => l,
                string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
                _ => Convert.ToDouble(value, CultureInfo.InvariantCulture)
            };
        }

        private static bool ToBool(object? value)
        {
            return value switch
            {
                bool b => b,
                string s when bool.TryParse(s, out var b) => b,
                _ => Convert.ToBoolean(value, CultureInfo.InvariantCulture)
            };
        }

        private static bool EqualsNormalized(object? left, object? right)
        {
            if (IsNumber(left) || IsNumber(right))
            {
                return Math.Abs(ToDouble(left) - ToDouble(right)) < double.Epsilon;
            }

            return Equals(left, right);
        }

        private static bool IsNumber(object? value)
        {
            return value is byte or short or int or long or float or double or decimal;
        }
    }
}
