using System;
using MassTransit;

namespace Messages
{
	/// <summary>
	/// File watcher found file
	/// </summary>
	public interface FileFound
		: CorrelatedBy<Guid>
	{
		Uri Location { get; }
	}

	/// <summary>
	/// Excel software did its job
	/// </summary>
	public interface ExcelUpdated
		: CorrelatedBy<Guid>
	{
	}

	public interface MailSent
		: CorrelatedBy<Guid>
	{
	}

	/// <summary>
	/// User has seen event
	/// </summary>
	public interface RogerWilco
		: CorrelatedBy<Guid>
	{
	}

	/// <summary>
	/// The user story is done
	/// </summary>
	public interface UserStoryComplete
		: CorrelatedBy<Guid>
	{
	}

	/// <summary>
	/// Shut yourself down!
	/// </summary>
	public interface Shutdown
	{
	}
}
