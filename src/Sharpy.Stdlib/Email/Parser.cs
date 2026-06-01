using System;
using System.Text;

namespace Sharpy
{
    internal static class EmailParser
    {
        public static EmailMessage ParseString(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new EmailMessage();
            }

            var msg = new EmailMessage();

            // Find header/body boundary (first blank line)
            int bodyStart;
            string headersPart;
            string bodyPart;

            int crlfBlank = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            int lfBlank = text.IndexOf("\n\n", StringComparison.Ordinal);

            if (crlfBlank >= 0 && (lfBlank < 0 || crlfBlank <= lfBlank))
            {
                headersPart = text.Substring(0, crlfBlank);
                bodyStart = crlfBlank + 4;
            }
            else if (lfBlank >= 0)
            {
                headersPart = text.Substring(0, lfBlank);
                bodyStart = lfBlank + 2;
            }
            else
            {
                // No blank line — could be all headers or all body
                if (text.Contains(":"))
                {
                    headersPart = text;
                    bodyStart = text.Length;
                }
                else
                {
                    headersPart = "";
                    bodyStart = 0;
                }
            }

            bodyPart = bodyStart < text.Length ? text.Substring(bodyStart) : "";

            // Parse headers
            if (headersPart.Length > 0)
            {
                string[] lines = headersPart.Replace("\r\n", "\n").Split('\n');
                string? currentName = null;
                string? currentValue = null;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.Length == 0)
                        continue;

                    if (line[0] == ' ' || line[0] == '\t')
                    {
                        // Continuation line
                        if (currentName != null)
                        {
                            currentValue = currentValue + " " + line.TrimStart();
                        }
                    }
                    else
                    {
                        // Flush previous header
                        if (currentName != null)
                        {
                            msg.AddHeader(currentName, currentValue ?? "");
                        }

                        int colonIdx = line.IndexOf(':');
                        if (colonIdx > 0)
                        {
                            currentName = line.Substring(0, colonIdx);
                            currentValue = line.Substring(colonIdx + 1).TrimStart();
                        }
                        else
                        {
                            currentName = null;
                            currentValue = null;
                        }
                    }
                }

                // Flush last header
                if (currentName != null)
                {
                    msg.AddHeader(currentName, currentValue ?? "");
                }
            }

            // Set body
            if (bodyPart.Length > 0)
            {
                string? contentType = msg.GetItem("Content-Type");
                string subtype = "plain";
                if (contentType != null && contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                {
                    int semiIdx = contentType.IndexOf(';');
                    string mediaType = semiIdx > 0 ? contentType.Substring(0, semiIdx).Trim() : contentType.Trim();
                    if (mediaType.Length > 5)
                    {
                        subtype = mediaType.Substring(5);
                    }
                }
                msg.SetContent(bodyPart, subtype);
            }

            return msg;
        }

        public static EmailMessage ParseBytes(Bytes data)
        {
            string text = Encoding.UTF8.GetString(data.ToArray());
            return ParseString(text);
        }
    }
}
