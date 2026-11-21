using System.Globalization;
using System.Numerics;

namespace MyExcelMAUIApp
{
    //Помилка синтаксична (з позицією) або помилка обчислення
    public class ExprException : Exception
    {
        public ExprException(string message) : base(message)
        {
        }
    }

    public class Node
    {
        public char Op;
        public BigInteger Value;
        public string Name = "";
        public Node? Left;
        public Node? Right;

        public Node(char op, Node? left = null, Node? right = null)
        {
            Op = op;
            Left = left;
            Right = right;
        }

        public BigInteger Eval(Func<string, BigInteger> resolve)
        {
            if (Op == 'n') return Value;
            if (Op == 'r') return resolve(Name);

            BigInteger a = Left!.Eval(resolve);
            if (Op == 'u') return -a;

            BigInteger b = Right!.Eval(resolve);

            switch (Op)
            {
                case '+':
                    return a + b;
                case '-':
                    return a - b;
                case '*':
                    return a * b;
                case '/':
                    if (b.IsZero) throw new ExprException("Ділення на нуль");
                    return BigInteger.Divide(a, b);
                case 'd':
                    if (b.IsZero) throw new ExprException("Ділення на нуль у div");
                    return BigInteger.Divide(a, b);
                case 'm':
                    if (b.IsZero) throw new ExprException("Ділення на нуль у mod");
                    return BigInteger.Remainder(a, b);
                default: // '^'
                    if (b.Sign < 0) throw new ExprException("Степінь має бути невід'ємним цілим числом");
                    if (b > 10000) throw new ExprException("Показник степеня завеликий (максимум 10000)");
                    return BigInteger.Pow(a, (int)b);
            }
        }
    }

    public class Parser
    {
        private readonly string _text;
        private int _pos;

        private Parser(string text)
        {
            _text = text;
        }

        public static Node Parse(string text)
        {
            Parser p = new Parser(text);
            Node root = p.ParseExpression();

            if (p.Cur() != '\0' || p._pos < text.Length)
            {
                throw p.Error(p._pos + 1, "Очікувався оператор або кінець виразу, знайдено «" + text[p._pos] + "»");
            }

            return root;
        }

        private ExprException Error(int position, string message)
        {
            return new ExprException("Синтаксична помилка (позиція " + position + "): " + message);
        }

        // Пропускає пробіли й повертає поточний символ ('\0' наприкінці рядка).
        private char Cur()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            {
                _pos++;
            }

            return _pos < _text.Length ? _text[_pos] : '\0';
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private static bool IsLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        // Чи починається з поточної позиції окреме слово (mod / div).
        private bool WordAhead(string word)
        {
            int end = _pos + word.Length;

            if (end > _text.Length || _text.Substring(_pos, word.Length).ToLower() != word)
            {
                return false;
            }

            return end == _text.Length || (!IsLetter(_text[end]) && !IsDigit(_text[end]));
        }

        private Node ParseExpression()
        {
            Node left = ParseTerm();

            while (Cur() == '+' || Cur() == '-')
            {
                char op = _text[_pos++];
                left = new Node(op, left, ParseTerm());
            }

            return left;
        }

        private Node ParseTerm()
        {
            Node left = ParseUnary();
            char op = NextTermOperator();

            while (op != ' ')
            {
                left = new Node(op, left, ParseUnary());
                op = NextTermOperator();
            }

            return left;
        }

        // Читає * / mod div і повертає код операції; ' ' якщо наступного оператора немає.
        private char NextTermOperator()
        {
            char c = Cur();

            if (c == '*' || c == '/')
            {
                _pos++;
                return c;
            }

            if (WordAhead("mod"))
            {
                _pos += 3;
                return 'm';
            }

            if (WordAhead("div"))
            {
                _pos += 3;
                return 'd';
            }

            return ' ';
        }

        private Node ParseUnary()
        {
            char c = Cur();

            if (c == '+')
            {
                _pos++;
                return ParseUnary();
            }

            if (c == '-')
            {
                _pos++;
                return new Node('u', ParseUnary());
            }

            return ParsePower();
        }

        private Node ParsePower()
        {
            Node left = ParseAtom();

            if (Cur() == '^')
            {
                _pos++;
                return new Node('^', left, ParseUnary());
            }

            return left;
        }

        private Node ParseAtom()
        {
            char c = Cur();
            int start = _pos;

            if (IsDigit(c))
            {
                while (_pos < _text.Length && IsDigit(_text[_pos])) _pos++;

                if (_pos < _text.Length && char.IsLetter(_text[_pos]))
                {
                    throw Error(start + 1, "Некоректне число або ім'я (літери одразу після цифр)");
                }

                Node number = new Node('n');
                number.Value = BigInteger.Parse(_text.Substring(start, _pos - start), CultureInfo.InvariantCulture);
                return number;
            }

            if (IsLetter(c))
            {
                while (_pos < _text.Length && IsLetter(_text[_pos])) _pos++;
                int lettersEnd = _pos;
                while (_pos < _text.Length && IsDigit(_text[_pos])) _pos++;

                string word = _text.Substring(start, _pos - start);

                if (_pos == lettersEnd)
                {
                    throw Error(start + 1, "Невідомий ідентифікатор «" + word + "» (посилання має вигляд A1, B12)");
                }

                if (_text[lettersEnd] == '0')
                {
                    throw Error(start + 1, "Некоректне посилання на клітинку «" + word + "»");
                }

                Node reference = new Node('r');
                reference.Name = word;
                return reference;
            }

            if (c == '(')
            {
                _pos++;
                Node inner = ParseExpression();

                if (Cur() != ')')
                {
                    string found = _pos >= _text.Length ? "кінець виразу" : "«" + _text[_pos] + "»";
                    throw Error(_pos + 1, "Очікувалась закриваюча дужка «)», знайдено " + found);
                }

                _pos++;
                return inner;
            }

            if (_pos >= _text.Length)
            {
                throw Error(_pos + 1, "Несподіваний кінець виразу");
            }

            throw Error(_pos + 1, "Очікувалось число, посилання або дужка, знайдено «" + c + "»");
        }
    }
}