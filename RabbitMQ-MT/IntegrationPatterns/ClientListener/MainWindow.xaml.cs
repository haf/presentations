using System;
using System.ComponentModel;
using System.Windows;
using Magnum.FileSystem;
using MassTransit;
using MassTransit.NLogIntegration;
using Messages;
using NLog;
using File = System.IO.File;

namespace ClientListener
{
	/// <summary>
	/// 	Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		static readonly Logger _logger = LogManager.GetCurrentClassLogger();

		IServiceBus _bus;

		public MainWindow()
		{
			InitializeComponent();
		}

		protected override void OnActivated(EventArgs e)
		{
			base.OnActivated(e);

			_bus = ServiceBusFactory.New(sbc =>
				{
					sbc.UseNLog();
					sbc.UseRabbitMq();
					sbc.ReceiveFrom("rabbitmq://localhost/ClientListener");
					sbc.Subscribe(s =>
						{
							s.Handler<FileFound>(f =>
								{
									Dispatcher.BeginInvoke(new Action(() =>
										{
											if (!File.Exists(f.Location.LocalPath))
												return;

											new ShowPic(f.Location).ShowDialog();

											_logger.Warn("Publishing RogerWilco");

											_bus.Publish<RogerWilco>(new RogerWilcoImpl
												{
													CorrelationId = f.CorrelationId
												});
										}));
								});

							s.Handler<Shutdown>(_ => Close());
						});
				});
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);

			_bus.Dispose();
		}
	}

	internal class RogerWilcoImpl : RogerWilco
	{
		public Guid CorrelationId { get; set; }
	}
}
