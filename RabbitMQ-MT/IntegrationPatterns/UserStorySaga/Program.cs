// Copyright 2012 Henrik Feldt

using System;
using System.Data;
using System.Linq.Expressions;
using System.Threading;
using Automatonymous;
using Magnum.Caching;
using MassTransit;
using MassTransit.NLogIntegration;
using Messages;
using NHibernate;
using NHibernate.Mapping.ByCode;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;

namespace UserStorySaga
{
	internal class Program
	{
		static void Main(string[] args)
		{
			var awaiter = new ManualResetEventSlim(false);
			var bus = ServiceBusFactory.New(sbc =>
				{
					sbc.UseNLog();
					sbc.UseRabbitMqRouting();
					sbc.ReceiveFrom("rabbitmq://localhost/UserStorySaga");
					sbc.Subscribe(s =>
						{
							//s.StateMachineSaga(new )
							s.Handler<Shutdown>(_ => awaiter.Set());
						});
				});
			awaiter.Wait();
			bus.Dispose();
		}
	}

	public static class AutomatonymousNHibernateExtensions
	{
		public static void StateProperty<T, TMachine>(this IClassMapper<T> mapper, Expression<Func<T, State>> stateExpression)
			where T : class
			where TMachine : StateMachine, new()
		{
			mapper.Property(stateExpression, x =>
				{
					x.Type<AutomatonymousStateUserType<TMachine>>();
					x.NotNullable(true);
					x.Length(80);
				});
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