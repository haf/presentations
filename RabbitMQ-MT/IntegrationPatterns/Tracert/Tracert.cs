using System.Threading;
using MassTransit;
using MassTransit.NLogIntegration;
using Messages;
using NLog;

namespace Tracert
{
	class Tracert
	{
		static readonly Logger _logger = LogManager.GetCurrentClassLogger();

		static void Main(string[] args)
		{
			var awaiter = new ManualResetEventSlim(false);

			var bus = ServiceBusFactory.New(sbc =>
				{
					sbc.UseNLog();
					sbc.UseRabbitMqRouting();
					sbc.ReceiveFrom("rabbitmq://localhost/Tracert");
					sbc.Subscribe(s =>
						{
							s.Handler<Shutdown>(_ => awaiter.Set());
							s.Handler<Trace>(msg => _logger.Warn("{0}: {1}", msg.CorrelationId, msg.Message));
						});
				});

			_logger.Warn("Started Tracert");
			
			awaiter.Wait();

			bus.Dispose();
		}
	}
}
