using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace WebUI.Util
{
    public static class StringExtensions
    {
        /// <summary>
        /// Returns the string encoded with <see cref="HttpUtility.HtmlEncode(string)"/>
        /// </summary>
        public static string AsHtmlEncoded(this string s)
        {
            return HttpUtility.HtmlEncode(s);
        }

        /// <summary>
        /// Returns the string encoded with <see cref="HttpUtility.JavaScriptStringEncode(string)"/>
        /// </summary>
        public static string AsJavaScriptStringEncoded(this string s)
        {
            return HttpUtility.JavaScriptStringEncode(s);
        }

        public static string GetSymbolHash(this string docId)
        {
            string result = GetMD5Hash(docId, 16);
            return result;
        }

        public static string GetMD5Hash(string input, int digits)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(bytes);
                return ByteArrayToHexString(hashBytes, digits);
            }
        }

        public static string ByteArrayToHexString(byte[] bytes, int digits = 0)
        {
            if (digits == 0)
            {
                digits = bytes.Length * 2;
            }

            char[] c = new char[digits];
            byte b;
            for (int i = 0; i < digits / 2; i++)
            {
                b = ((byte)(bytes[i] >> 4));
                c[i * 2] = (char)(b > 9 ? b + 87 : b + 0x30);
                b = ((byte)(bytes[i] & 0xF));
                c[i * 2 + 1] = (char)(b > 9 ? b + 87 : b + 0x30);
            }

            return new string(c);
        }
    }
}