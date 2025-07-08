namespace CijeneScraper.Utility
{
    public static class StringHelpers
    {
        public static string TrimToMaxLength(this string? input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || maxLength <= 0)
                return string.Empty;

            if (input.Length <= maxLength)
                return input;

            return input.Substring(0, maxLength);
        }

        public static string NormalizeBarcode(this string? barcode, string productCode)
        {
            return string.IsNullOrEmpty(barcode) ? "_" + productCode : barcode;
        }
    }
}
