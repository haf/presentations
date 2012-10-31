using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Magnum.Caching;
using MassTransit;
using MassTransit.NLogIntegration;
using Messages;
using OfficeOpenXml;

namespace ExcelCreator
{
	class Program
	{
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
							s.Handler<FileFound>(f =>
								{
									WithLock(f, () => AddRow(f));
								});
							s.Handler<RogerWilco>(f =>
								{
								});
							s.Handler<Shutdown>(_ => awaiter.Set());
						});
				});
			awaiter.Wait();
			bus.Dispose();
		}

		static void AddRow(FileFound found)
		{
			var file = new FileInfo("pictures.xlsx");
			if (!file.Exists)
			{
				file.Create();
				file.Refresh();
			}
		
			using (var package = new ExcelPackage(file))
			{
				// get the first worksheet in the workbook
				if (package.Workbook.Worksheets.Count == 0)
					package.Workbook.Worksheets.Add("Pics");

				ExcelWorksheet worksheet = package.Workbook.Worksheets[1];

				int writeTo = 2;
				while (
					string.IsNullOrEmpty(worksheet.Cells[writeTo++, 1].Value.ToString()))
					;

				worksheet.Cells[writeTo, 1].Value = found.Location;
			} // the using statement automatically calls Dispose() which closes the package.
		}

		static void WithLock(CorrelatedBy<Guid> msg, Action callback)
		{
			var locker = _serializer.Get(msg.CorrelationId);
			lock (locker)
			{
				callback();
			}
		}
	}
}
