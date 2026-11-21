namespace MyExcelMAUIApp
{
    //Формує текст звіту про помилки обчислення для показу користувачу
    public static class ErrorReport
    {
        private const int MaxShown = 15;

        public static string Title(List<string> errors)
        {
            if (errors.Count == 0)
            {
                return "Обчислення";
            }

            return "Знайдено помилок: " + errors.Count;
        }

        public static string Body(List<string> errors)
        {
            if (errors.Count == 0)
            {
                return "Помилок не виявлено.";
            }

            int shown = Math.Min(MaxShown, errors.Count);
            string text = string.Join("\n", errors.GetRange(0, shown));

            if (errors.Count > shown)
            {
                text += "\n... та ще " + (errors.Count - shown);
            }

            return text;
        }
    }
}