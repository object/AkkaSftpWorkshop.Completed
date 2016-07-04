using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

using Shared;
using Messages;

namespace Actors
{
	public class SftpActor : ReceiveActor
	{
		private readonly IClientFactory _clientFactory;
		private ISftpClient _connection;

		public SftpActor(IClientFactory clientFactory)
		{
			_clientFactory = clientFactory;

			Disconnected();
		}

		private void Disconnected()
		{
			Receive<Connect>((cmd) =>
			{
				_connection = _clientFactory.CreateSftpClient();
				_connection.Connect();

				Become(Connected);
			});
		}

		private void Connected()
		{
			Receive<ListDirectory>((cmd) =>
			{
				IEnumerable<SftpFileInfo> result = null;
				try
				{
					result = _connection.ListDirectory(cmd.RemotePath, null);
				}
				catch (Exception)
				{
					result = new SftpFileInfo[] { };
				}
				this.Sender.Tell(result);
			});
			Receive<Disconnect>((cmd) =>
			{
				_connection.Disconnect();
				_connection.Dispose();

				Become(Disconnected);
			});
		}
	}
}
