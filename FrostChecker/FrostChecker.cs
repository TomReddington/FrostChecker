using HtmlAgilityPack;
using MailKit.Net.Smtp;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace FrostChecker
{
    public class FrostChecker
    {
        private static readonly string EmailSenderName;
        private static readonly string EmailSenderAddress;
        private static readonly string EmailSenderAppPassword;
        private static readonly string EmailRecipientName;
        private static readonly string EmailRecipientAddress;

        static FrostChecker()
        {
            EmailSenderName = Environment.GetEnvironmentVariable("EmailSenderName", EnvironmentVariableTarget.Process);
            EmailSenderAddress = Environment.GetEnvironmentVariable("EmailSenderAddress", EnvironmentVariableTarget.Process);
            EmailSenderAppPassword = Environment.GetEnvironmentVariable("EmailSenderAppPassword", EnvironmentVariableTarget.Process);
            EmailRecipientName = Environment.GetEnvironmentVariable("EmailRecipientName", EnvironmentVariableTarget.Process);
            EmailRecipientAddress = Environment.GetEnvironmentVariable("EmailRecipientAddress", EnvironmentVariableTarget.Process);
        }

        [Function("FrostChecker")]
        public async Task Run([TimerTrigger("0 8 5 * * *")] TimerInfo myTimer)
        {
            //log.LogInformation("Requesting frost data.");

            string url = "https://www.theweatheroutlook.com/weather/will-it-frost/leeds";
            var web = new HtmlWeb();
            var doc = web.Load(url);

            //log.LogInformation("Got frost data. Parsing data...");

            var percentageDatePairs = ToPercentageDatePairs(doc);

            var pairsAboveThreshold = percentageDatePairs.Where(x => x.Percentage >= 50);

            //log.LogInformation("Email required. Sending email...");

            if (pairsAboveThreshold.Any())
            {
                var emailData = ToEmailData(pairsAboveThreshold);
                SendEmail(emailData);
                //log.LogInformation("Email sent.");
            }
            else
            {
                //log.LogInformation("Email not required.");
            }
        }

        private static IEnumerable<(int Percentage, DateOnly Date)> ToPercentageDatePairs(HtmlDocument doc)
        {
            var allPercentageNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'progress-bar')]");

            var nextSevenNodes = allPercentageNodes.TakeLast(13).Take(7);

            int[] nextSevenPercentages = nextSevenNodes
                .Select(x => x.InnerText)
                .Select(x => x.Substring(0, x.Length - 1))
                .Select(x => int.Parse(x))
                .ToArray();

            List<DateOnly> nextSevenDays = new();
            for (int i = 0; i < 7; i++)
            {
                nextSevenDays.Add(DateOnly.FromDateTime(DateTime.Today.AddDays(i)));
            }

            var percentageDatePairs = nextSevenPercentages.Zip(nextSevenDays);

            return percentageDatePairs;
        }

        private static EmailData ToEmailData(IEnumerable<(int Percentage, DateOnly Date)> pairsAboveThreshold)
        {
            string body = string.Join("\r\n", pairsAboveThreshold.Select(x => $"{x.Percentage}% on {x.Date}"));

            var emailData = new EmailData()
            {
                Subject = "FrostChecker",
                Body = body
            };

            return emailData;
        }

        private static void SendEmail(EmailData emailData)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(EmailSenderName, EmailSenderAddress));
            message.To.Add(new MailboxAddress(EmailRecipientName, EmailRecipientAddress));
            message.Subject = emailData.Subject;

            message.Body = new TextPart("plain")
            {
                Text = emailData.Body
            };

            using (var client = new SmtpClient())
            {
                client.Connect("smtp.gmail.com", 587, false);

                client.Authenticate(EmailSenderAddress, EmailSenderAppPassword);

                client.Send(message);
                client.Disconnect(true);
            }
        }

        private class EmailData
        {
            public string Subject { get; set; }
            public string Body { get; set; }
        }
    }
}
