using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

using Shared;

namespace SftpActors
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

		public class Connect { }
		public class Disconnect { }
		public class ListDirectory
		{
			public ListDirectory(string remotePath)
			{
				this.RemotePath = remotePath;
			}

			public string RemotePath { get; private set; }
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
