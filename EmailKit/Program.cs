using MimeKit;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace EmailKit
{
    class Program
    {
        static void Main(string[] args)
        {
            DirectoryInfo di = new DirectoryInfo(Directory.GetCurrentDirectory());

            string workingFolder = Path.Combine(di.FullName, "working");
            if (!Directory.Exists(workingFolder))
            {
                Directory.CreateDirectory(workingFolder);
            }

            if (args.Length > 0)
            {
                foreach (string item in args)
                {
                    FileInfo fi = null;

                    if (File.Exists(item))
                    {
                        fi = new FileInfo(item);
                        workingFolder = ProcessMessage(workingFolder, fi);
                    }
                    else
                    {
                        string filename = Path.Combine(di.FullName, item);
                        if (File.Exists(filename))
                        {
                            fi = new FileInfo(filename);
                            workingFolder = ProcessMessage(workingFolder, fi);
                        }
                    }
                }
            }
            else
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                foreach (FileInfo fi in di.GetFiles("*.eml"))
                {
                    workingFolder = ProcessMessage(workingFolder, fi);
                }
            }

        }

        private static string ProcessMessage(string workingFolder, FileInfo fi)
        {
            MimeMessage message = MimeMessage.Load(fi.FullName);
            if (message != null)
            {
                string datestamp = message.Date.ToString("yyyyMMdd-HHmmss");
                workingFolder = Path.Combine(workingFolder, datestamp);
                if (!Directory.Exists(workingFolder))
                {
                    Directory.CreateDirectory(workingFolder);
                }

                Console.WriteLine($"Θέμα: {message.Subject}");

                foreach (MimeEntity bodypart in message.BodyParts)
                {
                    switch (bodypart)
                    {
                        case TextPart m:
                            switch (m.ContentType.MimeType.ToLower())
                            {
                                case "text/plain":
                                    StringBuilder sb = new StringBuilder();
                                    sb.AppendLine($"Date       : {message.Date.ToString("dddd, dd MMMM yyyy @ HH:hh")}");
                                    sb.AppendLine($"From       : {message.From.Mailboxes.FirstOrDefault().Name} <{message.From.Mailboxes.FirstOrDefault().Address}>");
                                    if (message.To.Mailboxes.Count() > 0)
                                    {
                                        sb.AppendLine($"To         : {message.To.Mailboxes.FirstOrDefault().Name} <{message.To.Mailboxes.FirstOrDefault().Address}>");
                                    }
                                    sb.AppendLine($"Subject    : {message.Subject}");
                                    if (message.Attachments.Count() > 0)
                                    {
                                        sb.Append("Attachments: ");
                                        foreach (MimeEntity attachment in message.Attachments)
                                        {
                                            MimePart file = attachment as MimePart;
                                            if (file != null)
                                            {
                                                sb.Append($"'{file.FileName}', ");
                                            }
                                        }
                                        sb.AppendLine();
                                    }
                                    sb.AppendLine();
                                    sb.Append(m.Text);
                                    workingFolder = Path.Combine(fi.Directory.FullName, "working", datestamp, "message.txt");
                                    File.WriteAllText(workingFolder, sb.ToString());
                                    break;

                                case "text/html":
                                    workingFolder = Path.Combine(fi.Directory.FullName, "working", datestamp, "message.html");
                                    File.WriteAllText(workingFolder, m.Text);
                                    break;

                                default:
                                    Console.WriteLine(m.ContentType.MimeType);
                                    break;
                            }
                            break;

                        case MessageDispositionNotification m:
                            switch (m.ContentType.MimeType)
                            {
                                case "message/disposition-notification":
                                    workingFolder = Path.Combine(fi.Directory.FullName, "working", datestamp, "disposition-notification.txt");
                                    using (Stream file = File.Create(workingFolder))
                                    {
                                        m.WriteTo(file);
                                    }
                                    break;

                                default:
                                    Console.WriteLine(m.ContentType.MimeType);
                                    break;
                            }
                            break;

                        case MimePart m:
                            switch (m.ContentType.MimeType.ToLower())
                            {
                                case "message/delivery-status":
                                    workingFolder = Path.Combine(fi.Directory.FullName, "working", datestamp, "delivery-status.txt");
                                    using (Stream file = File.Create(workingFolder))
                                    {
                                        m.Content.DecodeTo(file);
                                    }
                                    break;

                                default:
                                    string filename = m.FileName
                                        .Replace("cid:", "")
                                        .Replace("?", "")
                                        .Replace(":", "")
                                        .Replace("  ", " ")
                                        .Replace("..", ".")
                                        .Replace(" .", ".");

                                    filename = string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
                                    filename = filename.Length > 64 ? filename.Substring(0, 64) : filename;

                                    workingFolder = Path.Combine(fi.Directory.FullName, "working", datestamp, filename);
                                    using (Stream file = File.Create(workingFolder))
                                    {
                                        m.Content.DecodeTo(file);
                                    }
                                    string hash = MD5Hash(workingFolder);
                                    Console.WriteLine($"{filename} - MD5: {hash}");

                                    break;
                            }
                            break;

                        case MessagePart m:
                            switch (m.ContentType.MimeType.ToLower())
                            {
                                case "message/rfc822":
                                    workingFolder = Path.Combine(fi.Directory.FullName, "working", datestamp, "message.eml");
                                    using (Stream file = File.Create(workingFolder))
                                    {
                                        m.WriteTo(file);
                                    }
                                    break;

                                case "text/rfc822-headers":
                                    workingFolder = Path.Combine(fi.Directory.FullName, "working", datestamp, "headers.txt");
                                    using (Stream file = File.Create(workingFolder))
                                    {
                                        m.WriteTo(file);
                                    }
                                    break;

                                default:
                                    Console.WriteLine(m.ContentType.MimeType);
                                    break;
                            }
                            break;

                        default:
                            Console.WriteLine(bodypart.GetType().Name);
                            break;
                    }
                }

            }

            return workingFolder;
        }

        static string MD5Hash(string filename)
        {
            using (var md5 = MD5.Create())
            {
                var result = md5.ComputeHash(File.ReadAllBytes(filename));
                return string.Join("", result.Select(x => x.ToString("X2")));
            }
        }
    }
}
