﻿using System;
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
			Console.WriteLine("We are ready for real things! In step 4 you will implement UploadFile and DownloadFile commands.");
			Console.WriteLine("Once the actor is property implemented the program should display the following messages:");
			Console.WriteLine();
			ColoredConsole.WriteLine(ConsoleColor.Cyan, "SSH.NET: Connecting...");
			ColoredConsole.WriteLine(ConsoleColor.Green, "SSH.NET: Connected.");
			ColoredConsole.WriteLine(ConsoleColor.Cyan, "SSH.NET: Checking if directory <directory name> exists...");
			ColoredConsole.WriteLine(ConsoleColor.Green, "SSH.NET: Directory <directory name> exists.");
			ColoredConsole.WriteLine(ConsoleColor.Cyan, "SSH.NET: Uploading file <file name>...");
			ColoredConsole.WriteLine(ConsoleColor.Green, "SSH.NET: File <file name> is uploaded.");
			ColoredConsole.WriteLine(ConsoleColor.Cyan, "SSH.NET: Downloading file <file name>...");
			ColoredConsole.WriteLine(ConsoleColor.Green, "SSH.NET: File <file name> is downloaded.");
			Console.WriteLine("    pause for about 10 seconds");
			ColoredConsole.WriteLine(ConsoleColor.Cyan, "SSH.NET: Disconnecting...");
			ColoredConsole.WriteLine(ConsoleColor.Green, "SSH.NET: Disconnected.");
			Console.WriteLine();
		}

		private async static Task Run()
		{
			var clientFactory = ClientFactory.Create();
			var actorSystem = ActorSystem.Create("MyActorSystem");

			var sftpActor = actorSystem.ActorOf(
				Props.Create(() => new SftpActor(clientFactory)),
				"sftpActor");

			var remotePath = "/test/12345.dll";
			sftpActor.Tell(new UploadFile("Wire.dll", remotePath));
			sftpActor.Tell(new DownloadFile("Wire.bak", remotePath));
			Console.WriteLine();

			Console.ReadKey();

			await actorSystem.Terminate();
		}
	}
}
