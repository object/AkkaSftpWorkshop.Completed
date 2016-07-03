using System;
using Akka.Actor;

using Shared;
using Messages;

namespace Actors
{
	public class SftpActor : ReceiveActor
	{
		private IClientFactory _clientFactory;
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
			Receive<Disconnect>((cmd) =>
			{
				_connection.Disconnect();
				_connection.Dispose();

				Become(Disconnected);
			});
		}
	}
}
