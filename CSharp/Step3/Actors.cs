using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

using Shared;
using Messages;

namespace Actors
{
	public class SftpActor : ReceiveActor, IWithUnboundedStash
	{
		private IClientFactory _clientFactory;
		private ISftpClient _connection;
		private const int ConnectionTimeoutInSeconds = 10;
		private DateTimeOffset _idleFromTime;

		public SftpActor(IClientFactory clientFactory)
		{
			_clientFactory = clientFactory;

			Disconnected();
		}

		public IStash Stash { get; set; }

		private void Disconnected()
		{
			Receive<ListDirectory>((cmd) =>
			{
				this.Stash.Stash();

				_connection = _clientFactory.CreateSftpClient();
				_connection.Connect();

				this.Stash.UnstashAll();
				StartIdlePeriod();
				Become(Connected);
			});
		}

		private void Connected()
		{
			Receive<ListDirectory>((cmd) =>
			{
				StopIdlePeriod();

				IEnumerable<SftpFileInfo> result = null;
				try
				{
					result = _connection.ListDirectory(cmd.RemotePath, null);
				}
				catch (Exception)
				{
					result = new SftpFileInfo[] { };
				}
				this.Sender.Tell(result, Self);

				StartIdlePeriod();
			});

			Receive<ReceiveTimeout>((cmd) =>
			{
				if (DateTimeOffset.Now - _idleFromTime > TimeSpan.FromSeconds(ConnectionTimeoutInSeconds))
				{
					StopIdlePeriod();

					_connection.Disconnect();
					_connection.Dispose();

					Become(Disconnected);
				}
			});
		}

		private void StartIdlePeriod()
		{
			_idleFromTime = DateTimeOffset.Now;
			this.SetReceiveTimeout(TimeSpan.FromSeconds(ConnectionTimeoutInSeconds));
		}

		private void StopIdlePeriod()
		{
			this.SetReceiveTimeout(null);
		}
	}
}
