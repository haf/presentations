using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using MassTransit;
using MassTransit.NLogIntegration;
using Messages;

namespace Mailer
{
	class Program
	{
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
							s.Handler<FileFound>(f =>
								{
									var m = new MailMessage("henrik@logibit.se", "henrik.feldt@jayway.com", "Hello from Mailer", "Wake up");
									var c = new SmtpClient("smtp.gmail.com", 587)
										{
											Credentials = new NetworkCredential("henrik.feldt@jayway.com", args[0]),
											EnableSsl = true
										};
									c.Send(m);
								});
							s.Handler<Shutdown>(_ => awaiter.Set());
						});
				});
			awaiter.Wait();
			bus.Dispose();
		}
	}
}
