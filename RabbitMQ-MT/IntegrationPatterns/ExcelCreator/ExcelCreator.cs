using System;
using System.IO;
using System.Threading;
using Magnum.Caching;
using MassTransit;
using MassTransit.NLogIntegration;
using Messages;
using NLog;
using OfficeOpenXml;

namespace ExcelCreator
{
	class ExcelCreator
	{
		static readonly Logger _logger = LogManager.GetCurrentClassLogger();
		static readonly Cache<Guid, object> _serializer = new ReaderWriterLockedCache<Guid, object>(new DictionaryCache<Guid, object>(g => new object()));

		static void Main(string[] args)
		{
			var awaiter = new ManualResetEventSlim(false);
			var bus = ServiceBusFactory.New(sbc =>
				{
					sbc.UseNLog();
					sbc.UseRabbitMqRouting();
					sbc.ReceiveFrom("rabbitmq://localhost/ExcelCreator");
					sbc.SetConcurrentConsumerLimit(1);
					sbc.Subscribe(s =>
						{
							s.Handler<FileFound>((cf, f) =>
								{
									WithLock(f, () => AddRow(f));
									cf.Bus.Publish<ExcelUpdated>(new ExcelUpdatedImpl
										{
											CorrelationId = f.CorrelationId
										});
								});
							s.Handler<RogerWilco>(f =>
								{
								});
							s.Handler<Shutdown>(_ => awaiter.Set());
						});
				});

			_logger.Warn("ExcelCreator started");

			awaiter.Wait();
			
			bus.Dispose();
		}

		static void AddRow(FileFound found)
		{
			var file = new FileInfo("pictures.xlsx");
			var newFile = !file.Exists;

			try
			{
				using (var package = newFile ? new ExcelPackage() : new ExcelPackage(file))
				{
					// get the first worksheet in the workbook
					if (package.Workbook.Worksheets.Count == 0)
						package.Workbook.Worksheets.Add("Pics");

					var worksheet = package.Workbook.Worksheets[1];

					var start = int.Parse( Convert.ToString(worksheet.Cells[1, 1].Value ?? "2") );
					worksheet.Cells[1, 1].Value = start + 1;

					worksheet.Cells[start, 1].Value = found.Location;

					if (newFile) package.SaveAs(file);
					else package.Save();
				} // the using statement automatically calls Dispose() which closes the package.
			}
			catch (Exception e)
			{
				_logger.ErrorException("Could not add to excel sheet", e);
			}

		}

		static void WithLock(CorrelatedBy<Guid> msg, Action callback)
		{
			var locker = _serializer.Get(msg.CorrelationId);
			lock (locker)
			{
                StaThreadHelper.Execute(callback);
			}
		}

		class ExcelUpdatedImpl
			: ExcelUpdated
		{
			public Guid CorrelationId { get; set; }
		}
	}
}
