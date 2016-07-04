using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;

using Shared;
using Messages;

namespace Actors
{
    public class RunnerActor : UntypedActor
    {
        protected override void OnReceive(object message)
        {
            if (message is Run)
            {
                var sftpActor = Context.ActorOf(
                        Props.Create(() => new SftpActor(ClientFactory.Create(0))),
                        "sftpActor");

                var remotePath = "/";
                sftpActor.Ask(new ListDirectory(remotePath))
                    .ContinueWith(result =>
                    {
                        var dirs = result.Result as IEnumerable<SftpFileInfo>;
                        if (dirs.Any())
                        {
                            foreach (var entry in dirs)
                            {
                                Console.WriteLine("{0}: {1}",
                                      entry.IsDirectory ? "Directory" : "File",
                                      entry.Name);
                            }
                        }
                        else
                        {
                            Console.WriteLine("The remote directory is empty");
                        }
                    }).PipeTo(Self);

                Console.WriteLine();
                sftpActor.Tell(PoisonPill.Instance);
            }
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return new OneForOneStrategy(
                3,
                TimeSpan.FromSeconds(10),
                x =>
                {
                    if (x is System.IO.FileNotFoundException) return Directive.Resume;
                    if (x is NotSupportedException) return Directive.Stop;
                    if (x is NotImplementedException) return Directive.Stop;

                    return Directive.Restart;
                });
        }
    }

    public class SftpActor : ReceiveActor, IWithUnboundedStash
	{
		private readonly IClientFactory _clientFactory;
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
