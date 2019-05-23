using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace KV4S.AmateurRadio.DSTAR.IRCDBB.StationTracking
{
    class Program
    {
        public static string URL = "https://irc-1.openquad.net/ics/ics.txt";

        //load from App.config
        public static MailAddress from = new MailAddress(ConfigurationManager.AppSettings["EmailFrom"]);
        public static string toConfig = ConfigurationManager.AppSettings["EmailTo"];
        public static string smtpHost = ConfigurationManager.AppSettings["SMTPHost"];
        public static string smtpPort = ConfigurationManager.AppSettings["SMTPPort"];
        public static string smtpUser = ConfigurationManager.AppSettings["SMTPUser"];
        public static string smtpPswrd = ConfigurationManager.AppSettings["SMTPPassword"];
        public static int intMinutesUntilNotify = Convert.ToInt32(ConfigurationManager.AppSettings["MinutesUntilNextNotification"]);

        private static List<string> _callsignList = null;
        private static string CallsignListString
        {
            set
            {
                string[] callsignArray = value.Split(',');
                _callsignList = new List<string>(callsignArray.Length);
                _callsignList.AddRange(callsignArray);
            }
        }

        private static List<string> _emailAddressList = null;
        private static string EmailAddressListString
        {
            set
            {
                string[] emailAddressArray = value.Split(',');
                _emailAddressList = new List<string>(emailAddressArray.Length);
                _emailAddressList.AddRange(emailAddressArray);
            }
        }



        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Welcome to the D-STAR Station Tracker Application by KV4S!");
                Console.WriteLine(" ");
                Console.WriteLine("Beginning download from " + URL);
                Console.WriteLine("Please Stand by.....");
                Console.WriteLine(" ");



                CallsignListString = ConfigurationManager.AppSettings["Callsigns"].ToUpper();
                foreach (string callsign in _callsignList)
                {
                    Console.WriteLine("Checking station " + callsign);

                    //need to eliminate similar type callsigns and get exact using 8 characters callsign positions in the UR field.
                    string strFormattedCallsign = callsign.PadRight(callsign.Count() + (8 - callsign.Count()), '_') + '/';

                    string LogLine = "";
                    DateTime dt = DateTime.Now;
                    string strReflector = "";

                    var client = new WebClient();
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    using (var stream = client.OpenRead(URL))
                    using (var reader = new StreamReader(stream))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Contains(strFormattedCallsign))
                            {
                                string[] strSpaces = line.Split(' ');
                                dt = DateTime.ParseExact(strSpaces[0] + " " + strSpaces[1], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                                strReflector = strSpaces[strSpaces.Count() - 1];
                                LogLine = dt.ToString("yyyy-MM-dd HH:mm:ss") + "~" + callsign + "~" + strReflector;
                            }
                        }
                        if (strReflector != "________" && LogLine != "")
                        {
                            if (File.Exists(callsign + ".txt"))
                            {
                                bool updated = false;
                                using (StreamReader sr = File.OpenText(callsign + ".txt"))
                                {
                                    String s = "";

                                    while ((s = sr.ReadLine()) != null)
                                    {
                                        if (LogLine != s)
                                        {
                                            string[] strSep = s.Split('~');
                                            DateTime dtLogTime = DateTime.ParseExact(strSep[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                                            TimeSpan ts = dt - dtLogTime;
                                            if (ts.TotalMinutes > intMinutesUntilNotify)
                                            {
                                                Console.WriteLine(LogLine);
                                                updated = true;
                                                if (ConfigurationManager.AppSettings["StatusEmails"] == "Y")
                                                {
                                                    Email(callsign, strReflector);
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine("Station " + callsign + " has not transmitted in the last " + intMinutesUntilNotify + " minutes.");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Station " + callsign + " has not transmitted in the last " + intMinutesUntilNotify + " minutes.");
                                        }
                                    }
                                }
                                if (updated)
                                {
                                    File.Delete(callsign + ".txt");
                                    FileStream fs = null;
                                    fs = new FileStream(callsign + ".txt", FileMode.Append);
                                    StreamWriter log = new StreamWriter(fs);
                                    log.WriteLine(LogLine);
                                    log.Close();
                                    fs.Close();
                                }
                            }
                            else
                            {
                                FileStream fs = null;
                                fs = new FileStream(callsign + ".txt", FileMode.Append);
                                StreamWriter log = new StreamWriter(fs);
                                log.WriteLine(LogLine);
                                log.Close();
                                fs.Close();
                                Console.WriteLine("Station " + callsign + " is now being tracked on the DSTAR website. Current reflector " + strReflector);
                                if (ConfigurationManager.AppSettings["StatusEmails"] == "Y")
                                {
                                    Email(callsign, strReflector);
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Station " + callsign + " has not transmitted in the last " + intMinutesUntilNotify + " minutes.");
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Program encountered and error:");
                Console.WriteLine(ex.Message);
                LogError(ex.Message, ex.Source);
                if (ConfigurationManager.AppSettings["EmailError"] == "Y")
                {
                    EmailError(ex.Message, ex.Source);
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

        private static void EmailError(string Message, string Source)
        {
            try
            {
                MailMessage mail = new MailMessage();
                mail.Subject = "DSTAR.StationTracking Error";
                mail.From = from;

                EmailAddressListString = toConfig;
                foreach (string emailAddress in _emailAddressList)
                {
                    mail.To.Add(emailAddress);
                }

                mail.Body = "Message: " + Message + " Source: " + Source;

                SmtpClient smtp = new SmtpClient();
                smtp.Host = smtpHost;
                smtp.Port = Convert.ToInt32(smtpPort);

                smtp.Credentials = new NetworkCredential(smtpUser, smtpPswrd);
                smtp.EnableSsl = true;
                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Program encountered and an error sending email:");
                Console.WriteLine(ex.Message);
                LogError(ex.Message, ex.Source);
            }
        }

        private static void Email(string callSign, string Status)
        {
            try
            {
                MailMessage mail = new MailMessage();
                mail.Subject = "DSTAR.StationTracking";
                mail.From = from;

                EmailAddressListString = toConfig;
                foreach (string emailAddress in _emailAddressList)
                {
                    mail.To.Add(emailAddress);
                }

                mail.Body = "Station " + callSign + " has transmitted on " + Status;

                SmtpClient smtp = new SmtpClient();
                smtp.Host = smtpHost;
                smtp.Port = Convert.ToInt32(smtpPort);

                smtp.Credentials = new NetworkCredential(smtpUser, smtpPswrd);
                smtp.EnableSsl = true;
                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending email:");
                Console.WriteLine(ex.Message);
                LogError(ex.Message, ex.Source);
            }
        }

        private static void LogError(string Message, string source)
        {
            try
            {
                FileStream fs = null;
                fs = new FileStream("ErrorLog.txt", FileMode.Append);
                StreamWriter log = new StreamWriter(fs);
                log.WriteLine(DateTime.Now + " Error: " + Message + " Source: " + source);
                log.Close();
                fs.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error logging previous error.");
                Console.WriteLine("Make sure the Error log is not open.");
            }
        }
    }
}
