using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

using Shared;

namespace SftpActors
{
	public class SftpActor : ReceiveActor, IWithUnboundedStash
	{
		private IClientFactory _clientFactory;
		private ISftpClient _connection;
		private IFileStreamProvider _fileStreamProvider;
		private const int ConnectionTimeoutInSeconds = 10;
		private DateTimeOffset _idleFromTime;

		public SftpActor(IClientFactory clientFactory)
		{
			_clientFactory = clientFactory;
			_fileStreamProvider = _clientFactory.CreateFileStreamProvider();

			Disconnected();
		}

		public IStash Stash { get; set; }

		public interface ISftpCommand { }

		public class ListDirectory : ISftpCommand
		{
			public ListDirectory(string remotePath)
			{
				this.RemotePath = remotePath;
			}

			public string RemotePath { get; private set; }
		}

		public class UploadFile : ISftpCommand
		{
			public UploadFile(string localPath, string remotePath)
			{
				this.LocalPath = localPath;
				this.RemotePath = remotePath;
			}

			public string LocalPath { get; private set; }
			public string RemotePath { get; private set; }
		}

		public class DownloadFile : ISftpCommand
		{
			public DownloadFile(string localPath, string remotePath)
			{
				this.LocalPath = localPath;
				this.RemotePath = remotePath;
			}

			public string LocalPath { get; private set; }
			public string RemotePath { get; private set; }
		}


		private void Disconnected()
		{
			Receive<ISftpCommand>((cmd) =>
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

			Receive<UploadFile>((cmd) =>
			{
				StopIdlePeriod();

				Utils.EnsureParentDirectoryExists(_connection, cmd.RemotePath);
				var stream = _fileStreamProvider.OpenRead(cmd.LocalPath);
				_connection.UploadFile(stream, cmd.RemotePath, null);

				StartIdlePeriod();
			});

			Receive<DownloadFile>((cmd) =>
			{
				StopIdlePeriod();

				var stream = _fileStreamProvider.OpenWrite(cmd.LocalPath);
				_connection.DownloadFile(cmd.RemotePath, stream, null);

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
