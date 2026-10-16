using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    static class IntranetMailing
    {

        public static void SendMail(string subject, string content, string recipients = "dev-support@archetype-group.com")
        {

            MailMessage mail = new MailMessage("navision-noreply@archetype-group.com", recipients);
            SmtpClient client = new SmtpClient();
            client.Port = 25;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Credentials = new System.Net.NetworkCredential(ConsoleApp1.Properties.Settings.Default.SMTPUser, ConsoleApp1.Properties.Settings.Default.SMTPPass);
            client.Host = ConsoleApp1.Properties.Settings.Default.SMTPHost;
            mail.Subject = subject;
            mail.Body = content;
            mail.IsBodyHtml = true;
            client.Send(mail);
        }


    }
}
