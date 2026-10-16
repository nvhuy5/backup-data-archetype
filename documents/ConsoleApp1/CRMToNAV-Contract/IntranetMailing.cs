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

        public static void SendMail(string subject, string content, string recipients = "dev-support@archetype-group.com,loup.rouan@archetype-group.com,hien.ntt@archetype-group.com", string from = "navision-noreply@archetype-group.com")
        {

            MailMessage mail = new MailMessage(from, recipients);
            SmtpClient client = new SmtpClient();
            client.Port = 25;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;

            client.Host = ConsoleApp1.Properties.Settings.Default.SMTPHost;
            
            mail.Subject = subject;
            mail.Body = content;
            mail.IsBodyHtml = true;
            client.Send(mail);
        }


    }
}
