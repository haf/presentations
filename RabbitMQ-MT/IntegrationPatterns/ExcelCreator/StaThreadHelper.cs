using System;
using System.Threading;

namespace ExcelCreator
{
	public static class StaThreadHelper
	{
		public static void Execute(Action action)
		{
			var thrown = InSTA(action);
			
			if (thrown != null)
			{
				thrown.PreserveStackTrace();
				throw thrown;
			}
		}

		static Exception InSTA(Action action)
		{
			Exception thrown = null;
			var t = new Thread(new ThreadStart(delegate
				{
					try
					{
						action();
					}
					catch (Exception e)
					{
						thrown = e;
					}
				}));
			t.SetApartmentState(ApartmentState.STA);
			t.Start();
			t.Join();

			return thrown;
		}
	}
}