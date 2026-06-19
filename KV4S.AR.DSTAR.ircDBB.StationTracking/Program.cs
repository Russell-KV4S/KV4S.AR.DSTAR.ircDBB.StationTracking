using System.Configuration;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using Telegram.Bot;

namespace KV4S.AmateurRadio.DSTAR.IRCDBB.StationTracking;

internal class Program
{
    private const string Url = "https://irc-1.openquad.net/ics/ics.txt";
    private const int SleepTimeMilliseconds = 2000;
    private static readonly HttpClient HttpClient = new();

    private static readonly int MinutesUntilNotify = Convert.ToInt32(ConfigurationManager.AppSettings["MinutesUntilNextNotification"]);
    private static readonly TelegramBotClient Bot = new(ConfigurationManager.AppSettings["BotToken"]);
    private static readonly string DestinationId = ConfigurationManager.AppSettings["DestinationID"];
    private static readonly MailAddress From = new(ConfigurationManager.AppSettings["EmailFrom"]);
    private static readonly string ToConfig = ConfigurationManager.AppSettings["EmailTo"];
    private static readonly string SmtpHost = ConfigurationManager.AppSettings["SMTPHost"];
    private static readonly string SmtpPort = ConfigurationManager.AppSettings["SMTPPort"];
    private static readonly string SmtpUser = ConfigurationManager.AppSettings["SMTPUser"];
    private static readonly string SmtpPassword = ConfigurationManager.AppSettings["SMTPPassword"];

    private static List<string> _callsignList = [];
    private static List<string> _emailAddressList = [];

