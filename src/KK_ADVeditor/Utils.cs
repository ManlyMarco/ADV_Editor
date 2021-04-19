using System.Text;

namespace KK_ADVeditor
{
    public static class Utils
    {
        public static string Escape(this string txt)
        {
            if (string.IsNullOrEmpty(txt))
                return string.Empty;
            StringBuilder stringBuilder = new StringBuilder(txt.Length + 2);
            foreach (char ch in txt)
            {
                switch (ch)
                {
                    case char.MinValue:
                        stringBuilder.Append("\\0");
                        break;
                    case '\a':
                        stringBuilder.Append("\\a");
                        break;
                    case '\b':
                        stringBuilder.Append("\\b");
                        break;
                    case '\t':
                        stringBuilder.Append("\\t");
                        break;
                    case '\n':
                        stringBuilder.Append("\\n");
                        break;
                    case '\v':
                        stringBuilder.Append("\\v");
                        break;
                    case '\f':
                        stringBuilder.Append("\\f");
                        break;
                    case '\r':
                        stringBuilder.Append("\\r");
                        break;
                    case '"':
                        stringBuilder.Append("\\\"");
                        break;
                    case '\'':
                        stringBuilder.Append("\\'");
                        break;
                    case '\\':
                        stringBuilder.Append("\\");
                        break;
                    default:
                        stringBuilder.Append(ch);
                        break;
                }
            }
            return stringBuilder.ToString();
        }

        public static string Unescape(this string txt)
        {
            if (string.IsNullOrEmpty(txt))
                return txt;
            StringBuilder stringBuilder = new StringBuilder(txt.Length);
            int num;
            for (int startIndex = 0; startIndex < txt.Length; startIndex = num + 2)
            {
                num = txt.IndexOf('\\', startIndex);
                if (num < 0 || num == txt.Length - 1)
                    num = txt.Length;
                stringBuilder.Append(txt, startIndex, num - startIndex);
                if (num < txt.Length)
                {
                    char ch = txt[num + 1];
                    switch (ch)
                    {
                        case '"':
                            stringBuilder.Append('"');
                            break;
                        case '\'':
                            stringBuilder.Append('\'');
                            break;
                        case '0':
                            stringBuilder.Append(char.MinValue);
                            break;
                        case '\\':
                            stringBuilder.Append('\\');
                            break;
                        case 'a':
                            stringBuilder.Append('\a');
                            break;
                        case 'b':
                            stringBuilder.Append('\b');
                            break;
                        case 'f':
                            stringBuilder.Append('\f');
                            break;
                        case 'n':
                            stringBuilder.Append('\n');
                            break;
                        case 'r':
                            stringBuilder.Append('\r');
                            break;
                        case 't':
                            stringBuilder.Append('\t');
                            break;
                        case 'v':
                            stringBuilder.Append('\v');
                            break;
                        default:
                            stringBuilder.Append('\\').Append(ch);
                            break;
                    }
                }
                else
                    break;
            }
            return stringBuilder.ToString();
        }
    }
}