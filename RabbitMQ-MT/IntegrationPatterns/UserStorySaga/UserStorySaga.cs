// Copyright 2012 Henrik Feldt

using System;
using System.Data;
using System.Data.SqlServerCe;
using System.IO;
using System.Linq.Expressions;
using System.Threading;
using Automatonymous;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using FluentNHibernate.Conventions.Helpers;
using FluentNHibernate.Mapping;
using Magnum.Caching;
using MassTransit;
using MassTransit.NHibernateIntegration.Saga;
using MassTransit.NLogIntegration;
using Messages;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.SqlTypes;
using NHibernate.Tool.hbm2ddl;
using NHibernate.UserTypes;
using NLog;

namespace UserStorySaga
{
	internal class UserStorySaga
	{
		static readonly Logger _logger = LogManager.GetCurrentClassLogger();
		static readonly string _connectionString = string.Format("Data Source={0}", Path.Combine("App_Data", "sagas.sdf"));

		static void Main(string[] args)
		{
			var awaiter = new ManualResetEventSlim(false);

			_logger.Info("Using db: {0}", Path.GetFullPath(Path.Combine("App_Data", "sagas.sdf")));

			var sessionFactory = Fluently.Configure()
				.Database(MsSqlCeConfiguration.Standard.ConnectionString(
					_connectionString))
				.Mappings(n => n.FluentMappings
					.Add<InstanceMap>()
					.Conventions.Add(DefaultLazy.Never()))
				.ExposeConfiguration(UpdateSchema)
				.BuildSessionFactory();

			var bus = ServiceBusFactory.New(sbc =>
				{
					sbc.UseNLog();
					sbc.UseRabbitMqRouting();
					sbc.ReceiveFrom("rabbitmq://localhost/UserStorySaga");
					sbc.Subscribe(s =>
						{
							s.StateMachineSaga(
								new PictureDiscoverySaga(),
								new NHibernateSagaRepository<PictureDiscoveryStateInstance>(sessionFactory));
							s.Handler<Shutdown>(_ => awaiter.Set());
						});
				});

			_logger.Warn("Started UserStorySaga");
			awaiter.Wait();

			bus.Dispose();
		}

		static void UpdateSchema(Configuration cfg)
		{
			// of course no way to *know* if I need to upgrade it, no idempotent method to call
			try { new SqlCeEngine(_connectionString).Upgrade(); }
			catch (SqlCeException) { }
			new SchemaExport(cfg).Create(true, true);
			//new SchemaUpdate(cfg).Execute(true, false);
		}
	}

	public class PictureDiscoveryStateInstance
		: SagaStateMachineInstance
	{
		/// <summary> for serialization </summary>
		[Obsolete("for serialization")]
		protected PictureDiscoveryStateInstance()
		{
			Started = new DateTime(1970, 01, 01);
			Ended = new DateTime(1970, 01, 01);
		}

		public PictureDiscoveryStateInstance(Guid correlationId)
			: this()
		{
			CorrelationId = correlationId;
		}

		public DateTime Started { get; set; }
		public DateTime Ended { get; set; }

		public State CurrentState { get; set; }

		public Guid CorrelationId { get; set; }
		public IServiceBus Bus { get; set; }

		public CompositeEventStatus CompositeSvcsComplete { get; set; }
	}

