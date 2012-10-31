// Copyright 2012 Henrik Feldt

using System;
using System.Threading;
using MassTransit;
using MassTransit.NLogIntegration;
using Messages;
using NLog;
using NLog.Config;
using RabbitMQ.Client;

namespace Consumer
{
	internal class Program
	{
		static void Main(string[] args)
		{
			SimpleConfigurator.ConfigureForConsoleLogging(LogLevel.Trace);

			Console.Write("Starting...");

			var awaiter = new ManualResetEventSlim(false);

			Console.CancelKeyPress += (sender, eventArgs) => awaiter.Set();

			var sb = ServiceBusFactory.New(sbc =>
				{
					sbc.UseNLog();
					sbc.UseRabbitMq();
					sbc.ReceiveFrom("rabbitmq://localhost/Consumer");
					sbc.Subscribe(s => s.Consumer(() => new Printer()));
				});

			Console.WriteLine("Waiting...");

			awaiter.Wait();

			Console.WriteLine("Exiting...");

			sb.Dispose();
		}
	}

	internal class Printer
		: Consumes<FoundFile>.All
	{
		public void Consume(FoundFile file)
		{
			Console.WriteLine("Found {0}", file.Location);
		}
	}
}