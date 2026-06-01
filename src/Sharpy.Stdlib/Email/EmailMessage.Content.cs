using System;
using System.Text;

namespace Sharpy
{
    public sealed partial class EmailMessage
    {
        public void SetContent(string text, string subtype = "plain")
        {
            _body = text;
            _contentType = "text/" + subtype;
            SetItem("Content-Type", "text/" + subtype + "; charset=\"utf-8\"");
        }

        public string GetContent()
        {
            return _body ?? "";
        }

        public string? GetPayload()
        {
            return _body;
        }

        public bool IsMultipart() => _isMultipart;

        public void AddAttachment(Bytes data, string maintype = "application", string subtype = "octet-stream", string? filename = null)
        {
            if (!_isMultipart)
            {
                _isMultipart = true;
                _boundary = "===============" + Math.Abs(_headers.Count.GetHashCode()).ToString(System.Globalization.CultureInfo.InvariantCulture) + "==";
                SetItem("Content-Type", "multipart/mixed; boundary=\"" + _boundary + "\"");
            }

            string contentType = maintype + "/" + subtype;
            _attachments.Add(new Attachment(data, contentType, filename));
        }

        public List<Attachment> IterAttachments()
        {
            var result = new List<Attachment>();
            for (int i = 0; i < _attachments.Count; i++)
            {
                result.Append(_attachments[i]);
            }
            return result;
        }

        public string AsString()
        {
            var sb = new StringBuilder();

            foreach (var (name, value) in _headers)
            {
                sb.Append(name);
                sb.Append(": ");
                sb.Append(value);
                sb.Append("\r\n");
            }

            if (!_isMultipart)
            {
                sb.Append("\r\n");
                if (_body != null)
                {
                    sb.Append(_body);
                }
            }
            else
            {
                sb.Append("\r\n");
                // Text body part
                if (_body != null)
                {
                    sb.Append("--");
                    sb.Append(_boundary);
                    sb.Append("\r\n");
                    sb.Append("Content-Type: ");
                    sb.Append(_contentType);
                    sb.Append("; charset=\"utf-8\"\r\n");
                    sb.Append("\r\n");
                    sb.Append(_body);
                    sb.Append("\r\n");
                }

                // Attachment parts
                foreach (var att in _attachments)
                {
                    sb.Append("--");
                    sb.Append(_boundary);
                    sb.Append("\r\n");
                    sb.Append("Content-Type: ");
                    sb.Append(att.ContentType);
                    if (att.Filename != null)
                    {
                        sb.Append("; name=\"");
                        sb.Append(att.Filename);
                        sb.Append("\"");
                    }
                    sb.Append("\r\n");
                    sb.Append("Content-Transfer-Encoding: base64\r\n");
                    if (att.Filename != null)
                    {
                        sb.Append("Content-Disposition: attachment; filename=\"");
                        sb.Append(att.Filename);
                        sb.Append("\"\r\n");
                    }
                    sb.Append("\r\n");
                    sb.Append(Convert.ToBase64String(att.Data.ToArray()));
                    sb.Append("\r\n");
                }

                sb.Append("--");
                sb.Append(_boundary);
                sb.Append("--\r\n");
            }

            return sb.ToString();
        }

        public Bytes AsBytes()
        {
            return new Bytes(Encoding.UTF8.GetBytes(AsString()));
        }

        public override string ToString() => AsString();
    }
}
