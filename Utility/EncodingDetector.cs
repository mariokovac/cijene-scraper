using System.Text;

namespace CijeneScraper.Utility
{
    public static class EncodingDetector
    {
        public static string GetText(byte[] bytes, Encoding[] encodings)
        {
            return GetEncoding(bytes, encodings).Item2;
        }

        public static (Encoding, string) GetEncoding(byte[] bytes, Encoding[] encodings)
        {
            foreach (var encoding in encodings)
            {
                try
                {
                    // Provjeri BOM
                    var preamble = encoding.GetPreamble();
                    if (preamble.Length > 0 && bytes.Length >= preamble.Length)
                    {
                        bool hasBom = true;
                        for (int i = 0; i < preamble.Length; i++)
                        {
                            if (bytes[i] != preamble[i])
                            {
                                hasBom = false;
                                break;
                            }
                        }
                        if (hasBom) return (encoding, encoding.GetString(bytes));
                    }

                    // Pokušaj dekodiranje
                    string content = encoding.GetString(bytes);

                    // Provjeri da li je sadržaj valjan
                    if (IsValidText(content))
                    {
                        return (encoding, content);
                    }
                }
                catch
                {
                    continue;
                }
            }

            return (Encoding.UTF8, Encoding.UTF8.GetString(bytes)); // Default to UTF-8 if no encoding matches
        }

        private static bool IsValidText(string text)
        {
            // Provjeri da li nema replacement karaktera
            if (text.Contains('\uFFFD'))
                return false;

            // Provjeri tipične hrvatske znakove
            var croatianChars = new[] { 'č', 'ć', 'đ', 'š', 'ž', 'Č', 'Ć', 'Đ', 'Š', 'Ž' };

            foreach (char c in text)
            {
                if (Array.IndexOf(croatianChars, c) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
