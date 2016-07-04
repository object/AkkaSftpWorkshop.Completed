using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;

using Shared;
using Messages;
using Actors;

namespace Application
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			PrintInstructions();

			Console.Write("Press any key to start the actor system and validate the implementation.");
			Console.ReadKey();

			Run().Wait();
		}

		private static void PrintInstructions()
		{
			Console.WriteLine("No new commands are implemented in the step 3. In fact you will have to remove previously implemented Connect and Disconnect commands,");
			Console.WriteLine("so the actor will manage FTP connections internally, connect on demand and reconnect after being 10 seconds idle.");
			Console.WriteLine("Once the actor is property implemented the program should display the following messages:");
			Console.WriteLine();
			ColoredConsole.WriteLine(ConsoleColor.Cyan, "SSH.NET: Connecting...");
			ColoredConsole.WriteLine(ConsoleColor.Green, "SSH.NET: Connected.");
			ColoredConsole.WriteLine(ConsoleColor.Cyan, "SSH.NET: Listing directory <directory name>...");
			ColoredConsole.WriteLine(ConsoleColor.Green, "SSH.NET: Directory <directory name> is listed.");
			Console.WriteLine("    Directory listing results");
			Console.WriteLine("    pause for about 10 seconds");
			ColoredConsole.WriteLine(ConsoleColor.Cyan, "SSH.NET: Disconnecting...");
			ColoredConsole.WriteLine(ConsoleColor.Green, "SSH.NET: Disconnected.");
			Console.WriteLine();
		}

		private static async Task Run()
		{
			var actorSystem = ActorSystem.Create("MyActorSystem");

            var runnerActor = actorSystem.ActorOf(
                Props.Create(() => new RunnerActor()),
                "runnerActor");

            runnerActor.Tell(new Run());

            Console.ReadKey();

			await actorSystem.Terminate();
		}
	}
}
