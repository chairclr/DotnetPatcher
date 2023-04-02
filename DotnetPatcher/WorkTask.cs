using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.Util;


namespace DotnetPatcher
{

	public class WorkTask
	{
		public delegate void Worker();

		public readonly Worker task;

		public WorkTask(Worker worker)
		{
			this.task = worker;
		}

		public static void ExecuteParallel(List<WorkTask> items)
		{
			try
			{
				List<string> working = new List<string>();
				Parallel.ForEach(Partitioner.Create(items, EnumerablePartitionerOptions.NoBuffering),
					// leave some cores to not use the entire cpu
					new ParallelOptions { MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount - 1, 1) },
					item =>
					{
						item.task();
					});
			}
			catch (AggregateException ex)
			{
				IEnumerable<Exception> actual = ex.Flatten().InnerExceptions.Where(e => !(e is OperationCanceledException));
				if (!actual.Any())
					throw new OperationCanceledException();

				throw new AggregateException(actual);
			}
		}
	}
}
