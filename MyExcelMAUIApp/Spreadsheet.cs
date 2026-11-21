using System.Numerics;
using System.Text.Json;

namespace MyExcelMAUIApp
{
    public class CellResult
    {
        public bool IsEmpty;
        public bool Success = true;
        public BigInteger Value;
        public string Error = "";
    }

    //Модель таблиці: вирази клітинок, обчислення з посиланнями, цикли, розміри, файл JSON
    public class Spreadsheet
    {
        public List<List<string>> Cells = new List<List<string>>();

        private readonly Dictionary<(int, int), CellResult> _results = new Dictionary<(int, int), CellResult>();
        private readonly HashSet<(int, int)> _visiting = new HashSet<(int, int)>();

        public Spreadsheet(int rows, int columns)
        {
            for (int r = 0; r < rows; r++)
            {
                AddRow(columns);
            }
        }

        public int Rows
        {
            get { return Cells.Count; }
        }

        public int Columns
        {
            get { return Cells[0].Count; }
        }

        //Назви

        public static string ColumnName(int index)
        {
            string name = "";

            do
            {
                name = (char)('A' + index % 26) + name;
                index = index / 26 - 1;
            }
            while (index >= 0);

            return name;
        }

        public static string CellName(int row, int col)
        {
            return ColumnName(col) + (row + 1);
        }

        //Редагування

        public string GetExpression(int row, int col)
        {
            return Cells[row][col];
        }

        public void SetExpression(int row, int col, string expression)
        {
            Cells[row][col] = expression;
            _results.Clear();
        }

        public void AddRow()
        {
            AddRow(Cells.Count == 0 ? 1 : Columns);
        }

        private void AddRow(int columns)
        {
            List<string> row = new List<string>();

            for (int c = 0; c < columns; c++)
            {
                row.Add("");
            }

            Cells.Add(row);
            _results.Clear();
        }

        public bool RemoveLastRow()
        {
            if (Rows <= 1) return false;

            Cells.RemoveAt(Rows - 1);
            _results.Clear();
            return true;
        }

        public void AddColumn()
        {
            foreach (List<string> row in Cells)
            {
                row.Add("");
            }

            _results.Clear();
        }

        public bool RemoveLastColumn()
        {
            if (Columns <= 1) return false;

            foreach (List<string> row in Cells)
            {
                row.RemoveAt(row.Count - 1);
            }

            _results.Clear();
            return true;
        }

        //Обчислення

        public void Recalculate()
        {
            _results.Clear();

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    Evaluate(r, c);
                }
            }
        }

        public CellResult GetResult(int row, int col)
        {
            return Evaluate(row, col);
        }

        //Рядки виду «B2: текст помилки» для всіх клітинок із помилками
        public List<string> GetErrors()
        {
            List<string> errors = new List<string>();

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    CellResult result = Evaluate(r, c);

                    if (!result.Success)
                    {
                        errors.Add(CellName(r, c) + ": " + result.Error);
                    }
                }
            }

            return errors;
        }

        private CellResult Evaluate(int row, int col)
        {
            if (_results.TryGetValue((row, col), out CellResult? cached))
            {
                return cached;
            }

            string text = Cells[row][col].Trim();

            if (text.StartsWith("="))
            {
                text = text.Substring(1).Trim();
            }

            CellResult result = new CellResult();

            if (text == "")
            {
                result.IsEmpty = true;
            }
            else if (!_visiting.Add((row, col)))
            {
                result.Success = false;
                result.Error = "Циклічне посилання";
                return result;
            }
            else
            {
                try
                {
                    result = Calculator.Evaluate(text, ResolveReference);
                }
                finally
                {
                    _visiting.Remove((row, col));
                }
            }

            _results[(row, col)] = result;
            return result;
        }

        // Посилання вже перевірене парсером: літери, потім цифри (без нуля на початку).
        private BigInteger ResolveReference(string reference)
        {
            string name = reference.ToUpper();
            int i = 0;
            long col = 0;

            while (i < name.Length && name[i] >= 'A' && name[i] <= 'Z')
            {
                col = col * 26 + (name[i] - 'A' + 1);
                i++;

                if (col > 100000)
                {
                    throw new ExprException("Посилання " + name + " виходить за межі таблиці");
                }
            }

            col--;

            if (!int.TryParse(name.Substring(i), out int rowNumber) || rowNumber > Rows || col >= Columns)
            {
                throw new ExprException("Посилання " + name + " виходить за межі таблиці");
            }

            CellResult result = Evaluate(rowNumber - 1, (int)col);

            if (!result.Success)
            {
                throw new ExprException("Клітинка " + name + " містить помилку: " + result.Error);
            }

            return result.IsEmpty ? BigInteger.Zero : result.Value;
        }

        //Файл (JSON)

        public void Save(string path)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(Cells));
        }

        public static Spreadsheet Load(string path)
        {
            List<List<string>>? data = JsonSerializer.Deserialize<List<List<string>>>(File.ReadAllText(path));

            if (data == null || data.Count == 0)
            {
                throw new InvalidDataException("Файл не містить даних таблиці.");
            }

            int columns = 1;

            foreach (List<string> row in data)
            {
                if (row != null && row.Count > columns) columns = row.Count;
            }

            Spreadsheet sheet = new Spreadsheet(data.Count, columns);

            for (int r = 0; r < data.Count; r++)
            {
                for (int c = 0; data[r] != null && c < data[r].Count; c++)
                {
                    sheet.Cells[r][c] = data[r][c] ?? "";
                }
            }

            return sheet;
        }
    }
}