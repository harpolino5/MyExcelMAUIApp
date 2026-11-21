using System.Numerics;

namespace MyExcelMAUIApp
{
    //Два етапи: 1) синтаксичний аналіз із локалізацією помилки; 2) обчислення значення
    public static class Calculator
    {
        public static CellResult Evaluate(string expression, Func<string, BigInteger> resolveReference)
        {
            CellResult result = new CellResult();

            try
            {
                Node tree = Parser.Parse(expression);                 // 1 етап
                result.Value = tree.Eval(resolveReference);           // 2 етап
            }
            catch (ExprException ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }
    }
}