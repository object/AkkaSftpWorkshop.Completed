﻿using System;
using System.IO;
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
		private IAsyncResult _asyncResult;
		private Stream _stream;
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

		public class Cancel : ISftpCommand
		{
			public Cancel(string target)
			{
				this.Target = target;
			}

			public string Target { get; private set; }
		}

		public interface ISftpCommandResult { }

		public class Completed : ISftpCommandResult { }

		public class Cancelled : ISftpCommandResult { }

		public class Error : ISftpCommandResult 
		{
			public Error(string message)
			{
				this.Message = message;
			}

			public string Message { get; private set; }
		}

		private void Disconnected()
		{
			Receive<ISftpCommand>((cmd) =>
			{
				this.Stash.Stash();

				Connect();

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

				AsyncCallback callback = ar =>
				{
					try
					{
						_connection.EndUploadFile(ar);
						var result = _clientFactory.CreateSftpAsyncResult(ar);
						if (result.IsCanceled)
							this.Self.Tell(new Cancelled());
						else
							this.Self.Tell(new Completed());
					}
					catch (Exception ex)
					{
						this.Self.Tell(new Error(ex.Message));
					}
				};
				Utils.EnsureParentDirectoryExists(_connection, cmd.RemotePath);
				_stream = _fileStreamProvider.OpenRead(cmd.LocalPath);
				_asyncResult = _connection.BeginUploadFile(_stream, cmd.RemotePath, callback, null);

				Become(Transferring);
			});

			Receive<DownloadFile>((cmd) =>
			{
				StopIdlePeriod();

				AsyncCallback callback = ar =>
				{
					try
					{
						_connection.EndDownloadFile(ar);
						var result = _clientFactory.CreateSftpAsyncResult(ar);
						if (result.IsCanceled)
							this.Self.Tell(new Cancelled());
						else
							this.Self.Tell(new Completed());
					}
					catch (Exception ex)
					{
						this.Self.Tell(new Error(ex.Message));
					}
				};
				_stream = _fileStreamProvider.OpenWrite(cmd.LocalPath);
				_asyncResult = _connection.BeginDownloadFile(cmd.RemotePath, _stream, callback, null);

				Become(Transferring);
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

		private void Transferring()
		{
			Receive<Cancel>((cmd) =>
			{
				var result = _clientFactory.CreateSftpAsyncResult(_asyncResult);
				result.IsCanceled = true;
			});

			Receive<ISftpCommand>((cmd) =>
			{
				this.Stash.Stash();
			});

			Receive<Completed>((cmd) =>
			{
				_stream.Close();
				this.Stash.UnstashAll();

				StartIdlePeriod();
				Become(Connected);
			});

			Receive<Cancelled>((cmd) =>
			{
				_stream.Close();
				this.Stash.UnstashAll();

				StartIdlePeriod();
				Become(Connected);
			});

			Receive<Error>((cmd) =>
			{
				Disconnect();
				_stream.Close();
				this.Stash.UnstashAll();

				StartIdlePeriod();
				Become(Disconnected);
			});
		}

		private void Connect()
		{
			_connection = _clientFactory.CreateSftpClient();
			_connection.Connect();
		}

		private void Disconnect()
		{
			StopIdlePeriod();
			_connection.Disconnect();
			_connection.Dispose();
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