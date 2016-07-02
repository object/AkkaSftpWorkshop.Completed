using System;
using System.IO;

namespace Shared
{
	public static class ClientFactory
	{
		public static IClientFactory Create(int transferDelay = 0)
		{
			var rootDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../../../sftp"));
			return new LocalFileClientFactory(rootDir, "", transferDelay);
		}
	}
}

