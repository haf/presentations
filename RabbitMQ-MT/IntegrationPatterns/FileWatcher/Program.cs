using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MassTransit;
using MassTransit.NLogIntegration;
using Messages;

namespace FileWatcher
{
	class Program
	{
		static void Main(string[] args)
		{
			var p = new Program();
			p.Run();
		}

		
		IServiceBus _bus;

		void Run()
		{
			var awaiter = new ManualResetEventSlim(false);
			Console.CancelKeyPress += (sender, args) => awaiter.Set();
			
			_bus = ServiceBusFactory.New(sbc =>
				{
					sbc.UseNLog();
					sbc.UseRabbitMqRouting();
					sbc.ReceiveFrom("rabbitmq://localhost/FileWatcher");
					sbc.Subscribe(s =>
						{
							s.Handler<Shutdown>(_ => awaiter.Set());
						});
				});

			var w = new ImageWatcherImpl(@"C:\Users\xyz\Dropbox\Camera Uploads");
			w.ImageAdded += ImageAdded;

			awaiter.Wait();

			w.ImageAdded -= ImageAdded;
			w.Dispose();
			
			_bus.Dispose();
			_bus = null;
		}

		void ImageAdded(object sender, ImageAddedEventArgs e)
		{
			if (_bus == null)
				return;

			_bus.Publish<FileFound>(new Found
			{
				Location = new Uri(e.Path),
				CorrelationId = Guid.NewGuid()
			});
		}
	}


	class Found : FileFound
	{
		public Uri Location { get; set; }
		public Guid CorrelationId { get; set; }
	}

	public interface ImageWatcher : IDisposable
	{
		/// <summary>
		/// Gets the path being listened on.
		/// </summary>
		string Path { get; }

		event EventHandler<ImageAddedEventArgs> ImageAdded;
	}

	public class ImageWatcherImpl : ImageWatcher
	{
		private readonly string _path;
		private readonly FileSystemWatcher _watcher = new FileSystemWatcher();

		public ImageWatcherImpl(string path)
		{
			if (path == null) throw new ArgumentNullException("path");
			_path = path;
			_watcher.Path = path;
			_watcher.IncludeSubdirectories = false;
			_watcher.NotifyFilter = NotifyFilters.FileName;
			_watcher.Created += FileCreated;
			_watcher.EnableRaisingEvents = true;
		}

		private void FileCreated(object sender, FileSystemEventArgs e)
		{
			var a = ImageAdded;
			if (a != null)
				a(this, new ImageAddedEventArgs(e));
		}

		/// <summary>
		/// Gets the path being listened on.
		/// </summary>
		public string Path
		{
			get { return _path; }
		}

		public event EventHandler<ImageAddedEventArgs> ImageAdded;

		#region Implementation of IDisposable

		public void Dispose()
		{
			_watcher.Created -= FileCreated;
			_watcher.EnableRaisingEvents = false;
			_watcher.Dispose();
		}

		#endregion
	}

	/// <summary>
	/// Event arguments when an image is added to the specific folder.
	/// </summary>
	public class ImageAddedEventArgs : EventArgs
	{
		private readonly string _path;

		public ImageAddedEventArgs(FileSystemEventArgs inner)
		{
			_path = inner.FullPath;
		}

		/// <summary>
		/// Gets the path where the image was added.
		/// </summary>
		public string Path
		{
			get { return _path; }
		}

		public override string ToString()
		{
			return string.Format("ImageAddedEventArgs({0})", Path);
		}
	}
}