    private static string CallsignListString
    {
        set => _callsignList = [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries)];
    }

    private static string EmailAddressListString
    {
        set => _emailAddressList = [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries)];
    }

    private static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("Welcome to the D-STAR Station Tracker Application by KV4S!");
            Console.WriteLine(" ");
            Console.WriteLine("Beginning download from " + Url);
            Console.WriteLine("Please Stand by.....");
            Console.WriteLine(" ");

            CallsignListString = ConfigurationManager.AppSettings["Callsigns"].ToUpperInvariant();
            var trackingLines = await DownloadTrackingLinesAsync();

            foreach (var callsign in _callsignList)
            {
                Console.WriteLine("Checking station " + callsign);

                var formattedCallsign = callsign.PadRight(8, '_') + '/';
                var logLine = string.Empty;
                var transmissionTime = DateTime.Now;
                var reflector = string.Empty;
                var target = string.Empty;

                foreach (var line in trackingLines)
                {
                    if (!line.Contains(formattedCallsign, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parts = line.Split(' ');
                    transmissionTime = DateTime.ParseExact($"{parts[0]} {parts[1]}", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    reflector = parts[^1];
                    var callsignLocation = line.IndexOf(formattedCallsign, StringComparison.Ordinal);
                    target = line.Substring(callsignLocation + 14, 8);
                    logLine = $"{transmissionTime:yyyy-MM-dd HH:mm:ss}~{callsign}~{target}~{reflector}";
                }

                if (reflector != "________" && logLine.Length > 0)
                {
                    var logFilePath = GetStationLogPath(callsign);
                    if (File.Exists(logFilePath))
                    {
                        var updated = false;
                        using var streamReader = File.OpenText(logFilePath);
                        string? previousLogLine;

                        while ((previousLogLine = streamReader.ReadLine()) is not null)
                        {
                            if (logLine != previousLogLine)
                            {
                                var storedLogParts = previousLogLine.Split('~');
                                var previousTransmissionTime = DateTime.ParseExact(storedLogParts[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                                var elapsed = transmissionTime - previousTransmissionTime;
                                if (elapsed.TotalMinutes > MinutesUntilNotify)
                                {
                                    Console.WriteLine(logLine);
                                    updated = true;
                                    if (ConfigurationManager.AppSettings["StatusEmails"] == "Y")
                                    {
                                        Email(callsign, target, reflector);
                                    }

                                    if (ConfigurationManager.AppSettings["TelegramStatus"] == "Y")
                                    {
                                        await Bot.SendTextMessageAsync(DestinationId, "DSTAR.StationTracking - Station " +
                                            callsign + ", with a Target of " + target + ", has transmitted on " + reflector);
                                        await Task.Delay(SleepTimeMilliseconds);
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Station " + callsign + " has not transmitted in the last " + MinutesUntilNotify + " minutes.");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Station " + callsign + " has not transmitted in the last " + MinutesUntilNotify + " minutes.");
                            }
                        }

                        if (updated)
                        {
                            File.WriteAllText(logFilePath, logLine + Environment.NewLine);
                        }
                    }
                    else
                    {
                        File.WriteAllText(logFilePath, logLine + Environment.NewLine);
                        Console.WriteLine("Station " + callsign + " is now being tracked on the DSTAR website. Current Target: " + target + " Current Reflector: " + reflector);
                        if (ConfigurationManager.AppSettings["StatusEmails"] == "Y")
                        {
                            Email(callsign, target, reflector);
                        }

                        if (ConfigurationManager.AppSettings["TelegramStatus"] == "Y")
                        {
                            await Bot.SendTextMessageAsync(DestinationId, "DSTAR.StationTracking - Station " +
                                callsign + ", with a Target of " + target + ", has transmitted on " + reflector);
                            await Task.Delay(SleepTimeMilliseconds);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Station " + callsign + " has not transmitted in the last " + MinutesUntilNotify + " minutes.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Program encountered and error:");
            Console.WriteLine(ex.Message);
            LogError(ex.Message, ex.Source ?? "Unknown");
            if (ConfigurationManager.AppSettings["EmailError"] == "Y")
            {
                EmailError(ex.Message, ex.Source ?? "Unknown");
            }

            if (ConfigurationManager.AppSettings["TelegramError"] == "Y")
            {
                await Bot.SendTextMessageAsync(DestinationId, "DSTAR.StationTracking Error - Message: " + ex.Message + " Source: " + ex.Source);
                await Task.Delay(SleepTimeMilliseconds);
            }
        }
        finally
        {
            if (ConfigurationManager.AppSettings["Unattended"] == "N")
            {
                Console.WriteLine("Press any key on your keyboard to quit...");
                Console.ReadKey();
            }
        }
    }

    private static async Task<List<string>> DownloadTrackingLinesAsync()
    {
        ServicePointManager.Expect100Continue = true;
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        using var stream = await HttpClient.GetStreamAsync(Url);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static string GetStationLogPath(string callsign) => Path.Combine(AppContext.BaseDirectory, callsign + ".txt");

    private static void EmailError(string message, string source)
    {
        try
        {
            using var mail = new MailMessage
            {
                Subject = "DSTAR.StationTracking Error",
                From = From,
                Body = "Message: " + message + " Source: " + source
            };

            EmailAddressListString = ToConfig;
            foreach (var emailAddress in _emailAddressList)
            {
                mail.To.Add(emailAddress);
            }

            using var smtp = new SmtpClient
            {
                Host = SmtpHost,
                Port = Convert.ToInt32(SmtpPort),
                Credentials = new NetworkCredential(SmtpUser, SmtpPassword),
                EnableSsl = true
            };

            smtp.Send(mail);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Program encountered and an error sending email:");
            Console.WriteLine(ex.Message);
            LogError(ex.Message, ex.Source ?? "Unknown");
        }
    }

    private static void Email(string callSign, string target, string status)
    {
        try
        {
            using var mail = new MailMessage
            {
                Subject = "DSTAR.StationTracking",
                From = From,
                Body = "Station " + callSign + ", with a Target of " + target + ", has transmitted on " + status
            };

            EmailAddressListString = ToConfig;
            foreach (var emailAddress in _emailAddressList)
            {
                mail.To.Add(emailAddress);
            }

            using var smtp = new SmtpClient
            {
                Host = SmtpHost,
                Port = Convert.ToInt32(SmtpPort),
                Credentials = new NetworkCredential(SmtpUser, SmtpPassword),
                EnableSsl = true
            };

            smtp.Send(mail);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error sending email:");
            Console.WriteLine(ex.Message);
            LogError(ex.Message, ex.Source ?? "Unknown");
        }
    }

    private static void LogError(string message, string source)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "ErrorLog.txt"),
                DateTime.Now + " Error: " + message + " Source: " + source + Environment.NewLine);
        }
        catch (Exception)
        {
            Console.WriteLine("Error logging previous error.");
            Console.WriteLine("Make sure the Error log is not open.");
        }
    }
}
