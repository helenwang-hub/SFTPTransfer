using System;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace SFTPTransfer
{
    public class Email
    {
        public static string SendEmail(IConfigurationRoot config, string Subject, string Body, string emailAttachment = "", string env = "")
        {
            string Msg = "";
            try
            {
                MailMessage mail = null;
                if (!String.IsNullOrEmpty(env))
                    mail = new MailMessage(config.GetSection("EmailCredentials").GetSection(env).GetSection("FromEmail").Value, config.GetSection("EmailCredentials").GetSection(env).GetSection("ToEmail").Value);
                else
                    mail = new MailMessage(config.GetSection("EmailCredentials").GetSection("FromEmail").Value, config.GetSection("EmailCredentials").GetSection("ToEmail").Value);
                SmtpClient client = new SmtpClient
                {
                    Port = Int32.Parse(config.GetSection("EmailCredentials").GetSection("Port").Value),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Host = config.GetSection("EmailCredentials").GetSection("Host").Value
                };
                mail.Subject = Subject;
                mail.Body = Body;
                if (!string.IsNullOrEmpty(emailAttachment))
                {
                    using (Attachment attachment = new Attachment(emailAttachment))
                    {
                        mail.Attachments.Add(attachment);
                        client.Send(mail);
                    }
                }
                else
                {
                    client.Send(mail);
                }   
                
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString());
                Msg = ex.ToString();
            }
            return Msg;
        }

    }
}