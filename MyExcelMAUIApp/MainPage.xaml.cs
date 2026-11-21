namespace MyExcelMAUIApp
{
    /// <summary>Лише інтерфейс: обробники подій делегують роботу моделі (Spreadsheet).</summary>
    public partial class MainPage : ContentPage
    {
        private Spreadsheet _sheet = new Spreadsheet(20, 10);
        private bool _showValues;
        private bool _isRefreshing;

        public MainPage()
        {
            InitializeComponent();
            RebuildGrid();
        }

        // ---------- Обробники подій ----------

        private async void SaveButton_Clicked(object? sender, EventArgs e)
        {
            await SaveTableAsync();
        }

        private async void ReadButton_Clicked(object? sender, EventArgs e)
        {
            await OpenTableAsync();
        }

        private async void CalculateButton_Clicked(object? sender, EventArgs e)
        {
            await CalculateAsync();
        }

        private async void ExitButton_Clicked(object? sender, EventArgs e)
        {
            if (await DisplayAlert("Підтвердження", "Ви дійсно хочете вийти?", "Так", "Ні"))
            {
                Application.Current?.Quit();
            }
        }

        private async void HelpButton_Clicked(object? sender, EventArgs e)
        {
            await DisplayAlert("Довідка", HelpText, "OK");
        }

        private async void AboutButton_Clicked(object? sender, EventArgs e)
        {
            await DisplayAlert("Про програму", AboutText, "OK");
        }

        private void AddRowButton_Clicked(object? sender, EventArgs e)
        {
            _sheet.AddRow();
            RebuildGrid();
        }

        private void DeleteRowButton_Clicked(object? sender, EventArgs e)
        {
            if (_sheet.RemoveLastRow()) RebuildGrid();
        }

        private void AddColumnButton_Clicked(object? sender, EventArgs e)
        {
            _sheet.AddColumn();
            RebuildGrid();
        }

        private void DeleteColumnButton_Clicked(object? sender, EventArgs e)
        {
            if (_sheet.RemoveLastColumn()) RebuildGrid();
        }

        private void ModeSwitch_Toggled(object? sender, ToggledEventArgs e)
        {
            _showValues = e.Value;
            RefreshDisplay();
        }

        // Фокус на клітинці: показуємо вираз і, якщо є, причину помилки.
        private void Entry_Focused(object? sender, FocusEventArgs e)
        {
            if (_isRefreshing || sender is not Entry entry) return;

            int row = Grid.GetRow(entry) - 1;
            int col = Grid.GetColumn(entry) - 1;
            if (row >= _sheet.Rows || col >= _sheet.Columns) return;

            _isRefreshing = true;
            entry.Text = _sheet.GetExpression(row, col);
            entry.ClearValue(Entry.TextColorProperty);
            _isRefreshing = false;

            CellResult result = _sheet.GetResult(row, col);
            statusLabel.Text = result.Success ? "" : Spreadsheet.CellName(row, col) + ": " + result.Error;
        }

        // Втрата фокусу: зберігаємо вираз і перераховуємо таблицю.
        private void Entry_Unfocused(object? sender, FocusEventArgs e)
        {
            if (_isRefreshing || sender is not Entry entry) return;

            int row = Grid.GetRow(entry) - 1;
            int col = Grid.GetColumn(entry) - 1;
            if (row >= _sheet.Rows || col >= _sheet.Columns) return;

            _sheet.SetExpression(row, col, entry.Text ?? "");
            _sheet.Recalculate();
            RefreshDisplay();
        }

        // ---------- Дії ----------

        private async Task SaveTableAsync()
        {
            string? name = await DisplayPromptAsync("Збереження", "Введіть ім'я файлу (без розширення):",
                "Зберегти", "Скасувати", initialValue: "table");

            if (string.IsNullOrWhiteSpace(name)) return;

            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(bad, '_');
            }

            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), name + ".json");

            try
            {
                _sheet.Save(path);
                await DisplayAlert("Збережено", "Таблицю збережено у файл:\n" + path, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Помилка", "Не вдалося зберегти файл: " + ex.Message, "OK");
            }
        }

        private async Task OpenTableAsync()
        {
            var jsonType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".json" } },
                { DevicePlatform.MacCatalyst, new[] { "public.json" } },
                { DevicePlatform.iOS, new[] { "public.json" } },
                { DevicePlatform.Android, new[] { "application/json" } }
            });

            try
            {
                FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Оберіть файл таблиці",
                    FileTypes = jsonType
                });

                if (file == null) return;

                _sheet = Spreadsheet.Load(file.FullPath);
                statusLabel.Text = "";
                RebuildGrid();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Помилка", "Не вдалося прочитати файл: " + ex.Message, "OK");
            }
        }

        private async Task CalculateAsync()
        {
            _sheet.Recalculate();
            modeSwitch.IsToggled = true;
            RefreshDisplay();
            statusLabel.Text = "";

            List<string> errors = _sheet.GetErrors();

            await DisplayAlert(ErrorReport.Title(errors), ErrorReport.Body(errors), "OK");
        }

        // ---------- Побудова та відображення таблиці ----------

        private void RebuildGrid()
        {
            _isRefreshing = true;

            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();

            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(40)));

            for (int c = 0; c < _sheet.Columns; c++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(110)));
                AddHeader(Spreadsheet.ColumnName(c), 0, c + 1);
            }

            for (int r = 0; r < _sheet.Rows; r++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                AddHeader((r + 1).ToString(), r + 1, 0);

                for (int c = 0; c < _sheet.Columns; c++)
                {
                    Entry entry = new Entry { VerticalOptions = LayoutOptions.Center };
                    entry.Focused += Entry_Focused;
                    entry.Unfocused += Entry_Unfocused;
                    Grid.SetRow(entry, r + 1);
                    Grid.SetColumn(entry, c + 1);
                    grid.Children.Add(entry);
                }
            }

            _isRefreshing = false;
            RefreshDisplay();
        }

        private void AddHeader(string text, int row, int col)
        {
            Label label = new Label
            {
                Text = text,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };

            Grid.SetRow(label, row);
            Grid.SetColumn(label, col);
            grid.Children.Add(label);
        }

        // Показує в усіх клітинках вираз або значення залежно від режиму.
        private void RefreshDisplay()
        {
            _isRefreshing = true;

            foreach (IView child in grid.Children)
            {
                if (child is not Entry entry) continue;

                int row = Grid.GetRow(entry) - 1;
                int col = Grid.GetColumn(entry) - 1;
                entry.ClearValue(Entry.TextColorProperty);

                if (!_showValues)
                {
                    entry.Text = _sheet.GetExpression(row, col);
                    continue;
                }

                CellResult result = _sheet.GetResult(row, col);

                if (result.IsEmpty)
                {
                    entry.Text = "";
                }
                else if (result.Success)
                {
                    entry.Text = result.Value.ToString();
                }
                else
                {
                    entry.Text = "#ПОМИЛКА";
                    entry.TextColor = Colors.Red;
                }
            }

            _isRefreshing = false;
        }

        // ---------- Тексти довідки ----------

        private const string HelpText =
            "Клітинки містять вирази з цілих чисел довільної довжини, круглих дужок та посилань на клітинки (A1, B12, AA3).\n\n" +
            "Бінарні операції: +  -  *  /  mod  div  ^ (степінь)\n" +
            "Унарні операції: + та - (наприклад -5 або -(A1 + 2))\n" +
            "Пріоритет: ^, унарні + -, * / mod div, бінарні + -.\n" +
            "Можна починати вираз із «=».\n\n" +
            "Режими: ВИРАЗ показує введене, ЗНАЧЕННЯ — результат (помилки: #ПОМИЛКА). " +
            "Клацніть на клітинку з помилкою, щоб побачити причину.\n\n" +
            "«Порахувати» перевіряє синтаксис усіх виразів, обчислює значення та показує список помилок. " +
            "Файли зберігаються у форматі JSON у теці «Документи».";

        private const string AboutText =
            "Лабораторна робота №1. Електронна таблиця варіант 1.\n" +
            "Студент: Акімова Юлія, 2 курс, група К-14";
    }
}