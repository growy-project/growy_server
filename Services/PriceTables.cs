namespace growy_server.Services
{
    internal static class PriceTables
    {
        // "CEDEAR" has its own table with no exchange column filter; every other
        // exchange shares symbol_date_price and filters by @Exchange.
        public static (string TableName, bool IsCedear, string ExchangeFilter) Resolve(string exchange)
        {
            bool isCedear = exchange == "CEDEAR";
            string tableName = isCedear ? "symbol_date_price_cedears" : "symbol_date_price";
            string exchangeFilter = isCedear ? "" : "AND exchange = @Exchange";
            return (tableName, isCedear, exchangeFilter);
        }
    }
}
