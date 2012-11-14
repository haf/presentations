using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using MassTransit;
using MassTransit.NLogIntegration;
using Messages;
using NLog;

namespace Mailer
{
	class Mailer
	{
		static readonly Logger _logger = LogManager.GetCurrentClassLogger();

		static void Main(string[] args)
		{
			if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
			{
				Console.WriteLine("plz mail pwd");
				return;
			}
			
			var awaiter = new ManualResetEventSlim(false);

			var bus = ServiceBusFactory.New(sbc =>
				{
					sbc.UseNLog();
					sbc.UseRabbitMqRouting();
					sbc.ReceiveFrom("rabbitmq://localhost/Mailer");
					sbc.Subscribe(s =>
						{
							s.Handler<FileFound>((cf, f) => SendMail(args, cf));
							s.Handler<Shutdown>(_ => awaiter.Set());
						});
				});

			_logger.Warn("Mailer Started");

			awaiter.Wait();

			bus.Dispose();
		}

		static void SendMail(string[] args, IConsumeContext<FileFound> cf)
		{
			try
			{
				var f = cf.Message;

				_logger.Warn("Handling FileFound");

				var m = new MailMessage("henrik@logibit.se", "henrik.feldt@jayway.com",
				                        string.Format("Hello from Mailer #{0}", f.CorrelationId),
				                        string.Format("Wake up, found {0}", f.Location));

				var c = new SmtpClient("smtp.gmail.com", 587)
					{
						Credentials = new NetworkCredential("henrik.feldt@jayway.com", args[0]),
						EnableSsl = true
					};

				//c.Send(m);

				_logger.Warn("Mail Sent");

				cf.Bus.Publish<MailSent>(
					new MailSendImpl
						{
							CorrelationId = f.CorrelationId
						});
			}
			catch (Exception e)
			{
				_logger.ErrorException("could not send mail", e);
			}
		}

		class MailSendImpl
			: MailSent
		{
			public Guid CorrelationId { get; set; }
		}
	}
}