	public class PictureDiscoverySaga
		: AutomatonymousStateMachine<PictureDiscoveryStateInstance>
	{
		public PictureDiscoverySaga()
		{
			Event(() => FileFound);
			Event(() => ExcelUpdated);
			Event(() => MailSent);
			Event(() => ServicesComplete, x => x.CompositeSvcsComplete, ExcelUpdated, MailSent);
			Event(() => RogerWilco);

			State(() => WaitingForServices);
			State(() => WaitingForUser);

			Initially(
				When(FileFound)
					.Then(x => x.Started = DateTime.UtcNow)
					.Publish<PictureDiscoveryStateInstance, FileFound, Trace>(
						(i, m) => new TraceImpl
							{
								Message = "Saga noticed that a file was found",
								CorrelationId = m.CorrelationId
							})
				.TransitionTo(WaitingForServices));

			During(WaitingForServices,
				When(ServicesComplete)
					.Publish(instance =>
						new TraceImpl
							{
								Message = "Composite event triggered",
								CorrelationId = instance.CorrelationId
							} as Trace)
					.TransitionTo(WaitingForUser));

			During(WaitingForUser,
				When(RogerWilco)
					.Then(x => x.Ended = DateTime.UtcNow)
					.Publish(instance =>
						new UserStoryCompleteImpl
							{
								CorrelationId = instance.CorrelationId
							} as UserStoryComplete)
					.Publish(instance => new TraceImpl
						{
							Message = string.Format("Finished user story in {0}", 
												    instance.Ended - instance.Started),
							CorrelationId = instance.CorrelationId
						} as Trace)
				.TransitionTo(Final));
		}

		public Event<FileFound> FileFound { get; private set; }
		public Event<ExcelUpdated> ExcelUpdated { get; private set; }
		public Event<MailSent> MailSent { get; private set; }
		public Event<RogerWilco> RogerWilco { get; private set; }
		public Event ServicesComplete { get; private set; }

		public State WaitingForServices { get; private set; }
		public State WaitingForUser { get; private set; }

		class TraceImpl
			: Trace
		{
			public string Message { get; set; }
			public Guid CorrelationId { get; set; }
		}

		class UserStoryCompleteImpl
			: UserStoryComplete
		{
			public Guid CorrelationId { get; set; }
		}
	}

	public class InstanceMap
		: ClassMap<PictureDiscoveryStateInstance>
	{
		public InstanceMap()
		{
			Id(x => x.CorrelationId).GeneratedBy.Assigned();
			
			this.StateProperty<PictureDiscoveryStateInstance, PictureDiscoverySaga>(
				x => x.CurrentState);

			Map(x => x.Started);
			Map(x => x.Ended);

			Map(x => x.CompositeSvcsComplete);
		}
	}

	public static class AutomatonymousNHibernateExtensions
	{
		public static void StateProperty<T, TMachine>(this ClasslikeMapBase<T> mapper, 
			Expression<Func<T, object>> stateExpression)
			where T : class
			where TMachine : StateMachine, new()
		{
			mapper.Map(stateExpression)
				.CustomType<AutomatonymousStateUserType<TMachine>>()
				.Not.Nullable()
				.Length(80);
		}
	}

	public class AutomatonymousStateUserType<T> :
		IUserType
		where T : StateMachine, new()
	{
		static readonly Cache<Type, Cache<string, State>> _stateCache;
		readonly T _machine;

		static AutomatonymousStateUserType()
		{
			_stateCache = new ConcurrentCache<Type, Cache<string, State>>();
		}

		public AutomatonymousStateUserType()
		{
			_machine = new T();
		}

		bool IUserType.Equals(object x, object y)
		{
			var xs = (State) x;
			var ys = (State) y;

			return xs.Name.Equals(ys.Name);
		}

		public int GetHashCode(object x)
		{
			return ((State) x).Name.GetHashCode();
		}

		public object NullSafeGet(IDataReader rs, string[] names, object owner)
		{
			var value = (string) NHibernateUtil.String.NullSafeGet(rs, names);

			var cache = GetStateMethod();

			var state = cache[value];

			return state;
		}

		public void NullSafeSet(IDbCommand cmd, object value, int index)
		{
			if (value == null)
			{
				NHibernateUtil.String.NullSafeSet(cmd, null, index);
				return;
			}

			value = ((State) value).Name;

			NHibernateUtil.String.NullSafeSet(cmd, value, index);
		}

		public object DeepCopy(object value)
		{
			return value ?? null;
		}

		public object Replace(object original, object target, object owner)
		{
			return original;
		}

		public object Assemble(object cached, object owner)
		{
			return cached;
		}

		public object Disassemble(object value)
		{
			return value;
		}

		public SqlType[] SqlTypes
		{
			get { return new[] {NHibernateUtil.String.SqlType}; }
		}

		public Type ReturnedType
		{
			get { return typeof (State); }
		}

		public bool IsMutable
		{
			get { return false; }
		}

		Cache<string, State> GetStateMethod()
		{
			return _stateCache.Get(typeof (T), _ =>
				{
					Cache<string, State> states = new DictionaryCache<string, State>();
					foreach (var state in _machine.States)
					{
						states.Add(state.Name, state);
					}

					return states;
				});
		}
	}
}