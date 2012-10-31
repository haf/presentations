using System;
using System.ComponentModel;
using System.Windows;
using MassTransit;
using MassTransit.NLogIntegration;
using Messages;

namespace ClientListener
{
	/// <summary>
	/// 	Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
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
					sbc.UseRabbitMqRouting();
					sbc.ReceiveFrom("rabbitmq://localhost/ClientListener");
					sbc.Subscribe(s =>
						{
							s.Handler<FileFound>(f =>
								{
									Dispatcher.Invoke(new Action(() =>
										{
											new ShowPic(f.Location).ShowDialog();
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
